using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Sales.UseCases.AnularFactura;

[RequireFeature(SubscriptionFeatureCodes.Sales)]
public sealed record AnularFacturaCommand(Guid VentaId) : IRequest<Result<Guid>>;
