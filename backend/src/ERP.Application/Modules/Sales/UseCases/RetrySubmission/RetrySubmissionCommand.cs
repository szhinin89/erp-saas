using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Sales.UseCases.RetrySubmission;

[RequireFeature(SubscriptionFeatureCodes.Sales)]
public sealed record RetrySubmissionCommand(Guid SaleId) : IRequest<Result<Guid>>, ICompanyScopedRequest;
