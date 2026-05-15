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

namespace ERP.Application.Modules.Purchasing.UseCases.Retenciones;

public sealed class GenerarCompraRetencionEmitidaCommandHandler
    : IRequestHandler<GenerarCompraRetencionEmitidaCommand, Result<Guid>>
{
    private readonly ICompraRepository                    _compraRepository;
    private readonly IConfiguracionSRIRepository        _configSriRepository;
    private readonly IConfiguracionRetencionRepository  _configRetencionRepository;
    private readonly IUserActivityRepository              _activity;
    private readonly ICurrentTenant                       _currentTenant;
    private readonly ICurrentUser                         _currentUser;
    private readonly IUnitOfWork                          _unitOfWork;
    private readonly ILogger<GenerarCompraRetencionEmitidaCommandHandler> _logger;

    public GenerarCompraRetencionEmitidaCommandHandler(
        ICompraRepository compraRepository,
        IConfiguracionSRIRepository configSriRepository,
        IConfiguracionRetencionRepository configRetencionRepository,
        IUserActivityRepository activity,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        ILogger<GenerarCompraRetencionEmitidaCommandHandler> logger)
    {
        _compraRepository           = compraRepository;
        _configSriRepository       = configSriRepository;
        _configRetencionRepository = configRetencionRepository;
        _activity                  = activity;
        _currentTenant             = currentTenant;
        _currentUser               = currentUser;
        _unitOfWork                = unitOfWork;
        _logger                    = logger;
    }

    public async Task<Result<Guid>> Handle(GenerarCompraRetencionEmitidaCommand command, CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;
        var userId   = _currentUser.UserId;

        var compra = await _compraRepository.GetByIdWithDetailsAsync(tenantId, command.CompraFacturaId, ct);
        if (compra is null)
            return Result<Guid>.Failure("Compra no encontrada.");
        if (compra.Estado != EstadoCompra.Aprobado)
            return Result<Guid>.Failure("Solo se puede generar retención para una compra Aprobada.");

        var configs = await _configRetencionRepository.GetActivosParaProveedorAsync(tenantId, ct);
        if (configs.Count == 0)
            return Result<Guid>.Failure(
                "No hay tasas de retención activas para proveedor. Configure en Configuración de retenciones.");

        var configSri = await _configSriRepository.GetByTenantIdAsync(tenantId, ct);
        if (configSri is null)
            return Result<Guid>.Failure("La configuración SRI no está configurada.");

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var secuencial = CapturarSecuencial(configSri);
            await _configSriRepository.UpdateAsync(configSri, ct);

            var fecha = DateTime.UtcNow;
            var clave = ClaveAccesoHelper.Generar(
                configSri.RucEmpresa, configSri.Ambiente, configSri.Establecimiento,
                configSri.PuntoEmision, configSri.TipoEmision, secuencial, fecha, "07");

            var ret = CompraRetencionEmitida.Create(
                tenantId, compra.ProveedorId, compra.Id, clave, fecha,
                configSri.Establecimiento, configSri.PuntoEmision, secuencial, userId);

            foreach (var cfg in configs)
            {
                decimal @base;
                if (cfg.Impuesto == "IVA")
                {
                    if (compra.IvaTotal <= 0) continue;
                    @base = compra.IvaTotal;
                }
                else if (cfg.Impuesto == "RENTA")
                {
                    @base = compra.Subtotal;
                }
                else
                    continue;

                var valor = CompraRetencionCalculo.CalcularValorRetenido(@base, cfg.Porcentaje);
                if (valor <= 0) continue;

                var det = CompraDetalleRetencionEmitida.Create(
                    tenantId, cfg.Impuesto, cfg.CodigoSri, @base, cfg.Porcentaje, valor,
                    compra.NumeroFactura, userId);
                det.AsignarRetencionId(ret.Id);
                ret.AgregarDetalle(det);
            }

            if (ret.Detalles.Count == 0)
            {
                await _unitOfWork.RollbackAsync(ct);
                return Result<Guid>.Failure("No se generaron líneas de retención (bases o porcentajes en cero).");
            }

            await _compraRepository.AddRetencionEmitidaAsync(ret, ct);
            await _activity.AddAsync(UserActivity.Create(
                tenantId, userId, _currentUser.Email, _currentUser.FullName,
                module: "compras", action: "compras.retencion.generar",
                entityType: "CompraRetencionEmitida", entityId: ret.Id,
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

    private static string CapturarSecuencial(ERP.Domain.Configuration.Entities.ConfiguracionSRI config)
    {
        var s = config.SecuencialActual.ToString("D9");
        config.IncrementarSecuencial();
        return s;
    }
}
