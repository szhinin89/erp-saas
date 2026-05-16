using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;

namespace ERP.Application.Modules.Purchasing.UseCases.GetOrdenesPendientesPorFacturar;

[RequireFeature(SubscriptionFeatureCodes.Purchases)]
public sealed record GetOrdersPendingBillingQuery
    : IRequest<Result<IReadOnlyList<PurchaseOrderDto>>>;
