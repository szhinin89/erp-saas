using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Sales.UseCases.ValidateSale;

[RequireFeature(SubscriptionFeatureCodes.Sales)]
public sealed record ValidateSaleCommand(Guid VentaId) : IRequest<Result<Guid>>, ICompanyScopedRequest;
