using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Cash.DTOs;
using ERP.Application.Common;
using ERP.Domain.Modules.Cash.Entities;
using ERP.Domain.Modules.Cash.Interfaces;

namespace ERP.Application.Modules.Cash.UseCases;

public sealed record ListCashesChicasQuery : IRequest<Result<IReadOnlyList<PettyCashDto>>>;

public sealed class ListCashesChicasQueryHandler : IRequestHandler<ListCashesChicasQuery, Result<IReadOnlyList<PettyCashDto>>>
{
    private readonly ICashRepository _caja;
    public ListCashesChicasQueryHandler(ICashRepository caja) => _caja = caja;

    public async Task<Result<IReadOnlyList<PettyCashDto>>> Handle(ListCashesChicasQuery _, CancellationToken ct)
    {
        var list = await _caja.ListPettyCashesAsync(ct);
        return Result<IReadOnlyList<PettyCashDto>>.Success(
            list.Select(x => new PettyCashDto(
                x.Id,
                x.Name,
                x.AssignedBalance,
                x.CurrentBalance,
                x.ReplenishBankAccountId,
                x.LedgerAccountId,
                x.IsActive)).ToList());
    }
}

public sealed record CrearPettyCashCommand(
    string Name,
    decimal AssignedBalance,
    Guid? ReplenishBankAccountId,
    Guid? LedgerAccountId) : IRequest<Result<PettyCashDto>>;

public sealed class CrearPettyCashCommandHandler : IRequestHandler<CrearPettyCashCommand, Result<PettyCashDto>>
{
    private readonly ICashRepository _caja;
    private readonly ICurrentSubscriber _tenant;
    private readonly ICurrentUser _user;
    private readonly IUnitOfWork _uow;

    public CrearPettyCashCommandHandler(ICashRepository caja, ICurrentSubscriber tenant, ICurrentUser user, IUnitOfWork uow)
    {
        _caja   = caja;
        _tenant = tenant;
        _user   = user;
        _uow    = uow;
    }

    public async Task<Result<PettyCashDto>> Handle(CrearPettyCashCommand cmd, CancellationToken ct)
    {
        var c = PettyCash.Create(
            _tenant.SubscriberId,
            cmd.Name,
            cmd.AssignedBalance,
            _user.UserId,
            cmd.ReplenishBankAccountId,
            cmd.LedgerAccountId);
        await _caja.AddPettyCashAsync(c, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<PettyCashDto>.Success(
            new PettyCashDto(
                c.Id,
                c.Name,
                c.AssignedBalance,
                c.CurrentBalance,
                c.ReplenishBankAccountId,
                c.LedgerAccountId,
                c.IsActive));
    }
}

public sealed record CrearGastoPettyCashCommand(
    Guid PettyCashId,
    DateTime TransactionDate,
    string Concept,
    decimal Amount,
    string VoucherType,
    string? VoucherNumber) : IRequest<Result<PettyCashExpenseDto>>;

public sealed class CrearGastoPettyCashCommandHandler
    : IRequestHandler<CrearGastoPettyCashCommand, Result<PettyCashExpenseDto>>
{
    private readonly ICashRepository _caja;
    private readonly ICurrentSubscriber _tenant;
    private readonly ICurrentUser _user;
    private readonly IUnitOfWork _uow;

    public CrearGastoPettyCashCommandHandler(
        ICashRepository caja,
        ICurrentSubscriber tenant,
        ICurrentUser user,
        IUnitOfWork uow)
    {
        _caja   = caja;
        _tenant = tenant;
        _user   = user;
        _uow    = uow;
    }

    public async Task<Result<PettyCashExpenseDto>> Handle(CrearGastoPettyCashCommand cmd, CancellationToken ct)
    {
        var caja = await _caja.GetPettyCashByIdAsync(cmd.PettyCashId, ct);
        if (caja is null || !caja.IsActive)
            return Result<PettyCashExpenseDto>.Failure("Caja chica no encontrada o inactiva.");

        try
        {
            caja.RegisterExpense(cmd.Amount, _user.UserId);
        }
        catch (InvalidOperationException ex)
        {
            return Result<PettyCashExpenseDto>.Failure(ex.Message);
        }

        var gasto = PettyCashExpense.Create(
            _tenant.SubscriberId,
            cmd.PettyCashId,
            cmd.TransactionDate,
            cmd.Concept,
            cmd.Amount,
            cmd.VoucherType,
            cmd.VoucherNumber,
            _user.UserId);

        await _caja.AddPettyCashExpenseAsync(gasto, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<PettyCashExpenseDto>.Success(
            new PettyCashExpenseDto(
                gasto.Id,
                gasto.PettyCashId,
                gasto.ExpenseDate,
                gasto.Description,
                gasto.Amount,
                gasto.VoucherType,
                gasto.VoucherNumber,
                gasto.JournalEntryId));
    }
}

public sealed record CrearCashCountCommand(
    Guid PettyCashId,
    DateTime CountDate,
    decimal PhysicalCash,
    string? Notes) : IRequest<Result<PettyCashCountDto>>;

public sealed class CrearCashCountCommandHandler : IRequestHandler<CrearCashCountCommand, Result<PettyCashCountDto>>
{
    private readonly ICashRepository _caja;
    private readonly ICurrentSubscriber _tenant;
    private readonly ICurrentUser _user;
    private readonly IUnitOfWork _uow;

    public CrearCashCountCommandHandler(ICashRepository caja, ICurrentSubscriber tenant, ICurrentUser user, IUnitOfWork uow)
    {
        _caja   = caja;
        _tenant = tenant;
        _user   = user;
        _uow    = uow;
    }

    public async Task<Result<PettyCashCountDto>> Handle(CrearCashCountCommand cmd, CancellationToken ct)
    {
        var caja = await _caja.GetPettyCashByIdAsync(cmd.PettyCashId, ct);
        if (caja is null)
            return Result<PettyCashCountDto>.Failure("Caja chica no encontrada.");

        var arqueo = CashCount.Create(
            _tenant.SubscriberId,
            cmd.PettyCashId,
            cmd.CountDate,
            cmd.PhysicalCash,
            caja.CurrentBalance,
            cmd.Notes,
            _user.UserId);

        await _caja.AddCashCountAsync(arqueo, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<PettyCashCountDto>.Success(
            new PettyCashCountDto(
                arqueo.Id,
                arqueo.PettyCashId,
                arqueo.CountDate,
                arqueo.PhysicalCash,
                arqueo.Difference,
                arqueo.Notes,
                arqueo.IsApproved));
    }
}

public sealed record AprobarCashCountCommand(Guid ArqueoId) : IRequest<Result<PettyCashCountDto>>;

public sealed class AprobarCashCountCommandHandler : IRequestHandler<AprobarCashCountCommand, Result<PettyCashCountDto>>
{
    private readonly ICashRepository _caja;
    private readonly ICurrentUser _user;
    private readonly IUnitOfWork _uow;

    public AprobarCashCountCommandHandler(ICashRepository caja, ICurrentUser user, IUnitOfWork uow)
    {
        _caja = caja;
        _user = user;
        _uow  = uow;
    }

    public async Task<Result<PettyCashCountDto>> Handle(AprobarCashCountCommand cmd, CancellationToken ct)
    {
        var arqueo = await _caja.GetCashCountByIdAsync(cmd.ArqueoId, ct);
        if (arqueo is null)
            return Result<PettyCashCountDto>.Failure("Arqueo no encontrado.");

        var caja = await _caja.GetPettyCashByIdAsync(arqueo.PettyCashId, ct);
        if (caja is null)
            return Result<PettyCashCountDto>.Failure("Caja chica no encontrada.");

        try
        {
            arqueo.Approve(_user.UserId);
            caja.ApplyBalanceFromCount(arqueo.PhysicalCash, _user.UserId);
        }
        catch (InvalidOperationException ex)
        {
            return Result<PettyCashCountDto>.Failure(ex.Message);
        }

        await _uow.SaveChangesAsync(ct);

        return Result<PettyCashCountDto>.Success(
            new PettyCashCountDto(
                arqueo.Id,
                arqueo.PettyCashId,
                arqueo.CountDate,
                arqueo.PhysicalCash,
                arqueo.Difference,
                arqueo.Notes,
                arqueo.IsApproved));
    }
}

public sealed record ReposicionPettyCashCommand(Guid PettyCashId, decimal Amount) : IRequest<Result<PettyCashDto>>;

public sealed class ReposicionPettyCashCommandHandler
    : IRequestHandler<ReposicionPettyCashCommand, Result<PettyCashDto>>
{
    private readonly ICashRepository _caja;
    private readonly ICurrentUser _user;
    private readonly IUnitOfWork _uow;

    public ReposicionPettyCashCommandHandler(ICashRepository caja, ICurrentUser user, IUnitOfWork uow)
    {
        _caja = caja;
        _user = user;
        _uow  = uow;
    }

    public async Task<Result<PettyCashDto>> Handle(ReposicionPettyCashCommand cmd, CancellationToken ct)
    {
        var caja = await _caja.GetPettyCashByIdAsync(cmd.PettyCashId, ct);
        if (caja is null || !caja.IsActive)
            return Result<PettyCashDto>.Failure("Caja chica no encontrada o inactiva.");

        if (caja.ReplenishBankAccountId is null)
            return Result<PettyCashDto>.Failure(
                "La caja chica no tiene cuenta bancaria de reposición configurada.");

        try
        {
            caja.RegisterReplenishment(cmd.Amount, _user.UserId);
        }
        catch (ArgumentException ex)
        {
            return Result<PettyCashDto>.Failure(ex.Message);
        }

        var banco = await _caja.GetBankAccountByIdAsync(caja.ReplenishBankAccountId.Value, ct);
        if (banco is not null)
            banco.IncrementBalance(-cmd.Amount, _user.UserId);

        await _uow.SaveChangesAsync(ct);

        return Result<PettyCashDto>.Success(
            new PettyCashDto(
                caja.Id,
                caja.Name,
                caja.AssignedBalance,
                caja.CurrentBalance,
                caja.ReplenishBankAccountId,
                caja.LedgerAccountId,
                caja.IsActive));
    }
}
