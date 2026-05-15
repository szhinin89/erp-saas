using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Cash.DTOs;
using ERP.Application.Common;
using ERP.Domain.Modules.Cash.Entities;
using ERP.Domain.Modules.Cash.Interfaces;

namespace ERP.Application.Modules.Cash.UseCases;

public sealed record ListCuentasBancariasQuery : IRequest<Result<IReadOnlyList<BankAccountDto>>>;

public sealed class ListCuentasBancariasQueryHandler
    : IRequestHandler<ListCuentasBancariasQuery, Result<IReadOnlyList<BankAccountDto>>>
{
    private readonly ICashRepository _caja;

    public ListCuentasBancariasQueryHandler(ICashRepository caja) => _caja = caja;

    public async Task<Result<IReadOnlyList<BankAccountDto>>> Handle(
        ListCuentasBancariasQuery request,
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

public sealed record CrearBankAccountCommand(
    string Name,
    string AccountNumber,
    string AccountType,
    string Currency,
    decimal InitialBalance,
    Guid? LedgerAccountId) : IRequest<Result<BankAccountDto>>;

public sealed class CrearBankAccountCommandHandler
    : IRequestHandler<CrearBankAccountCommand, Result<BankAccountDto>>
{
    private readonly ICashRepository _caja;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;
    private readonly IUnitOfWork _uow;

    public CrearBankAccountCommandHandler(
        ICashRepository caja,
        ICurrentTenant tenant,
        ICurrentUser user,
        IUnitOfWork uow)
    {
        _caja   = caja;
        _tenant = tenant;
        _user   = user;
        _uow    = uow;
    }

    public async Task<Result<BankAccountDto>> Handle(CrearBankAccountCommand cmd, CancellationToken ct)
    {
        var entity = BankAccount.Create(
            _tenant.TenantId,
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

public sealed record ListExtractosPorCuentaQuery(Guid BankAccountId)
    : IRequest<Result<IReadOnlyList<BankStatementDto>>>;

public sealed class ListExtractosPorCuentaQueryHandler
    : IRequestHandler<ListExtractosPorCuentaQuery, Result<IReadOnlyList<BankStatementDto>>>
{
    private readonly ICashRepository _caja;

    public ListExtractosPorCuentaQueryHandler(ICashRepository caja) => _caja = caja;

    public async Task<Result<IReadOnlyList<BankStatementDto>>> Handle(
        ListExtractosPorCuentaQuery request,
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
