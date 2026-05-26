using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Modules.Accounting.UseCases.AccountingConfiguration;

[RequireFeature(SubscriptionFeatureCodes.Accounting)]
public sealed record DeleteExpenseCategoryCommand(Guid Id) : IRequest<Result<Unit>>, ICompanyScopedRequest;
