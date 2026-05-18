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

public sealed class CreateSaleCommandHandler : IRequestHandler<CreateSaleCommand, Result<Guid>>
{
    private readonly ISalesRepository _ventasRepository;
    private readonly ISriSettingsRepository _configSriRepository;
    private readonly IStockRepository _stockRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IWarehouseRepository _bodegaRepository;
    private readonly IProductRepository     _productRepository;
    private readonly ITaxRateRepository     _taxRateRepository;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentTenant          _currentTenant;
    private readonly ICurrentUser            _currentUser;
    private readonly IUnitOfWork             _unitOfWork;
    private readonly ILogger<CreateSaleCommandHandler> _logger;

    public CreateSaleCommandHandler(
        ISalesRepository ventasRepository,
        ISriSettingsRepository configSriRepository,
        IStockRepository stockRepository,
        ICustomerRepository customerRepository,
        IWarehouseRepository bodegaRepository,
        IProductRepository productRepository,
        ITaxRateRepository taxRateRepository,
        IUserActivityRepository activity,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        ILogger<CreateSaleCommandHandler> logger)
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
        CreateSaleCommand command,
        CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;
        var userId   = _currentUser.UserId;

        _logger.LogInformation(
            "Creando venta: tenant={TenantId}, cliente={ClienteId}, Warehouse={BodegaId}, ítems={ItemCount}",
            tenantId, command.CustomerId, command.WarehouseId, command.Items.Count);

        // 1. Validar cliente existe y está activo
        var cliente = await _customerRepository.GetByIdAsync(tenantId, command.CustomerId, ct);
        if (cliente is null || !cliente.IsActive)
            return Result<Guid>.Failure("El cliente no existe o no está activo.");

        // 2. Validar Warehouse existe y está activa
        var Warehouse = await _bodegaRepository.GetByIdAsync(tenantId, command.WarehouseId, ct);
        if (Warehouse is null || !Warehouse.IsActive)
            return Result<Guid>.Failure("La Warehouse no existe o no está activa.");

        // 3. Validar productos existen y están activos (de-duplicar por ProductoId)
        var productos = new Dictionary<Guid, ERP.Domain.Products.Entities.Product>();
        foreach (var item in command.Items)
        {
            if (productos.ContainsKey(item.ProductId)) continue;
            var producto = await _productRepository.GetByIdAsync(item.ProductId, tenantId, ct);
            if (producto is null || !producto.IsActive)
                return Result<Guid>.Failure($"El producto con ID {item.ProductId} no existe o no está activo.");
            productos[item.ProductId] = producto;
        }

        // 4. Validar stock suficiente para cada ítem (sólo productos físicos con control de stock)
        foreach (var item in command.Items)
        {
            var producto = productos[item.ProductId];
            if (producto.IsService || !producto.TracksStock) continue;

            var stock = await _stockRepository.GetStockAsync(
                tenantId, command.WarehouseId, item.ProductId, ct);

            if (stock is null || stock.AvailableQuantity < item.Quantity)
            {
                _logger.LogWarning(
                    "Stock insuficiente: producto={ProductoId} ({Nombre}), Warehouse={BodegaId}, disponible={Disponible}, solicitado={Solicitado}",
                    item.ProductId, producto.ShortName, command.WarehouseId,
                    stock?.AvailableQuantity ?? 0, item.Quantity);
                return Result<Guid>.Failure(
                    $"Stock insuficiente para '{producto.ShortName}'. " +
                    $"Disponible: {stock?.AvailableQuantity ?? 0}, Solicitado: {item.Quantity}");
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

            var issueDate = DateTime.UtcNow;
            var accessKey = ClaveAccesoHelper.Generar(
                configSri.Ruc, configSri.Environment, configSri.EstabCode,
                configSri.EmPointCode, configSri.EmissionType, secuencial, issueDate);

            // 7. Calcular totales y construir detalles
            decimal subtotal = 0;
            decimal totalVat = 0;
            var detalles = new List<SalesBillLine>();

            foreach (var item in command.Items)
            {
                var producto = productos[item.ProductId];
                var subtotalItem = item.Quantity * item.UnitPrice;

                decimal impuestoItem = 0;
                if (producto.AppliesVatOnSale && producto.SaleTaxId.HasValue)
                {
                    var taxRate = await _taxRateRepository.GetByIdAsync(producto.SaleTaxId.Value, tenantId, ct);
                    if (taxRate is not null)
                        impuestoItem = subtotalItem * taxRate.Percentage / 100;
                }

                subtotal += subtotalItem;
                totalVat += impuestoItem;

                var detalle = SalesBillLine.Create(
                    tenantId:    tenantId,
                    productId:   item.ProductId,
                    productCode: producto.SaleCode,
                    quantity:    item.Quantity,
                    unitPrice:   item.UnitPrice,
                    vatTotal:    impuestoItem,
                    description: producto.Description,
                    createdBy:   userId
                );
                detalles.Add(detalle);
            }

            var total = subtotal + totalVat;

            // 8. Crear factura en estado Borrador
            var factura = SalesBill.Create(
                tenantId:      tenantId,
                branchId:      command.BranchId,
                customerId:    command.CustomerId,
                warehouseId:   command.WarehouseId,
                docType:       "01",
                estabCode:     configSri.EstabCode,
                emPointCode:   configSri.EmPointCode,
                sequential:    secuencial,
                accessKey:     accessKey,
                issueDate:     issueDate,
                subtotal:      subtotal,
                vatTotal:      totalVat,
                total:         total,
                xmlSignedPath: null,
                xmlAuthPath:   null,
                authNumber: null,
                authDate: null,
                errorMessage:  null,
                createdBy:     userId
            );

            foreach (var detalle in detalles)
            {
                detalle.AssignBillId(factura.Id);
                factura.AddLine(detalle);
            }

            await _ventasRepository.AddBillAsync(factura, ct);

            var numeroFactura = $"{factura.EstabCode}-{factura.EmPointCode}-{factura.Sequential}";
            await _activity.AddAsync(UserActivity.Create(
                tenantId: tenantId,
                userId: userId,
                userEmail: _currentUser.Email,
                userFullName: _currentUser.FullName,
                module: "Ventas",
                action: "CrearVenta",
                entityType: "SalesBill",
                entityId: factura.Id,
                description: $"Factura creada: {numeroFactura}"
            ), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation(
                "Venta creada: factura={FacturaId}, secuencial={Secuencial}, total={Total}, tenant={TenantId}",
                factura.Id, factura.Sequential, factura.Total, tenantId);

            return Result<Guid>.Success(factura.Id);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            _logger.LogError(ex, "Error al crear venta tenant={TenantId}", tenantId);
            return Result<Guid>.Failure($"No se pudo crear la venta: {ex.Message}");
        }
    }

    private static string CapturarSecuencialComoString(SriSettings config)
    {
        var secuencial = config.CurrentSequential.ToString("D9");
        config.IncrementSequential();
        return secuencial;
    }

}
