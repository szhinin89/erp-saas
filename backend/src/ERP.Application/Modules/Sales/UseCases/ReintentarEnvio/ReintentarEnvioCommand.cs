using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Sales.UseCases.ReintentarEnvio;

[RequireFeature(SubscriptionFeatureCodes.Sales)]
public sealed record RetrySubmissionCommand(Guid VentaId) : IRequest<Result<Guid>>, ICompanyScopedRequest;
