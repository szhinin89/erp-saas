using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.Services;
using ERP.Application.Sales.Helpers;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.Purchasing.Entities;
using ERP.Domain.Modules.Purchasing.Enums;
using ERP.Domain.Modules.Purchasing.Interfaces;

namespace ERP.Application.Modules.Purchasing.UseCases.Retentions;

public sealed class GenerateIssuedRetentionCommandHandler
    : IRequestHandler<GenerateIssuedRetentionCommand, Result<Guid>>
{
    private readonly IPurchBillRepository                    _compraRepository;
    private readonly ISriSettingsRepository                  _configSriRepository;
    private readonly IRetentionSettingsRepository            _configRetencionRepository;
    private readonly IEmissionPointRepository                _emissionPointRepository;
    private readonly IDocumentSequenceRepository             _docSeqRepository;
    private readonly IUserActivityRepository                 _activity;
    private readonly ICurrentSubscriber                      _currentSubscriber;
    private readonly ICurrentCompany                         _currentCompany;
    private readonly ICurrentUser                            _currentUser;
    private readonly IUnitOfWork                             _unitOfWork;
    private readonly ILogger<GenerateIssuedRetentionCommandHandler> _logger;

    public GenerateIssuedRetentionCommandHandler(
        IPurchBillRepository compraRepository,
        ISriSettingsRepository configSriRepository,
        IRetentionSettingsRepository configRetencionRepository,
        IEmissionPointRepository emissionPointRepository,
        IDocumentSequenceRepository docSeqRepository,
        IUserActivityRepository activity,
        ICurrentSubscriber currentSubscriber,
        ICurrentCompany currentCompany,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        ILogger<GenerateIssuedRetentionCommandHandler> logger)
    {
        _compraRepository          = compraRepository;
        _configSriRepository       = configSriRepository;
        _configRetencionRepository = configRetencionRepository;
        _emissionPointRepository   = emissionPointRepository;
        _docSeqRepository          = docSeqRepository;
        _activity                  = activity;
        _currentSubscriber         = currentSubscriber;
        _currentCompany            = currentCompany;
        _currentUser               = currentUser;
        _unitOfWork                = unitOfWork;
        _logger                    = logger;
    }

    public async Task<Result<Guid>> Handle(GenerateIssuedRetentionCommand command, CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        var userId   = _currentUser.UserId;

        var compra = await _compraRepository.GetByIdAsync(subscriberId, command.PurchBillId, ct);
        if (compra is null)
            return Result<Guid>.Failure("Compra no encontrada.");
        if (compra.Status != PurchaseStatus.Approved)
            return Result<Guid>.Failure("Solo se puede generar retención para una compra Aprobada.");

        var configs = await _configRetencionRepository.GetActiveForSupplierAsync(subscriberId, ct);
        if (configs.Count == 0)
            return Result<Guid>.Failure(
                "No hay tasas de retención activas para Supplier. Configure en Configuración de retenciones.");

        var hasSriConfig = await _configSriRepository.GetByCompanyIdAsync(_currentCompany.CompanyId, ct);
        if (hasSriConfig is null)
            return Result<Guid>.Failure("La configuración SRI no está configurada para esta empresa.");

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var sriConfig = await _configSriRepository.GetByCompanyIdForUpdateAsync(_currentCompany.CompanyId, ct)
                ?? throw new InvalidOperationException("SRI config no encontrada durante la transacción.");

            var emPoint = await _emissionPointRepository.GetDefaultForCompanyAsync(
                    subscriberId, _currentCompany.CompanyId, ct)
                ?? throw new InvalidOperationException("No hay punto de emisión predeterminado para esta empresa.");

            var docSeq = await _docSeqRepository.GetForUpdateAsync(emPoint.Id, "07", ct)
                ?? throw new InvalidOperationException($"No hay secuencial configurado para Retención en el punto de emisión {emPoint.Id}.");

            var secuencial = docSeq.CaptureAndIncrement();

            var fecha = DateTime.UtcNow;
            var clave = ClaveAccesoHelper.Generar(
                sriConfig.Ruc, sriConfig.Environment, emPoint.Establishment.Code,
                emPoint.Code, sriConfig.EmissionType, secuencial, fecha, "07");

            var ret = IssuedRetention.Create(
                subscriberId, compra.BusinessPartnerId, compra.Id, clave, fecha,
                emPoint.Establishment.Code, emPoint.Code, secuencial, userId);

            foreach (var cfg in configs)
            {
                decimal @base;
                if (cfg.TaxType == "IVA")
                {
                    if (compra.VatTotal <= 0) continue;
                    @base = compra.VatTotal;
                }
                else if (cfg.TaxType == "RENTA")
                {
                    @base = compra.Subtotal;
                }
                else
                    continue;

                var valor = PurchaseRetentionCalculo.CalcularValorRetenido(@base, cfg.Percentage);
                if (valor <= 0) continue;

                var det = PurchRetentionLine.Create(
                    subscriberId, cfg.TaxType, cfg.SriCode, @base, cfg.Percentage, valor,
                    compra.InvoiceNumber, userId);
                det.AssignRetentionId(ret.Id);
                ret.AddLine(det);
            }

            if (ret.Lines.Count == 0)
            {
                await _unitOfWork.RollbackAsync(ct);
                return Result<Guid>.Failure("No se generaron líneas de retención (bases o porcentajes en cero).");
            }

            await _compraRepository.AddIssuedRetentionAsync(ret, ct);
            await _activity.AddAsync(UserActivity.Create(
                subscriberId, userId, _currentUser.Email, _currentUser.FullName,
                module: "compras", action: "compras.retencion.generar",
                entityType: "IssuedRetention", entityId: ret.Id,
                description: $"Retención borrador clave {clave}"), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);
            _logger.LogInformation("Retención emitida borrador {Id} para compra {CompraId}", ret.Id, compra.Id);
            return Result<Guid>.Success(ret.Id);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            return Result<Guid>.Failure(ex.Message);
        }
    }

}
