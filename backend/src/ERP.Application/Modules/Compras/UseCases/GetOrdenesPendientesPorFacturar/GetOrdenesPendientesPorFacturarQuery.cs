using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Compras.DTOs;

namespace ERP.Application.Modules.Compras.UseCases.GetOrdenesPendientesPorFacturar;

[RequireFeature(SubscriptionFeatureCodes.Purchases)]
public sealed record GetOrdenesPendientesPorFacturarQuery
    : IRequest<Result<IReadOnlyList<OrdenCompraDto>>>;
