using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Modules.Accounting.UseCases.GetBalanceComprobacion;

public sealed record BalanceComprobacionLineDto(
    string  AccountCode,
    string  AccountName,
    string  AccountType,
    decimal TotalDebit,
    decimal TotalCredit,
    decimal NetBalance);

public sealed record GetBalanceComprobacionQuery(
    DateTime Desde,
    DateTime Hasta)
    : IRequest<Result<IReadOnlyList<BalanceComprobacionLineDto>>>;
