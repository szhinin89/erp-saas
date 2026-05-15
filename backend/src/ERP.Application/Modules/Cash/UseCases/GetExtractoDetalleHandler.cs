using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Cash.DTOs;
using ERP.Domain.Modules.Cash.Interfaces;

namespace ERP.Application.Modules.Cash.UseCases;

public sealed record ExtractoDetalleDto(
    BankStatementDto Cabecera,
    IReadOnlyList<BankTransactionDto> Rows);

public sealed record GetExtractoDetalleQuery(Guid ExtractoId) : IRequest<Result<ExtractoDetalleDto>>;

public sealed class GetExtractoDetalleQueryHandler : IRequestHandler<GetExtractoDetalleQuery, Result<ExtractoDetalleDto>>
{
    private readonly ICashRepository _caja;

    public GetExtractoDetalleQueryHandler(ICashRepository caja) => _caja = caja;

    public async Task<Result<ExtractoDetalleDto>> Handle(GetExtractoDetalleQuery request, CancellationToken ct)
    {
        var x = await _caja.GetBankStatementWithTransactionsAsync(request.ExtractoId, ct);
        if (x is null)
            return Result<ExtractoDetalleDto>.Failure("Extracto no encontrado.");

        var cab = new BankStatementDto(
            x.Id,
            x.BankAccountId,
            x.PeriodFrom,
            x.PeriodTo,
            x.OpeningBalance,
            x.ClosingBalance,
            x.LoadedAt,
            x.IsReconciled,
            x.Transactions.Count);

        var movs = x.Transactions
            .OrderBy(m => m.TransactionDate)
            .Select(m => new BankTransactionDto(
                m.Id,
                m.BankStatementId,
                m.TransactionDate,
                m.Description,
                m.Amount,
                m.TransactionType,
                m.Reference,
                m.JournalEntryId,
                m.Status))
            .ToList();

        return Result<ExtractoDetalleDto>.Success(new ExtractoDetalleDto(cab, movs));
    }
}
