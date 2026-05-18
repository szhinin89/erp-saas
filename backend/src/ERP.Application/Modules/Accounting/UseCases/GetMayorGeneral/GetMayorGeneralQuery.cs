using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Modules.Accounting.UseCases.GetMayorGeneral;

public sealed record MayorGeneralLineDto(
    DateTime Date,
    string   Reference,
    string   Description,
    decimal  Debit,
    decimal  Credit,
    decimal  Balance);

public sealed record GetMayorGeneralQuery(
    Guid     AccountId,
    DateTime Desde,
    DateTime Hasta)
    : IRequest<Result<IReadOnlyList<MayorGeneralLineDto>>>;
