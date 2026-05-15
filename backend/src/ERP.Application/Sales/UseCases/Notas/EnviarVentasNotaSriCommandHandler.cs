using System.Text;
using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Sales.Events;
using ERP.Domain.Modules.Sales.Interfaces;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Sales.UseCases.Notas;

public sealed class EnviarVentasNotaSriCommandHandler : IRequestHandler<EnviarVentasNotaSriCommand, Result<Guid>>
{
    private readonly ISalesRepository             _ventasRepository;
    private readonly ISriSettingsRepository _configSriRepository;
    private readonly ISriFacturaElectronicaService _sriService;
    private readonly IFileStorage                _fileStorage;
    private readonly IAccountingService          _accounting;
    private readonly IProductRepository          _productRepository;
    private readonly IUserActivityRepository     _activity;
    private readonly IUnitOfWork                 _unitOfWork;
    private readonly ICurrentTenant              _currentTenant;
    private readonly ICurrentUser                _currentUser;
    private readonly ILogger<EnviarVentasNotaSriCommandHandler> _logger;

    public EnviarVentasNotaSriCommandHandler(
        ISalesRepository ventasRepository,
        ISriSettingsRepository configSriRepository,
        ISriFacturaElectronicaService sriService,
        IFileStorage fileStorage,
        IAccountingService accounting,
        IProductRepository productRepository,
        IUserActivityRepository activity,
        IUnitOfWork unitOfWork,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        ILogger<EnviarVentasNotaSriCommandHandler> logger)
    {
        _ventasRepository    = ventasRepository;
        _configSriRepository = configSriRepository;
        _sriService          = sriService;
        _fileStorage         = fileStorage;
        _accounting          = accounting;
        _productRepository   = productRepository;
        _activity            = activity;
        _unitOfWork          = unitOfWork;
        _currentTenant       = currentTenant;
        _currentUser         = currentUser;
        _logger              = logger;
    }

    public async Task<Result<Guid>> Handle(EnviarVentasNotaSriCommand command, CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;
        var userId   = _currentUser.UserId;

        var nota = await _ventasRepository.GetNoteByIdWithLinesAsync(tenantId, command.NotaId, ct);
        if (nota is null)
            return Result<Guid>.Failure("Nota no encontrada.");

        if (nota.Status == "Borrador")
            nota.Validate(userId);

        if (nota.Status != "Validado")
            return Result<Guid>.Failure($"La nota debe estar Validada para enviar (estado: {nota.Status}).");

        var facturaOriginal = nota.OriginalBill;
        if (facturaOriginal.Status != "Autorizado")
            return Result<Guid>.Failure("La factura original debe permanecer autorizada.");

        var configSri = await _configSriRepository.GetByTenantIdAsync(tenantId, ct);
        if (configSri is null)
            return Result<Guid>.Failure("La configuración SRI no está configurada para este tenant.");

        var detalles = nota.Lines.ToList();
        string xmlContent;
        byte[] xmlFirmado;
        try
        {
            xmlContent = await _sriService.GenerarXmlNotaCreditoDebitoAsync(facturaOriginal, nota, detalles, configSri);
            xmlFirmado = await _sriService.FirmarXmlAsync(
                xmlContent, configSri.CertP12Path, configSri.CertPassword);
        }
        catch (SriCommunicationException ex)
        {
            nota.Reject(userId, ex.Message);
            await _ventasRepository.SaveChangesAsync(ct);
            return Result<Guid>.Failure($"Error SRI: {ex.Message}");
        }
        catch (Exception ex)
        {
            nota.Reject(userId, ex.Message);
            await _ventasRepository.SaveChangesAsync(ct);
            return Result<Guid>.Failure($"Error al generar XML: {ex.Message}");
        }

        SriAutorizacionResponse response;
        try
        {
            response = await _sriService.EnviarAlSriAsync(xmlFirmado, configSri.WsdlUrl);
        }
        catch (SriCommunicationException ex)
        {
            nota.Reject(userId, ex.Message);
            await _ventasRepository.SaveChangesAsync(ct);
            return Result<Guid>.Failure($"Error SRI: {ex.Message}");
        }
        catch (Exception ex)
        {
            nota.Reject(userId, ex.Message);
            await _ventasRepository.SaveChangesAsync(ct);
            return Result<Guid>.Failure($"Error de comunicación con SRI: {ex.Message}");
        }

        if (!response.IsAuthorized)
        {
            nota.Reject(userId, response.ErrorMessage ?? "Rechazada por el SRI.");
            await _ventasRepository.SaveChangesAsync(ct);
            return Result<Guid>.Failure(response.ErrorMessage ?? "El SRI rechazó la nota.");
        }

        var xmlGeneradoPath     = $"ventas/notas/{tenantId}/{nota.Id}/generado.xml";
        var xmlAutorizacionPath = $"ventas/notas/{tenantId}/{nota.Id}/autorizado.xml";
        try
        {
            await _fileStorage.SaveAsync(xmlGeneradoPath, new MemoryStream(xmlFirmado), ct);
            await _fileStorage.SaveAsync(xmlAutorizacionPath,
                new MemoryStream(Encoding.UTF8.GetBytes(response.AuthorizedXml)), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo guardar XML de nota {NotaId}", nota.Id);
            xmlGeneradoPath     = null;
            xmlAutorizacionPath = null;
        }

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var numero = $"{nota.EstabCode}-{nota.EmPointCode}-{nota.Sequential}";
            Result<Guid> asientoResult;
            if (string.Equals(nota.NoteType, "CREDITO", StringComparison.OrdinalIgnoreCase))
                asientoResult = await _accounting.CrearAsientoNotaCreditoVentaAsync(
                    nota.Id, numero, nota.IssueDate, nota.Subtotal, nota.VatTotal, nota.Total,
                    $"Nota de crédito {numero}", ct);
            else
                asientoResult = await _accounting.CrearAsientoNotaDebitoVentaAsync(
                    nota.Id, numero, nota.IssueDate, nota.Subtotal, nota.VatTotal, nota.Total,
                    $"Nota de débito {numero}", ct);

            if (!asientoResult.IsSuccess)
            {
                await _unitOfWork.RollbackAsync(ct);
                nota.Reject(userId, $"Autorizado por SRI pero falló el asiento: {asientoResult.Error}");
                await _ventasRepository.SaveChangesAsync(ct);
                return Result<Guid>.Failure(asientoResult.Error ?? "Error contable.");
            }

            if (string.Equals(nota.NoteType, "CREDITO", StringComparison.OrdinalIgnoreCase))
            {
                var stockLines = new List<SalesNoteStockLine>();
                foreach (var detalle in detalles)
                {
                    var producto = await _productRepository.GetByIdAsync(detalle.ProductId, tenantId, ct);
                    if (producto is null || producto.IsService || !producto.TracksStock)
                        continue;
                    stockLines.Add(new SalesNoteStockLine(detalle.ProductId, detalle.Quantity));
                }

                nota.AuthorizeCreditNote(
                    userId,
                    facturaOriginal.WarehouseId,
                    response.AuthNumber,
                    response.AuthDate,
                    xmlGeneradoPath,
                    xmlAutorizacionPath,
                    asientoResult.Value,
                    stockLines);
            }
            else
            {
                nota.AuthorizeDebitNote(
                    userId,
                    response.AuthNumber,
                    response.AuthDate,
                    xmlGeneradoPath,
                    xmlAutorizacionPath,
                    asientoResult.Value);
            }

            await _activity.AddAsync(UserActivity.Create(
                tenantId, userId, _currentUser.Email, _currentUser.FullName,
                module: "ventas", action: "ventas.nota.enviar",
                entityType: "SalesNote", entityId: nota.Id,
                description: $"{numero} — auth {response.AuthNumber}"), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);
            return Result<Guid>.Success(nota.Id);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            nota.Reject(userId, ex.Message);
            await _ventasRepository.SaveChangesAsync(ct);
            return Result<Guid>.Failure(ex.Message);
        }
    }
}
