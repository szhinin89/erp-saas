using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Accounting.DTOs;

namespace ERP.Application.Modules.Accounting.UseCases.AccountingConfiguration;

[RequireFeature(SubscriptionFeatureCodes.Accounting)]
public sealed record GetExpenseCategorysQuery : IRequest<Result<IReadOnlyList<ExpenseCategoryDto>>>, ICompanyScopedRequest;
