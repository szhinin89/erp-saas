using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Sales.UseCases.ValidarVenta;

[RequireFeature(SubscriptionFeatureCodes.Sales)]
public sealed record ValidarSaleCommand(Guid VentaId) : IRequest<Result<Guid>>;
