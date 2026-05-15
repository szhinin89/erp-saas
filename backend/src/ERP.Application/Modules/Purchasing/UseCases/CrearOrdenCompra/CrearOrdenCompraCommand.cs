using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;

namespace ERP.Application.Modules.Purchasing.UseCases.CrearOrdenCompra;

[RequireFeature(SubscriptionFeatureCodes.Purchases)]
public sealed record CrearOrderPurchaseCommand(
    Guid    SupplierId,
    DateTime                      RequiredDate,
    Guid?                         TargetWarehouseId,
    string?                       DeliveryAddress,
    string? Notes,
    List<PurchaseOrderItemRequest>  Items
) : IRequest<Result<PurchaseOrderDto>>;
