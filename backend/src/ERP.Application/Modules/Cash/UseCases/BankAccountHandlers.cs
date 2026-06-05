using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Cash.DTOs;
using ERP.Application.Common;
using ERP.Domain.Modules.Cash.Entities;
using ERP.Domain.Modules.Cash.Interfaces;

namespace ERP.Application.Modules.Cash.UseCases;

public sealed record ListBankAccountsQuery : IRequest<Result<IReadOnlyList<BankAccountDto>>>, ICompanyScopedRequest;

public sealed class ListBankAccountsQueryHandler
    : IRequestHandler<ListBankAccountsQuery, Result<IReadOnlyList<BankAccountDto>>>
{
    private readonly ICashRepository _caja;

    public ListBankAccountsQueryHandler(ICashRepository caja) => _caja = caja;

    public async Task<Result<IReadOnlyList<BankAccountDto>>> Handle(
        ListBankAccountsQuery request,
        CancellationToken ct)
    {
        var list = await _caja.ListBankAccountsAsync(ct);
        return Result<IReadOnlyList<BankAccountDto>>.Success(
            list.Select(x => new BankAccountDto(
                x.Id,
                x.Name,
                x.AccountNumber,
                x.AccountType,
                x.Currency,
                x.InitialBalance,
                x.CurrentBalance,
                x.IsActive,
                x.LedgerAccountId)).ToList());
    }
}

public sealed record CreateBankAccountCommand(
    string Name,
    string AccountNumber,
    string AccountType,
    string Currency,
    decimal InitialBalance,
    Guid? LedgerAccountId) : IRequest<Result<BankAccountDto>>, ICompanyScopedRequest;

public sealed class CreateBankAccountCommandHandler
    : IRequestHandler<CreateBankAccountCommand, Result<BankAccountDto>>
{
    private readonly ICashRepository _caja;
    private readonly ICurrentSubscriber _subscriber;
    private readonly ICurrentUser _user;
    private readonly IUnitOfWork _uow;

    public CreateBankAccountCommandHandler(
        ICashRepository caja,
        ICurrentSubscriber subscriber,
        ICurrentUser user,
        IUnitOfWork uow)
    {
        _caja   = caja;
        _subscriber = subscriber;
        _user   = user;
        _uow    = uow;
    }

    public async Task<Result<BankAccountDto>> Handle(CreateBankAccountCommand cmd, CancellationToken ct)
    {
        var entity = BankAccount.Create(
            _subscriber.SubscriberId,
            cmd.Name,
            cmd.AccountNumber,
            cmd.AccountType,
            cmd.Currency,
            cmd.InitialBalance,
            _user.UserId,
            cmd.LedgerAccountId);
        await _caja.AddBankAccountAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<BankAccountDto>.Success(
            new BankAccountDto(
                entity.Id,
                entity.Name,
                entity.AccountNumber,
                entity.AccountType,
                entity.Currency,
                entity.InitialBalance,
                entity.CurrentBalance,
                entity.IsActive,
                entity.LedgerAccountId));
    }
}

public sealed record ListStatementsByAccountQuery(Guid BankAccountId)
    : IRequest<Result<IReadOnlyList<BankStatementDto>>>, ICompanyScopedRequest;

public sealed class ListStatementsByAccountQueryHandler
    : IRequestHandler<ListStatementsByAccountQuery, Result<IReadOnlyList<BankStatementDto>>>
{
    private readonly ICashRepository _caja;

    public ListStatementsByAccountQueryHandler(ICashRepository caja) => _caja = caja;

    public async Task<Result<IReadOnlyList<BankStatementDto>>> Handle(
        ListStatementsByAccountQuery request,
        CancellationToken ct)
    {
        var list = await _caja.ListStatementsByAccountAsync(request.BankAccountId, ct);
        return Result<IReadOnlyList<BankStatementDto>>.Success(
            list.Select(x => new BankStatementDto(
                x.Id,
                x.BankAccountId,
                x.PeriodFrom,
                x.PeriodTo,
                x.OpeningBalance,
                x.ClosingBalance,
                x.LoadedAt,
                x.IsReconciled,
                x.Transactions.Count)).ToList());
    }
}
