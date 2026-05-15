using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;

namespace ERP.Application.Modules.Purchasing.UseCases.RechazarCompra;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public sealed record RechazarPurchaseCommand(Guid PurchBillId, string  Reason)
    : IRequest<Result<PurchBillDto>>;
