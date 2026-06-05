using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Purchasing.Entities;
using ERP.Domain.Modules.Purchasing.Interfaces;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Modules.Purchasing.UseCases.CreatePurchaseOrder;

public sealed class CreatePurchaseOrderCommandHandler
    : IRequestHandler<CreatePurchaseOrderCommand, Result<PurchaseOrderDto>>
{
    private readonly IPurchaseOrderRepository  _ordenRepo;
    private readonly IBusinessPartnerRepository _bpRepo;
    private readonly IProductRepository      _productRepo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentSubscriber          _currentSubscriber;
    private readonly ICurrentUser            _currentUser;
    private readonly ILogger<CreatePurchaseOrderCommandHandler> _logger;

    public CreatePurchaseOrderCommandHandler(
        IPurchaseOrderRepository ordenRepo,
        IBusinessPartnerRepository bpRepo,
        IProductRepository productRepo,
        IUserActivityRepository activity,
        ICurrentSubscriber currentSubscriber,
        ICurrentUser currentUser,
        ILogger<CreatePurchaseOrderCommandHandler> logger)
    {
        _ordenRepo     = ordenRepo;
        _bpRepo = bpRepo;
        _productRepo   = productRepo;
        _activity      = activity;
        _currentSubscriber = currentSubscriber;
        _currentUser   = currentUser;
        _logger        = logger;
    }

    public async Task<Result<PurchaseOrderDto>> Handle(
        CreatePurchaseOrderCommand command, CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        var userId   = _currentUser.UserId;

        var bp = await _bpRepo.GetByIdAsync(command.BusinessPartnerId, ct);
        if (bp is null || !bp.IsActive)
            return Result<PurchaseOrderDto>.Failure("El Supplier no existe o no estÃ¡ activo.");

        // Validar productos y construir lines
        var productosValidados = new Dictionary<Guid, ERP.Domain.Products.Entities.Product>();
        foreach (var item in command.Items)
        {
            if (productosValidados.ContainsKey(item.ProductId)) continue;
            var producto = await _productRepo.GetByIdAsync(item.ProductId, subscriberId, ct);
            if (producto is null || !producto.IsActive)
                return Result<PurchaseOrderDto>.Failure(
                    $"El producto {item.ProductId} no existe o no estÃ¡ activo.");
            productosValidados[item.ProductId] = producto;
        }

        var secuencial = await _ordenRepo.GetNextSequentialAsync(subscriberId, ct);

        var orden = PurchaseOrder.Create(
            subscriberId, secuencial,
            command.BusinessPartnerId, command.RequiredDate,
            command.TargetWarehouseId, command.DeliveryAddress, command.Notes,
            userId);

        foreach (var item in command.Items)
        {
            var producto = productosValidados[item.ProductId];
            var line  = PurchaseOrderLine.Create(
                subscriberId, orden.Id, item.ProductId,
                producto.ShortName,
                item.Quantity, item.UnitPrice, item.VatPct,
                userId);
            orden.AddLine(line);
        }

        await _ordenRepo.AddAsync(orden, ct);
        await _ordenRepo.SaveChangesAsync(ct);

        await _activity.AddAsync(UserActivity.Create(
            subscriberId, userId, _currentUser.Email, _currentUser.FullName,
            module: "compras", action: "orden-compra.crear",
            entityType: "PurchaseOrder", entityId: orden.Id,
            description: orden.OrderNumber), ct);

        _logger.LogInformation("OC creada: {Numero} ({Id})", orden.OrderNumber, orden.Id);

        return Result<PurchaseOrderDto>.Success(ToDto(orden, bp.Name.LegalName));
    }

    internal static PurchaseOrderDto ToDto(
        PurchaseOrder o, string proveedorNombre,
        IReadOnlyList<string>? advertencias = null) => new(
        o.Id, o.OrderNumber,
        o.BusinessPartnerId, proveedorNombre,
        o.IssueDate, o.RequiredDate,
        o.Status, o.Currency,
        o.Subtotal, o.TaxTotal, o.Total,
        o.Notes, o.DeliveryAddress,
        o.TargetWarehouseId,
        o.SentAt, o.ApprovedAt, o.ApprovedBy, o.ClosedAt,
        o.CreatedAt)
    {
        Warnings = advertencias is { Count: > 0 } ? advertencias : null,
    };
}

