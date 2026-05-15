using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;

namespace ERP.Application.Modules.Purchasing.UseCases.AprobarCompra;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public sealed record AprobarCompraCommand(Guid PurchBillId)
    : IRequest<Result<PurchBillDto>>;
