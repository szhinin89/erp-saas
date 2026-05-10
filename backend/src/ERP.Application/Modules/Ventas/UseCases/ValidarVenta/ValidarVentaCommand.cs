using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Ventas.UseCases.ValidarVenta;

[RequireFeature(SubscriptionFeatureCodes.Sales)]
public sealed record ValidarVentaCommand(Guid VentaId) : IRequest<Result<Guid>>;
