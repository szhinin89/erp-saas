using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Sales.UseCases.EmitirFacturaElectronica;

[RequireFeature(SubscriptionFeatureCodes.Sales)]
public sealed record EmitirFacturaElectronicaCommand(Guid VentaId) : IRequest<Result<Guid>>;
