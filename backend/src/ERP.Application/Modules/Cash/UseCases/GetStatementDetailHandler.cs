using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Cash.DTOs;
using ERP.Domain.Modules.Cash.Interfaces;

namespace ERP.Application.Modules.Cash.UseCases;

public sealed record StatementDetailDto(
    BankStatementDto Cabecera,
    IReadOnlyList<BankTransactionDto> Rows);

public sealed record GetStatementDetailQuery(Guid StatementId) : IRequest<Result<StatementDetailDto>>, ICompanyScopedRequest;

public sealed class GetStatementDetailQueryHandler : IRequestHandler<GetStatementDetailQuery, Result<StatementDetailDto>>
{
    private readonly ICashRepository _caja;

    public GetStatementDetailQueryHandler(ICashRepository caja) => _caja = caja;

    public async Task<Result<StatementDetailDto>> Handle(GetStatementDetailQuery request, CancellationToken ct)
    {
        var x = await _caja.GetBankStatementWithTransactionsAsync(request.StatementId, ct);
        if (x is null)
            return Result<StatementDetailDto>.Failure("Extracto no encontrado.");

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

        return Result<StatementDetailDto>.Success(new StatementDetailDto(cab, movs));
    }
}
