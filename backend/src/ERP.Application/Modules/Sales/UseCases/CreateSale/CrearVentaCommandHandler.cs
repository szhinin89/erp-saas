using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Application.Sales.Helpers;
using ERP.Domain.Common;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.MasterData.ValueObjects;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.Fiscal.Entities;
using ERP.Domain.Modules.Fiscal.Interfaces;
using ERP.Domain.Modules.Commercial.Entities;
using ERP.Domain.Modules.Commercial.Interfaces;
using ERP.Domain.Modules.Integration.Constants;
using ERP.Domain.Modules.Integration.Entities;
using ERP.Domain.Modules.Integration.Interfaces;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Sales.UseCases.CreateSale;

public sealed class CreateSaleCommandHandler : IRequestHandler<CreateSaleCommand, Result<Guid>>
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly ISalesOrderRepository _salesOrderRepository;
    private readonly IDocumentRelationRepository _documentRelationRepository;
    private readonly ISnowflakeIdGenerator _idGenerator;
    private readonly ISriSettingsRepository _configSriRepository;
    private readonly IEmissionPointRepository _emissionPointRepository;
    private readonly IDocumentSequenceRepository _docSeqRepository;
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
        IInvoiceRepository invoiceRepository,
        ISalesOrderRepository salesOrderRepository,
        IDocumentRelationRepository documentRelationRepository,
        ISnowflakeIdGenerator idGenerator,
        ISriSettingsRepository configSriRepository,
        IEmissionPointRepository emissionPointRepository,
        IDocumentSequenceRepository docSeqRepository,
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
        _invoiceRepository = invoiceRepository;
        _salesOrderRepository = salesOrderRepository;
        _documentRelationRepository = documentRelationRepository;
        _idGenerator = idGenerator;
        _configSriRepository = configSriRepository;
        _emissionPointRepository = emissionPointRepository;
        _docSeqRepository    = docSeqRepository;
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
            "Creando venta: tenant={SubscriberId}, bp={BusinessPartnerId}, Warehouse={BodegaId}, ítems={ItemCount}, salesOrder={SalesOrderPublicId}",
            subscriberId, command.BusinessPartnerId, command.WarehouseId, command.Items.Count, command.SalesOrderPublicId);

        var resolveResult = await ResolveCommandFromSalesOrderAsync(command, subscriberId, companyId, ct);
        if (!resolveResult.IsSuccess)
            return Result<Guid>.Failure(resolveResult.Error!);

        var effectiveCommand = resolveResult.Value;

        var preflight = await ValidateSalePreflightAsync(effectiveCommand, subscriberId, companyId, ct);
        if (!preflight.IsSuccess)
            return Result<Guid>.Failure(preflight.Error!);

        var productos = preflight.Value;

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            return await PersistSaleAsync(
                effectiveCommand, subscriberId, companyId, userId, productos, ct);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            _logger.LogError(ex, "Error al crear venta tenant={SubscriberId}", subscriberId);
            return Result<Guid>.Failure($"No se pudo crear la venta: {ex.Message}");
        }
    }

    private async Task<Result<CreateSaleCommand>> ResolveCommandFromSalesOrderAsync(
        CreateSaleCommand command,
        Guid subscriberId,
        Guid companyId,
        CancellationToken ct)
    {
        if (command.SalesOrderPublicId is null)
            return Result<CreateSaleCommand>.Success(command);

        var order = await _salesOrderRepository.GetByPublicIdReadOnlyAsync(command.SalesOrderPublicId.Value, ct);
        if (order is null)
            return Result<CreateSaleCommand>.Failure("Sales order not found.");

        if (order.SubscriberId != subscriberId)
            return Result<CreateSaleCommand>.Failure("Sales order not found.");

        if (order.BranchId != companyId)
            return Result<CreateSaleCommand>.Failure("Sales order does not belong to the active company.");

        var alreadyInvoiced = await _documentRelationRepository.ExistsSourceRelationAsync(
            subscriberId,
            DocumentModules.CommercialSalesOrder,
            order.Id,
            DocumentRelationTypes.OrderToInvoice,
            ct);

        if (alreadyInvoiced || order.Status == SalesOrder.Statuses.Invoiced)
            return Result<CreateSaleCommand>.Failure("Sales order was already invoiced.");

        if (order.Status is not (SalesOrder.Statuses.Confirmed or SalesOrder.Statuses.PartiallyInvoiced))
            return Result<CreateSaleCommand>.Failure(
                $"Sales order must be confirmed before invoicing (current: {order.Status}).");

        if (order.Lines.Count == 0)
            return Result<CreateSaleCommand>.Failure("Sales order has no lines.");

        if (command.BusinessPartnerId != Guid.Empty && command.BusinessPartnerId != order.BusinessPartnerId)
            return Result<CreateSaleCommand>.Failure("BusinessPartnerId does not match the sales order.");

        if (command.WarehouseId != Guid.Empty && command.WarehouseId != order.WarehouseId)
            return Result<CreateSaleCommand>.Failure("WarehouseId does not match the sales order.");

        var items = order.Lines
            .OrderBy(l => l.LineNo)
            .Select(l => new SaleItemDto(l.ProductId, l.Quantity, l.UnitPrice, DiscountAmount: 0))
            .ToList();

        var effectiveCommand = command with
        {
            BusinessPartnerId = order.BusinessPartnerId,
            WarehouseId = order.WarehouseId,
            BranchId = order.BranchId,
            Items = items,
            PaymentDays = command.PaymentDays > 0 ? command.PaymentDays : order.PaymentTermDays,
            Notes = string.IsNullOrWhiteSpace(command.Notes) ? order.Notes : command.Notes,
        };

        return Result<CreateSaleCommand>.Success(effectiveCommand);
    }

    private async Task<Result<Dictionary<Guid, ERP.Domain.Products.Entities.Product>>>
        ValidateSalePreflightAsync(
            CreateSaleCommand command,
            Guid subscriberId,
            Guid companyId,
            CancellationToken ct)
    {
        var cliente = await _bpRepository.GetByIdAsync(command.BusinessPartnerId, ct);
        if (cliente is null || !cliente.IsActive)
            return Result<Dictionary<Guid, ERP.Domain.Products.Entities.Product>>.Failure(
                "El cliente no existe o no está activo.");

        var warehouse = await _bodegaRepository.GetByIdAsync(subscriberId, command.WarehouseId, ct);
        if (warehouse is null || !warehouse.IsActive)
            return Result<Dictionary<Guid, ERP.Domain.Products.Entities.Product>>.Failure(
                "La Warehouse no existe o no está activa.");
        if (warehouse.CompanyId.HasValue && warehouse.CompanyId != companyId)
            return Result<Dictionary<Guid, ERP.Domain.Products.Entities.Product>>.Failure(
                "La bodega no pertenece a la empresa operativa activa.");

        var hasSriConfig = await _configSriRepository.GetByCompanyIdAsync(companyId, ct);
        if (hasSriConfig is null)
            return Result<Dictionary<Guid, ERP.Domain.Products.Entities.Product>>.Failure(
                "La configuración SRI no está configurada para esta empresa.");

        var productos = await LoadActiveProductsAsync(command, subscriberId, companyId, ct);
        if (!productos.IsSuccess)
            return Result<Dictionary<Guid, ERP.Domain.Products.Entities.Product>>.Failure(productos.Error!);

        var stockError = await ValidateStockAsync(command, productos.Value!, ct);
        if (stockError is not null)
            return Result<Dictionary<Guid, ERP.Domain.Products.Entities.Product>>.Failure(stockError);

        return Result<Dictionary<Guid, ERP.Domain.Products.Entities.Product>>.Success(productos.Value!);
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
        CancellationToken ct)
    {
        var cliente = await _bpRepository.GetByIdAsync(command.BusinessPartnerId, ct);
        if (cliente is null)
            return Result<Guid>.Failure("Cliente no encontrado.");

        var sriConfig = await _configSriRepository.GetByCompanyIdForUpdateAsync(companyId, ct)
            ?? throw new InvalidOperationException("SRI config no encontrada durante la transacción.");

        var emPoint = await _emissionPointRepository.GetDefaultForBranchAsync(subscriberId, command.BranchId, ct)
            ?? throw new InvalidOperationException($"No hay punto de emisión predeterminado para la sucursal {command.BranchId}.");

        var docSeq = await _docSeqRepository.GetForUpdateAsync(emPoint.Id, "01", ct)
            ?? throw new InvalidOperationException($"No hay secuencial configurado para Factura en el punto de emisión {emPoint.Id}.");

        var secuencial = docSeq.CaptureAndIncrement();

        var issueDate = DateTime.UtcNow;
        var accessKey = ClaveAccesoHelper.Generar(
            sriConfig.Ruc, sriConfig.Environment, emPoint.Establishment.Code,
            emPoint.Code, sriConfig.EmissionType, secuencial, issueDate);

        var publicId = Guid.NewGuid();
        var invoiceId = _idGenerator.NextId();
        var issueDateOnly = DateOnly.FromDateTime(issueDate);

        var invoice = Invoice.Create(
            invoiceId,
            publicId,
            subscriberId,
            companyId,
            command.BusinessPartnerId,
            command.WarehouseId,
            "01",
            emPoint.Establishment.Code,
            emPoint.Code,
            secuencial,
            accessKey,
            issueDate,
            MapBuyerIdType(cliente.Identification.Type),
            cliente.Identification.Number,
            cliente.LegalName,
            null,
            command.PaymentMethodCode,
            command.PaymentDays,
            command.Notes,
            userId);

        short lineNo = 1;
        foreach (var item in command.Items)
        {
            var producto = productos[item.ProductId];
            var brutoItem = item.Quantity * item.UnitPrice;
            var descItem = item.DiscountAmount < 0 ? 0 : item.DiscountAmount;
            var subtotalItem = brutoItem - descItem;

            decimal impuestoItem = 0;
            string vatCode = "0";
            decimal vatPct = 0m;

            if (producto.AppliesVatOnSale && producto.SaleTaxId.HasValue)
            {
                var taxRate = await _taxRateRepository.GetByIdAsync(producto.SaleTaxId.Value, subscriberId, ct);
                if (taxRate is not null)
                {
                    vatPct = taxRate.Percentage;
                    vatCode = SriVatCodeFromPercentage(vatPct);
                    impuestoItem = subtotalItem * vatPct / 100;
                }
            }

            invoice.AddLine(InvoiceDetail.Create(
                _idGenerator.NextId(),
                invoice.Id,
                subscriberId,
                companyId,
                lineNo++,
                item.ProductId,
                producto.ShortName,
                producto.SaleCode,
                "UND",
                producto.Description,
                item.Quantity,
                item.UnitPrice,
                item.DiscountAmount,
                vatCode,
                vatPct,
                impuestoItem,
                issueDateOnly));
        }

        var electronic = InvoiceElectronic.CreatePending(
            _idGenerator.NextId(),
            invoice.Id,
            subscriberId,
            companyId,
            issueDateOnly);
        invoice.AttachElectronic(electronic);
        invoice.AssignSnowflakeChildIds(_idGenerator);

        await _invoiceRepository.AddAsync(invoice, ct);

        var numeroFactura = invoice.InvoiceNumber;
        await _activity.AddAsync(UserActivity.Create(
            subscriberId: subscriberId,
            userId: userId,
            userEmail: _currentUser.Email,
            userFullName: _currentUser.FullName,
            module: "Ventas",
            action: "CreateSale",
            entityType: "Invoice",
            entityId: invoice.PublicId,
            description: $"Factura creada: {numeroFactura}"
        ), ct);

        await _unitOfWork.SaveChangesAsync(ct);
        await _unitOfWork.CommitAsync(ct);

        if (command.SalesOrderPublicId is not null)
        {
            _unitOfWork.ClearChangeTracker();

            try
            {
                var linkResult = await LinkInvoiceToSalesOrderAsync(
                    command.SalesOrderPublicId.Value,
                    invoice.Id,
                    subscriberId,
                    companyId,
                    userId,
                    ct);

                if (!linkResult.IsSuccess)
                {
                    _logger.LogWarning(
                        "Factura {PublicId} creada pero no se pudo vincular al pedido {SalesOrderPublicId}: {Error}",
                        invoice.PublicId,
                        command.SalesOrderPublicId,
                        linkResult.Error);
                    return Result<Guid>.Failure(linkResult.Error!);
                }
            }
            catch (Exception linkEx)
            {
                _logger.LogError(linkEx, "Link pedido→factura falló tras crear factura {PublicId}", invoice.PublicId);
                return Result<Guid>.Failure($"Invoice created but order link failed: {linkEx.Message}");
            }
        }

        _logger.LogInformation(
            "Venta creada: factura={PublicId}, secuencial={Secuencial}, total={Total}, tenant={SubscriberId}",
            invoice.PublicId, invoice.Sequential, invoice.Total, subscriberId);

        return Result<Guid>.Success(invoice.PublicId);
    }

    private async Task<Result<bool>> LinkInvoiceToSalesOrderAsync(
        Guid salesOrderPublicId,
        long invoiceId,
        Guid subscriberId,
        Guid companyId,
        Guid userId,
        CancellationToken ct)
    {
        var linkedOrder = await _salesOrderRepository.GetByPublicIdHeaderReadOnlyAsync(salesOrderPublicId, ct);
        if (linkedOrder is null)
            return Result<bool>.Failure("Sales order not found.");

        var alreadyInvoiced = await _documentRelationRepository.ExistsSourceRelationAsync(
            subscriberId,
            DocumentModules.CommercialSalesOrder,
            linkedOrder.Id,
            DocumentRelationTypes.OrderToInvoice,
            ct);

        if (alreadyInvoiced)
            return Result<bool>.Failure("Sales order was already invoiced.");

        var relation = DocumentRelation.Create(
            _idGenerator.NextId(),
            Guid.NewGuid(),
            subscriberId,
            companyId,
            DocumentModules.CommercialSalesOrder,
            linkedOrder.Id,
            DocumentModules.FiscalInvoice,
            invoiceId,
            DocumentRelationTypes.OrderToInvoice,
            userId);

        await _documentRelationRepository.AddAsync(relation, ct);
        await _documentRelationRepository.SaveChangesAsync(ct);

        await _salesOrderRepository.MarkInvoicedAsync(
            linkedOrder.Id,
            subscriberId,
            userId,
            _idGenerator,
            ct);
        await _salesOrderRepository.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }

    private static string MapBuyerIdType(string identificationType) => identificationType switch
    {
        TaxIdentification.TypeRuc => "04",
        TaxIdentification.TypeCi => "05",
        TaxIdentification.TypePassport => "06",
        _ => "07",
    };

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
