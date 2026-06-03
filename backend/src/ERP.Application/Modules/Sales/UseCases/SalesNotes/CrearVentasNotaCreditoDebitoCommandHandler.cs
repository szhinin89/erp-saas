using ERP.Domain.Common;
using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.Fiscal.Integration;
using ERP.Application.Sales.Helpers;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Interfaces;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Sales.UseCases.SalesNotes;

public sealed class CrearSalesNoteCommandHandler
    : IRequestHandler<CreateSalesNoteCommand, Result<Guid>>
{
    private readonly ISalesRepository            _ventasRepository;
    private readonly ISalesOriginalBillResolver  _originalBillResolver;
    private readonly ISriSettingsRepository      _configSriRepository;
    private readonly ICompanyRepository          _companyRepository;
    private readonly IEmissionPointRepository    _emissionPointRepository;
    private readonly IDocumentSequenceRepository _docSeqRepository;
    private readonly IProductRepository          _productRepository;
    private readonly ISriGlobalRateReader        _sriRates;
    private readonly IUserActivityRepository     _activity;
    private readonly ICurrentSubscriber          _currentSubscriber;
    private readonly ICurrentCompany             _currentCompany;
    private readonly ICurrentUser                _currentUser;
    private readonly IUnitOfWork                 _unitOfWork;
    private readonly ILogger<CrearSalesNoteCommandHandler> _logger;

    public CrearSalesNoteCommandHandler(
        ISalesRepository ventasRepository,
        ISalesOriginalBillResolver originalBillResolver,
        ISriSettingsRepository configSriRepository,
        ICompanyRepository companyRepository,
        IEmissionPointRepository emissionPointRepository,
        IDocumentSequenceRepository docSeqRepository,
        IProductRepository productRepository,
        ISriGlobalRateReader sriRates,
        IUserActivityRepository activity,
        ICurrentSubscriber currentSubscriber,
        ICurrentCompany currentCompany,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        ILogger<CrearSalesNoteCommandHandler> logger)
    {
        _ventasRepository        = ventasRepository;
        _originalBillResolver    = originalBillResolver;
        _configSriRepository     = configSriRepository;
        _companyRepository       = companyRepository;
        _emissionPointRepository = emissionPointRepository;
        _docSeqRepository        = docSeqRepository;
        _productRepository       = productRepository;
        _sriRates                = sriRates;
        _activity                = activity;
        _currentSubscriber       = currentSubscriber;
        _currentCompany          = currentCompany;
        _currentUser             = currentUser;
        _unitOfWork              = unitOfWork;
        _logger                  = logger;
    }

    public async Task<Result<Guid>> Handle(CreateSalesNoteCommand command, CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        var userId   = _currentUser.UserId;

        if (command.Items.Count == 0)
            return Result<Guid>.Failure("La nota debe tener al menos un detalle.");

        var factura = await _originalBillResolver.ResolveAsync(subscriberId, command.OriginalBillId, ct);
        if (factura is null)
            return Result<Guid>.Failure("Factura original no encontrada.");
        if (factura.Status != "Autorizado")
            return Result<Guid>.Failure(
                $"La factura original debe estar Autorizada (estado actual: {factura.Status}).");

        var hasSriConfig = await _configSriRepository.GetByCompanyIdAsync(_currentCompany.CompanyId, ct);
        if (hasSriConfig is null)
            return Result<Guid>.Failure("La configuraciÃ³n SRI no estÃ¡ configurada para esta empresa.");

        var productos = new Dictionary<Guid, ERP.Domain.Products.Entities.Product>();
        foreach (var item in command.Items)
        {
            if (productos.ContainsKey(item.ProductId)) continue;
            var p = await _productRepository.GetByIdAsync(item.ProductId, subscriberId, ct);
            if (p is null || !p.IsActive)
                return Result<Guid>.Failure($"Producto {item.ProductId} no existe o no estÃ¡ activo.");
            productos[item.ProductId] = p;
        }

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var sriConfig = await _configSriRepository.GetByCompanyIdForUpdateAsync(_currentCompany.CompanyId, ct)
                ?? throw new InvalidOperationException("SRI config no encontrada durante la transacciÃ³n.");

            var company = await _companyRepository.GetByIdAsync(_currentCompany.CompanyId, ct)
                ?? throw new InvalidOperationException("Empresa no encontrada.");

            var noteTypeEnum = Enum.Parse<NoteType>(command.NoteType, ignoreCase: true);
            var tipoDoc = noteTypeEnum == NoteType.Credit ? "04" : "05";

            var emPoint = await _emissionPointRepository.GetDefaultForBranchAsync(
                    _currentSubscriber.SubscriberId, factura.BranchId, ct)
                ?? throw new InvalidOperationException($"No hay punto de emisiÃ³n predeterminado para la sucursal {factura.BranchId}.");

            var docSeq = await _docSeqRepository.GetForUpdateAsync(emPoint.Id, tipoDoc, ct)
                ?? throw new InvalidOperationException($"No hay secuencial configurado para doc {tipoDoc} en el punto de emisiÃ³n {emPoint.Id}.");

            var secuencial = docSeq.CaptureAndIncrement();

            var issueDate = DateTime.UtcNow;
            var accessKey = ClaveAccesoHelper.Generar(
                company.Ruc, sriConfig.Environment, emPoint.Establishment.Code,
                emPoint.Code, sriConfig.EmissionType, secuencial, issueDate, tipoDoc);

            var detalles = new List<SalesNoteLine>();
            foreach (var item in command.Items)
            {
                var producto     = productos[item.ProductId];
                var subtotalItem = item.Quantity * item.UnitPrice;
                decimal impuestoItem = 0;
                string  vatCode      = "0";
                decimal vatPct       = 0m;

                if (producto.AppliesVatOnSale && !string.IsNullOrWhiteSpace(producto.SaleVatCode))
                {
                    var pct = await _sriRates.GetVatPercentageAsync(producto.SaleVatCode, ct);
                    if (pct.HasValue)
                    {
                        vatPct       = pct.Value;
                        vatCode      = producto.SaleVatCode;
                        impuestoItem = subtotalItem * vatPct / 100;
                    }
                }

                var det = SalesNoteLine.Create(
                    subscriberId, item.ProductId, producto.SaleCode, item.Quantity, item.UnitPrice,
                    vatCode, vatPct, impuestoItem, producto.Description, userId);
                detalles.Add(det);
            }

            var nota = SalesNote.Create(
                subscriberId,
                factura.Id,
                noteTypeEnum,
                command.Reason,
                tipoDoc,
                emPoint.Establishment.Code,
                emPoint.Code,
                secuencial,
                accessKey,
                issueDate,
                userId);

            foreach (var d in detalles)
            {
                d.AssignNoteId(nota.Id);
                nota.AddLine(d);
            }

            await _ventasRepository.AddNoteAsync(nota, ct);

            await _activity.AddAsync(UserActivity.Create(
                subscriberId, userId, _currentUser.Email, _currentUser.FullName,
                module: "ventas", action: "ventas.nota.crear",
                entityType: "SalesNote", entityId: nota.Id,
                description: $"{nota.NoteType} {nota.EstabCode}-{nota.EmPointCode}-{nota.Sequential}"), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation("Nota {NotaId} creada en Borrador para factura {FacturaId}", nota.Id, factura.Id);
            return Result<Guid>.Success(nota.Id);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            _logger.LogError(ex, "Error al crear nota de crÃ©dito/dÃ©bito");
            return Result<Guid>.Failure($"No se pudo crear la nota: {ex.Message}");
        }
    }

    private static string SriVatCodeFromPercentage(decimal percentage) => percentage switch
    {
        0m  => "0",
        5m  => "5",
        12m => "2",
        14m => "3",
        15m => "4",
        _   => "4"
    };
}

