using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Modules.Accounting.UseCases.GetTrialBalance;

public sealed record BalanceComprobacionLineDto(
    string  AccountCode,
    string  AccountName,
    string  AccountType,
    decimal TotalDebit,
    decimal TotalCredit,
    decimal NetBalance);

public sealed record GetTrialBalanceQuery(
    DateTime Desde,
    DateTime Hasta)
    : IRequest<Result<IReadOnlyList<BalanceComprobacionLineDto>>>, ICompanyScopedRequest;
