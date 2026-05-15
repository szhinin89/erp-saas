using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Application.Sales.Helpers;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Interfaces;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Sales.UseCases.Notas;

public sealed class CrearVentasNotaCreditoDebitoCommandHandler
    : IRequestHandler<CrearVentasNotaCreditoDebitoCommand, Result<Guid>>
{
    private readonly IVentasRepository           _ventasRepository;
    private readonly IConfiguracionSRIRepository _configSriRepository;
    private readonly IProductRepository          _productRepository;
    private readonly ITaxRateRepository          _taxRateRepository;
    private readonly IUserActivityRepository     _activity;
    private readonly ICurrentTenant              _currentTenant;
    private readonly ICurrentUser                _currentUser;
    private readonly IUnitOfWork                 _unitOfWork;
    private readonly ILogger<CrearVentasNotaCreditoDebitoCommandHandler> _logger;

    public CrearVentasNotaCreditoDebitoCommandHandler(
        IVentasRepository ventasRepository,
        IConfiguracionSRIRepository configSriRepository,
        IProductRepository productRepository,
        ITaxRateRepository taxRateRepository,
        IUserActivityRepository activity,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        ILogger<CrearVentasNotaCreditoDebitoCommandHandler> logger)
    {
        _ventasRepository    = ventasRepository;
        _configSriRepository = configSriRepository;
        _productRepository   = productRepository;
        _taxRateRepository   = taxRateRepository;
        _activity            = activity;
        _currentTenant       = currentTenant;
        _currentUser         = currentUser;
        _unitOfWork          = unitOfWork;
        _logger              = logger;
    }

    public async Task<Result<Guid>> Handle(CrearVentasNotaCreditoDebitoCommand command, CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;
        var userId   = _currentUser.UserId;

        if (command.Items.Count == 0)
            return Result<Guid>.Failure("La nota debe tener al menos un detalle.");

        var factura = await _ventasRepository.GetFacturaByIdAsync(tenantId, command.FacturaOriginalId, ct);
        if (factura is null)
            return Result<Guid>.Failure("Factura original no encontrada.");
        if (factura.Estado != "Autorizado")
            return Result<Guid>.Failure(
                $"La factura original debe estar Autorizada (estado actual: {factura.Estado}).");

        var configSri = await _configSriRepository.GetByTenantIdAsync(tenantId, ct);
        if (configSri is null)
            return Result<Guid>.Failure("La configuración SRI no está configurada para este tenant.");

        var productos = new Dictionary<Guid, ERP.Domain.Products.Entities.Product>();
        foreach (var item in command.Items)
        {
            if (productos.ContainsKey(item.ProductoId)) continue;
            var p = await _productRepository.GetByIdAsync(item.ProductoId, tenantId, ct);
            if (p is null || !p.IsActive)
                return Result<Guid>.Failure($"Producto {item.ProductoId} no existe o no está activo.");
            productos[item.ProductoId] = p;
        }

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var secuencial = CapturarSecuencialComoString(configSri);
            await _configSriRepository.UpdateAsync(configSri, ct);

            var fechaEmision = DateTime.UtcNow;
            var tipoDoc = string.Equals(command.TipoNota, "DEBITO", StringComparison.OrdinalIgnoreCase) ? "05" : "04";
            var claveAcceso = ClaveAccesoHelper.Generar(
                configSri.RucEmpresa, configSri.Ambiente, configSri.Establecimiento,
                configSri.PuntoEmision, configSri.TipoEmision, secuencial, fechaEmision, tipoDoc);

            var detalles = new List<VentasNotaDetalle>();
            foreach (var item in command.Items)
            {
                var producto = productos[item.ProductoId];
                var subtotalItem = item.Cantidad * item.PrecioUnitario;
                decimal impuestoItem = 0;
                if (producto.AppliesVatOnSale && producto.SaleTaxId.HasValue)
                {
                    var taxRate = await _taxRateRepository.GetByIdAsync(producto.SaleTaxId.Value, tenantId, ct);
                    if (taxRate is not null)
                        impuestoItem = subtotalItem * taxRate.Percentage / 100;
                }

                var det = VentasNotaDetalle.Create(
                    tenantId, item.ProductoId, item.Cantidad, item.PrecioUnitario, impuestoItem,
                    producto.Description, userId);
                detalles.Add(det);
            }

            var nota = VentasNotaCreditoDebito.Create(
                tenantId,
                factura.Id,
                command.TipoNota,
                command.Motivo,
                tipoDoc,
                configSri.Establecimiento,
                configSri.PuntoEmision,
                secuencial,
                claveAcceso,
                fechaEmision,
                userId);

            foreach (var d in detalles)
            {
                d.AsignarNotaId(nota.Id);
                nota.AgregarDetalle(d);
            }

            await _ventasRepository.AddNotaCreditoDebitoAsync(nota, ct);

            await _activity.AddAsync(UserActivity.Create(
                tenantId, userId, _currentUser.Email, _currentUser.FullName,
                module: "ventas", action: "ventas.nota.crear",
                entityType: "VentasNotaCreditoDebito", entityId: nota.Id,
                description: $"{nota.TipoNota} {nota.Establecimiento}-{nota.PuntoEmision}-{nota.Secuencial}"), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation("Nota {NotaId} creada en Borrador para factura {FacturaId}", nota.Id, factura.Id);
            return Result<Guid>.Success(nota.Id);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            _logger.LogError(ex, "Error al crear nota de crédito/débito");
            return Result<Guid>.Failure($"No se pudo crear la nota: {ex.Message}");
        }
    }

    private static string CapturarSecuencialComoString(ERP.Domain.Configuration.Entities.ConfiguracionSRI config)
    {
        var secuencial = config.SecuencialActual.ToString("D9");
        config.IncrementarSecuencial();
        return secuencial;
    }
}
