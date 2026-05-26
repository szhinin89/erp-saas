using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;

namespace ERP.Application.Modules.Purchasing.UseCases.ApprovePurchase;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public sealed record ApprovePurchaseCommand(Guid PurchBillId)
    : IRequest<Result<PurchBillDto>>, ICompanyScopedRequest;
