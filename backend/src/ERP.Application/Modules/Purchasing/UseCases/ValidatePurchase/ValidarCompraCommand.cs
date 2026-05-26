using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;

namespace ERP.Application.Modules.Purchasing.UseCases.ValidatePurchase;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public sealed record ValidatePurchaseCommand(Guid PurchBillId)
    : IRequest<Result<PurchBillDto>>, ICompanyScopedRequest;
