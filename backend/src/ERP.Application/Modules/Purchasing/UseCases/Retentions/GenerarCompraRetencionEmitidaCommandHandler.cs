using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.Services;
using ERP.Application.Sales.Helpers;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Purchasing.Entities;
using ERP.Domain.Modules.Purchasing.Enums;
using ERP.Domain.Modules.Purchasing.Interfaces;

namespace ERP.Application.Modules.Purchasing.UseCases.Retentions;

public sealed class GenerateIssuedRetentionCommandHandler
    : IRequestHandler<GenerateIssuedRetentionCommand, Result<Guid>>
{
    private readonly IPurchBillRepository                    _compraRepository;
    private readonly ISriSettingsRepository        _configSriRepository;
    private readonly IRetentionSettingsRepository  _configRetencionRepository;
    private readonly IUserActivityRepository              _activity;
    private readonly ICurrentSubscriber                       _currentSubscriber;
    private readonly ICurrentUser                         _currentUser;
    private readonly IUnitOfWork                          _unitOfWork;
    private readonly ILogger<GenerateIssuedRetentionCommandHandler> _logger;

    public GenerateIssuedRetentionCommandHandler(
        IPurchBillRepository compraRepository,
        ISriSettingsRepository configSriRepository,
        IRetentionSettingsRepository configRetencionRepository,
        IUserActivityRepository activity,
        ICurrentSubscriber currentSubscriber,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        ILogger<GenerateIssuedRetentionCommandHandler> logger)
    {
        _compraRepository           = compraRepository;
        _configSriRepository       = configSriRepository;
        _configRetencionRepository = configRetencionRepository;
        _activity                  = activity;
        _currentSubscriber             = currentSubscriber;
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

        var configSri = await _configSriRepository.GetBySubscriberIdAsync(subscriberId, ct);
        if (configSri is null)
            return Result<Guid>.Failure("La configuración SRI no está configurada.");

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var secuencial = CapturarSecuencial(configSri);
            await _configSriRepository.UpdateAsync(configSri, ct);

            var fecha = DateTime.UtcNow;
            var clave = ClaveAccesoHelper.Generar(
                configSri.Ruc, configSri.Environment, configSri.EstabCode,
                configSri.EmPointCode, configSri.EmissionType, secuencial, fecha, "07");

            var ret = IssuedRetention.Create(
                subscriberId, compra.BusinessPartnerId, compra.Id, clave, fecha,
                configSri.EstabCode, configSri.EmPointCode, secuencial, userId);

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

    private static string CapturarSecuencial(ERP.Domain.Configuration.Entities.SriSettings config)
    {
        var s = config.CurrentSequential.ToString("D9");
        config.IncrementSequential();
        return s;
    }
}
