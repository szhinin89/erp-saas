using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Application.Sales.Helpers;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Sales.Interfaces;
using ERP.Domain.Products.Interfaces;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Interfaces;

namespace ERP.Application.Sales.UseCases.CrearVenta;

public sealed class CreateSaleCommandHandler : IRequestHandler<CreateSaleCommand, Result<Guid>>
{
    private readonly ISalesRepository _ventasRepository;
    private readonly ISriSettingsRepository _configSriRepository;
    private readonly IStockRepository _stockRepository;
    private readonly IBusinessPartnerRepository _bpRepository;
    private readonly IWarehouseRepository _bodegaRepository;
    private readonly IProductRepository     _productRepository;
    private readonly ITaxRateRepository     _taxRateRepository;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentSubscriber          _currentSubscriber;
    private readonly ICurrentCompany           _currentCompany;
    private readonly ICurrentUser            _currentUser;
    private readonly IUnitOfWork             _unitOfWork;
    private readonly ILogger<CreateSaleCommandHandler> _logger;

    public CreateSaleCommandHandler(
        ISalesRepository ventasRepository,
        ISriSettingsRepository configSriRepository,
        IStockRepository stockRepository,
        IBusinessPartnerRepository bpRepository,
        IWarehouseRepository bodegaRepository,
        IProductRepository productRepository,
        ITaxRateRepository taxRateRepository,
        IUserActivityRepository activity,
        ICurrentSubscriber currentSubscriber,
        ICurrentCompany currentCompany,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        ILogger<CreateSaleCommandHandler> logger)
    {
        _ventasRepository    = ventasRepository;
        _configSriRepository = configSriRepository;
        _stockRepository     = stockRepository;
        _bpRepository        = bpRepository;
        _bodegaRepository    = bodegaRepository;
        _productRepository   = productRepository;
        _taxRateRepository   = taxRateRepository;
        _activity            = activity;
        _currentSubscriber       = currentSubscriber;
        _currentCompany        = currentCompany;
        _currentUser         = currentUser;
        _unitOfWork          = unitOfWork;
        _logger              = logger;
    }

    public async Task<Result<Guid>> Handle(
        CreateSaleCommand command,
        CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        var userId   = _currentUser.UserId;
        if (!_currentCompany.HasCompanyContext)
            return Result<Guid>.Failure("No hay empresa operativa seleccionada.");
        var companyId = _currentCompany.CompanyId;

        _logger.LogInformation(
            "Creando venta: tenant={SubscriberId}, bp={BusinessPartnerId}, Warehouse={BodegaId}, ítems={ItemCount}",
            subscriberId, command.BusinessPartnerId, command.WarehouseId, command.Items.Count);

        var preflight = await ValidateSalePreflightAsync(command, subscriberId, companyId, ct);
        if (!preflight.IsSuccess)
            return Result<Guid>.Failure(preflight.Error!);

        var (productos, configSri) = preflight.Value;

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            return await PersistSaleAsync(
                command, subscriberId, companyId, userId, productos, configSri, ct);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            _logger.LogError(ex, "Error al crear venta tenant={SubscriberId}", subscriberId);
            return Result<Guid>.Failure($"No se pudo crear la venta: {ex.Message}");
        }
    }

    private async Task<Result<(Dictionary<Guid, ERP.Domain.Products.Entities.Product> Productos, SriSettings ConfigSri)>>
        ValidateSalePreflightAsync(
            CreateSaleCommand command,
            Guid subscriberId,
            Guid companyId,
            CancellationToken ct)
    {
        var cliente = await _bpRepository.GetByIdAsync(command.BusinessPartnerId, ct);
        if (cliente is null || !cliente.IsActive)
            return Result<(Dictionary<Guid, ERP.Domain.Products.Entities.Product>, SriSettings)>.Failure(
                "El cliente no existe o no está activo.");

        var warehouse = await _bodegaRepository.GetByIdAsync(subscriberId, command.WarehouseId, ct);
        if (warehouse is null || !warehouse.IsActive)
            return Result<(Dictionary<Guid, ERP.Domain.Products.Entities.Product>, SriSettings)>.Failure(
                "La Warehouse no existe o no está activa.");
        if (warehouse.CompanyId.HasValue && warehouse.CompanyId != companyId)
            return Result<(Dictionary<Guid, ERP.Domain.Products.Entities.Product>, SriSettings)>.Failure(
                "La bodega no pertenece a la empresa operativa activa.");

        var productos = await LoadActiveProductsAsync(command, subscriberId, companyId, ct);
        if (!productos.IsSuccess)
            return Result<(Dictionary<Guid, ERP.Domain.Products.Entities.Product>, SriSettings)>.Failure(productos.Error!);

        var stockError = await ValidateStockAsync(command, productos.Value!, ct);
        if (stockError is not null)
            return Result<(Dictionary<Guid, ERP.Domain.Products.Entities.Product>, SriSettings)>.Failure(stockError);

        var configSri = await _configSriRepository.GetBySubscriberIdAsync(subscriberId, ct);
        if (configSri is null)
            return Result<(Dictionary<Guid, ERP.Domain.Products.Entities.Product>, SriSettings)>.Failure(
                "La configuración SRI no está configurada para este tenant.");

        return Result<(Dictionary<Guid, ERP.Domain.Products.Entities.Product>, SriSettings)>.Success(
            (productos.Value!, configSri));
    }

    private async Task<Result<Dictionary<Guid, ERP.Domain.Products.Entities.Product>>> LoadActiveProductsAsync(
        CreateSaleCommand command,
        Guid subscriberId,
        Guid companyId,
        CancellationToken ct)
    {
        var productos = new Dictionary<Guid, ERP.Domain.Products.Entities.Product>();
        foreach (var item in command.Items)
        {
            if (productos.ContainsKey(item.ProductId))
                continue;
            var producto = await _productRepository.GetByIdAsync(item.ProductId, subscriberId, ct);
            if (producto is null || !producto.IsActive)
                return Result<Dictionary<Guid, ERP.Domain.Products.Entities.Product>>.Failure(
                    $"El producto con ID {item.ProductId} no existe o no está activo.");
            if (producto.CompanyId.HasValue && producto.CompanyId != companyId)
                return Result<Dictionary<Guid, ERP.Domain.Products.Entities.Product>>.Failure(
                    $"El producto '{producto.ShortName}' no pertenece a la empresa operativa activa.");
            productos[item.ProductId] = producto;
        }
        return Result<Dictionary<Guid, ERP.Domain.Products.Entities.Product>>.Success(productos);
    }

    private async Task<string?> ValidateStockAsync(
        CreateSaleCommand command,
        Dictionary<Guid, ERP.Domain.Products.Entities.Product> productos,
        CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        foreach (var item in command.Items)
        {
            var producto = productos[item.ProductId];
            if (producto.IsService || !producto.TracksStock)
                continue;

            var stock = await _stockRepository.GetStockAsync(
                subscriberId, command.WarehouseId, item.ProductId, ct);

            if (stock is null || stock.AvailableQuantity < item.Quantity)
            {
                _logger.LogWarning(
                    "Stock insuficiente: producto={ProductoId} ({Nombre}), Warehouse={BodegaId}, disponible={Disponible}, solicitado={Solicitado}",
                    item.ProductId, producto.ShortName, command.WarehouseId,
                    stock?.AvailableQuantity ?? 0, item.Quantity);
                return
                    $"Stock insuficiente para '{producto.ShortName}'. " +
                    $"Disponible: {stock?.AvailableQuantity ?? 0}, Solicitado: {item.Quantity}";
            }
        }
        return null;
    }

    private async Task<Result<Guid>> PersistSaleAsync(
        CreateSaleCommand command,
        Guid subscriberId,
        Guid companyId,
        Guid userId,
        Dictionary<Guid, ERP.Domain.Products.Entities.Product> productos,
        SriSettings configSri,
        CancellationToken ct)
    {
        var secuencial = CapturarSecuencialComoString(configSri);
        await _configSriRepository.UpdateAsync(configSri, ct);

        var issueDate = DateTime.UtcNow;
        var accessKey = ClaveAccesoHelper.Generar(
            configSri.Ruc, configSri.Environment, configSri.EstabCode,
            configSri.EmPointCode, configSri.EmissionType, secuencial, issueDate);

        var (detalles, subtotal, totalVat) = await BuildBillLinesAsync(
            command, productos, subscriberId, userId, ct);

        var factura = CreateBill(
            command, subscriberId, companyId, userId, configSri,
            secuencial, accessKey, issueDate, detalles, subtotal, totalVat);

        await _ventasRepository.AddBillAsync(factura, ct);

        var numeroFactura = $"{factura.EstabCode}-{factura.EmPointCode}-{factura.Sequential}";
        await _activity.AddAsync(UserActivity.Create(
            subscriberId: subscriberId,
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
            "Venta creada: factura={FacturaId}, secuencial={Secuencial}, total={Total}, tenant={SubscriberId}",
            factura.Id, factura.Sequential, factura.Total, subscriberId);

        return Result<Guid>.Success(factura.Id);
    }

    private async Task<(List<SalesBillLine> Detalles, decimal Subtotal, decimal TotalVat)> BuildBillLinesAsync(
        CreateSaleCommand command,
        Dictionary<Guid, ERP.Domain.Products.Entities.Product> productos,
        Guid subscriberId,
        Guid userId,
        CancellationToken ct)
    {
        decimal subtotal = 0;
        decimal totalVat = 0;
        var detalles = new List<SalesBillLine>();

        foreach (var item in command.Items)
        {
            var producto      = productos[item.ProductId];
            var brutoItem     = item.Quantity * item.UnitPrice;
            var descItem      = item.DiscountAmount < 0 ? 0 : item.DiscountAmount;
            var subtotalItem  = brutoItem - descItem;

            decimal impuestoItem = 0;
            string  vatCode      = "0";
            decimal vatPct       = 0m;

            if (producto.AppliesVatOnSale && producto.SaleTaxId.HasValue)
            {
                var taxRate = await _taxRateRepository.GetByIdAsync(producto.SaleTaxId.Value, subscriberId, ct);
                if (taxRate is not null)
                {
                    vatPct       = taxRate.Percentage;
                    vatCode      = SriVatCodeFromPercentage(vatPct);
                    impuestoItem = subtotalItem * vatPct / 100;
                }
            }

            subtotal += subtotalItem;
            totalVat += impuestoItem;

            detalles.Add(SalesBillLine.Create(
                subscriberId:       subscriberId,
                productId:      item.ProductId,
                productCode:    producto.SaleCode,
                quantity:       item.Quantity,
                unitPrice:      item.UnitPrice,
                discountAmount: item.DiscountAmount,
                vatCode:        vatCode,
                vatPercentage:  vatPct,
                vatTotal:       impuestoItem,
                description:    producto.Description,
                createdBy:      userId
            ));
        }

        return (detalles, subtotal, totalVat);
    }

    private static SalesBill CreateBill(
        CreateSaleCommand command,
        Guid subscriberId,
        Guid companyId,
        Guid userId,
        SriSettings configSri,
        string secuencial,
        string accessKey,
        DateTime issueDate,
        List<SalesBillLine> detalles,
        decimal subtotal,
        decimal totalVat)
    {
        var total = subtotal + totalVat;
        var totalDiscount = detalles.Sum(d => d.DiscountAmount);
        var factura = SalesBill.Create(
            subscriberId:          subscriberId,
            branchId:              command.BranchId,
            businessPartnerId:     command.BusinessPartnerId,
            warehouseId:       command.WarehouseId,
            docType:           "01",
            estabCode:         configSri.EstabCode,
            emPointCode:       configSri.EmPointCode,
            sequential:        secuencial,
            accessKey:         accessKey,
            issueDate:         issueDate,
            subtotal:          subtotal,
            vatTotal:          totalVat,
            total:             total,
            totalDiscount:     totalDiscount,
            paymentMethodCode: command.PaymentMethodCode,
            paymentDays:       command.PaymentDays,
            notes:             command.Notes,
            xmlSignedPath:     null,
            xmlAuthPath:       null,
            authNumber:        null,
            authDate:          null,
            errorMessage:      null,
            createdBy:         userId,
            companyId:         companyId
        );

        foreach (var detalle in detalles)
        {
            detalle.AssignBillId(factura.Id);
            factura.AddLine(detalle);
        }

        return factura;
    }

    private static string CapturarSecuencialComoString(SriSettings config)
    {
        var secuencial = config.CurrentSequential.ToString("D9");
        config.IncrementSequential();
        return secuencial;
    }

    /// <summary>
    /// Convierte un porcentaje de IVA al código SRI correspondiente.
    /// Códigos oficiales: "0"=0%, "2"=12%, "3"=14%, "4"=15%, "5"=5%.
    /// </summary>
    private static string SriVatCodeFromPercentage(decimal percentage) => percentage switch
    {
        0m    => "0",
        5m    => "5",
        12m   => "2",
        14m   => "3",
        15m   => "4",
        _     => "4"   // default 15%
    };

}
