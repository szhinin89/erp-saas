using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Application.Sales.Helpers;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Sales.Interfaces;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Products.Interfaces;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Interfaces;

namespace ERP.Application.Sales.UseCases.CrearVenta;

public sealed class CrearVentaCommandHandler : IRequestHandler<CrearVentaCommand, Result<Guid>>
{
    private readonly IVentasRepository _ventasRepository;
    private readonly IConfiguracionSRIRepository _configSriRepository;
    private readonly IInventarioStockRepository _stockRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IBodegaRepository _bodegaRepository;
    private readonly IProductRepository     _productRepository;
    private readonly ITaxRateRepository     _taxRateRepository;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentTenant          _currentTenant;
    private readonly ICurrentUser            _currentUser;
    private readonly IUnitOfWork             _unitOfWork;
    private readonly ILogger<CrearVentaCommandHandler> _logger;

    public CrearVentaCommandHandler(
        IVentasRepository ventasRepository,
        IConfiguracionSRIRepository configSriRepository,
        IInventarioStockRepository stockRepository,
        ICustomerRepository customerRepository,
        IBodegaRepository bodegaRepository,
        IProductRepository productRepository,
        ITaxRateRepository taxRateRepository,
        IUserActivityRepository activity,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        ILogger<CrearVentaCommandHandler> logger)
    {
        _ventasRepository    = ventasRepository;
        _configSriRepository = configSriRepository;
        _stockRepository     = stockRepository;
        _customerRepository  = customerRepository;
        _bodegaRepository    = bodegaRepository;
        _productRepository   = productRepository;
        _taxRateRepository   = taxRateRepository;
        _activity            = activity;
        _currentTenant       = currentTenant;
        _currentUser         = currentUser;
        _unitOfWork          = unitOfWork;
        _logger              = logger;
    }

    public async Task<Result<Guid>> Handle(
        CrearVentaCommand command,
        CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;
        var userId   = _currentUser.UserId;

        _logger.LogInformation(
            "Creando venta: tenant={TenantId}, cliente={ClienteId}, bodega={BodegaId}, ítems={ItemCount}",
            tenantId, command.ClienteId, command.BodegaId, command.Items.Count);

        // 1. Validar cliente existe y está activo
        var cliente = await _customerRepository.GetByIdAsync(tenantId, command.ClienteId, ct);
        if (cliente is null || !cliente.IsActive)
            return Result<Guid>.Failure("El cliente no existe o no está activo.");

        // 2. Validar bodega existe y está activa
        var bodega = await _bodegaRepository.GetByIdAsync(tenantId, command.BodegaId, ct);
        if (bodega is null || !bodega.IsActive)
            return Result<Guid>.Failure("La bodega no existe o no está activa.");

        // 3. Validar productos existen y están activos (de-duplicar por ProductoId)
        var productos = new Dictionary<Guid, ERP.Domain.Products.Entities.Product>();
        foreach (var item in command.Items)
        {
            if (productos.ContainsKey(item.ProductoId)) continue;
            var producto = await _productRepository.GetByIdAsync(item.ProductoId, tenantId, ct);
            if (producto is null || !producto.IsActive)
                return Result<Guid>.Failure($"El producto con ID {item.ProductoId} no existe o no está activo.");
            productos[item.ProductoId] = producto;
        }

        // 4. Validar stock suficiente para cada ítem (sólo productos físicos con control de stock)
        foreach (var item in command.Items)
        {
            var producto = productos[item.ProductoId];
            if (producto.IsService || !producto.TracksStock) continue;

            var stock = await _stockRepository.GetStockByTenantBodegaProductAsync(
                tenantId, command.BodegaId, item.ProductoId, ct);

            if (stock is null || stock.CantidadDisponible < item.Cantidad)
            {
                _logger.LogWarning(
                    "Stock insuficiente: producto={ProductoId} ({Nombre}), bodega={BodegaId}, disponible={Disponible}, solicitado={Solicitado}",
                    item.ProductoId, producto.ShortName, command.BodegaId,
                    stock?.CantidadDisponible ?? 0, item.Cantidad);
                return Result<Guid>.Failure(
                    $"Stock insuficiente para '{producto.ShortName}'. " +
                    $"Disponible: {stock?.CantidadDisponible ?? 0}, Solicitado: {item.Cantidad}");
            }
        }

        // 5. Obtener configuración SRI
        var configSri = await _configSriRepository.GetByTenantIdAsync(tenantId, ct);
        if (configSri is null)
            return Result<Guid>.Failure("La configuración SRI no está configurada para este tenant.");

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            // 6. Secuencial SRI (incremento + factura + actividad en una misma transacción)
            var secuencial = CapturarSecuencialComoString(configSri);
            await _configSriRepository.UpdateAsync(configSri, ct);

            var fechaEmision = DateTime.UtcNow;
            var claveAcceso = ClaveAccesoHelper.Generar(
                configSri.RucEmpresa, configSri.Ambiente, configSri.Establecimiento,
                configSri.PuntoEmision, configSri.TipoEmision, secuencial, fechaEmision);

            // 7. Calcular totales y construir detalles
            decimal subtotal = 0;
            decimal totalImpuesto = 0;
            var detalles = new List<VentasDetalle>();

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

                subtotal += subtotalItem;
                totalImpuesto += impuestoItem;

                var detalle = VentasDetalle.Create(
                    tenantId: tenantId,
                    productoId: item.ProductoId,
                    cantidad: item.Cantidad,
                    precioUnitario: item.PrecioUnitario,
                    impuesto: impuestoItem,
                    descripcion: producto.Description,
                    createdBy: userId
                );
                detalles.Add(detalle);
            }

            var total = subtotal + totalImpuesto;

            // 8. Crear factura en estado Borrador
            var factura = VentasFactura.Create(
                tenantId: tenantId,
                sucursalId: command.SucursalId,
                clienteId: command.ClienteId,
                bodegaId: command.BodegaId,
                tipoDocumento: "01",
                establecimiento: configSri.Establecimiento,
                puntoEmision: configSri.PuntoEmision,
                secuencial: secuencial,
                claveAcceso: claveAcceso,
                fechaEmision: fechaEmision,
                subtotal: subtotal,
                impuesto: totalImpuesto,
                total: total,
                xmlGeneradoPath: null,
                xmlAutorizacionPath: null,
                numeroAutorizacion: null,
                fechaAutorizacion: null,
                mensajeError: null,
                createdBy: userId
            );

            foreach (var detalle in detalles)
            {
                detalle.AsignarFacturaId(factura.Id);
                factura.AgregarDetalle(detalle);
            }

            await _ventasRepository.AddFacturaAsync(factura, ct);

            var numeroFactura = $"{factura.Establecimiento}-{factura.PuntoEmision}-{factura.Secuencial}";
            await _activity.AddAsync(UserActivity.Create(
                tenantId: tenantId,
                userId: userId,
                userEmail: _currentUser.Email,
                userFullName: _currentUser.FullName,
                module: "Ventas",
                action: "CrearVenta",
                entityType: "VentasFactura",
                entityId: factura.Id,
                description: $"Factura creada: {numeroFactura}"
            ), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation(
                "Venta creada: factura={FacturaId}, secuencial={Secuencial}, total={Total}, tenant={TenantId}",
                factura.Id, factura.Secuencial, factura.Total, tenantId);

            return Result<Guid>.Success(factura.Id);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            _logger.LogError(ex, "Error al crear venta tenant={TenantId}", tenantId);
            return Result<Guid>.Failure($"No se pudo crear la venta: {ex.Message}");
        }
    }

    private static string CapturarSecuencialComoString(ConfiguracionSRI config)
    {
        var secuencial = config.SecuencialActual.ToString("D9");
        config.IncrementarSecuencial();
        return secuencial;
    }

}
