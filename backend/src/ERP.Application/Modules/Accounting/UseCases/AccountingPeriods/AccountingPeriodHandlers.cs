using MediatR;
using ERP.Application.Common;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Interfaces;

namespace ERP.Application.Modules.Accounting.UseCases.AccountingPeriods;

public sealed class OpenAccountingPeriodHandler
    : IRequestHandler<OpenAccountingPeriodCommand, Result<Guid>>
{
    private readonly IAccountingRepository _repo;
    private readonly ICurrentSubscriber    _subscriber;
    private readonly ICurrentCompany       _company;
    private readonly ICurrentUser          _user;
    private readonly IUnitOfWork           _uow;

    public OpenAccountingPeriodHandler(
        IAccountingRepository repo,
        ICurrentSubscriber subscriber,
        ICurrentCompany company,
        ICurrentUser user,
        IUnitOfWork uow)
    {
        _repo       = repo;
        _subscriber = subscriber;
        _company    = company;
        _user       = user;
        _uow        = uow;
    }

    public async Task<Result<Guid>> Handle(
        OpenAccountingPeriodCommand cmd, CancellationToken ct)
    {
        var subscriberId = _subscriber.SubscriberId;
        var companyId    = _company.CompanyId;

        var existing = await _repo.GetPeriodAsync(subscriberId, cmd.Year, cmd.Month, ct);
        if (existing is not null)
            return Result<Guid>.Failure($"El período {existing.Name} ya existe.");

        var period = AccountingPeriod.Create(subscriberId, companyId, cmd.Year, cmd.Month, _user.UserId);
        await _repo.AddPeriodAsync(period, ct);
        await _repo.SaveChangesAsync(ct);

        return Result<Guid>.Success(period.Id);
    }
}

public sealed class CloseAccountingPeriodHandler
    : IRequestHandler<CloseAccountingPeriodCommand, Result<bool>>
{
    private readonly IAccountingRepository _repo;
    private readonly ICurrentSubscriber    _subscriber;
    private readonly ICurrentUser          _user;
    private readonly IUnitOfWork           _uow;

    public CloseAccountingPeriodHandler(
        IAccountingRepository repo,
        ICurrentSubscriber subscriber,
        ICurrentUser user,
        IUnitOfWork uow)
    {
        _repo       = repo;
        _subscriber = subscriber;
        _user       = user;
        _uow        = uow;
    }

    public async Task<Result<bool>> Handle(
        CloseAccountingPeriodCommand cmd, CancellationToken ct)
    {
        var subscriberId = _subscriber.SubscriberId;
        var periods = await _repo.GetPeriodsAsync(subscriberId, ct);
        var period = periods.FirstOrDefault(p => p.Id == cmd.PeriodId);
        if (period is null)
            return Result<bool>.NotFound("Período contable no encontrado.");

        try { period.Close(_user.UserId); }
        catch (InvalidOperationException ex) { return Result<bool>.Failure(ex.Message); }

        await _repo.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

public sealed class ReopenAccountingPeriodHandler
    : IRequestHandler<ReopenAccountingPeriodCommand, Result<bool>>
{
    private readonly IAccountingRepository _repo;
    private readonly ICurrentSubscriber    _subscriber;
    private readonly ICurrentUser          _user;
    private readonly IUnitOfWork           _uow;

    public ReopenAccountingPeriodHandler(
        IAccountingRepository repo,
        ICurrentSubscriber subscriber,
        ICurrentUser user,
        IUnitOfWork uow)
    {
        _repo       = repo;
        _subscriber = subscriber;
        _user       = user;
        _uow        = uow;
    }

    public async Task<Result<bool>> Handle(
        ReopenAccountingPeriodCommand cmd, CancellationToken ct)
    {
        var subscriberId = _subscriber.SubscriberId;
        var periods = await _repo.GetPeriodsAsync(subscriberId, ct);
        var period = periods.FirstOrDefault(p => p.Id == cmd.PeriodId);
        if (period is null)
            return Result<bool>.NotFound("Período contable no encontrado.");

        try { period.Reopen(_user.UserId); }
        catch (InvalidOperationException ex) { return Result<bool>.Failure(ex.Message); }

        await _repo.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

public sealed class GetAccountingPeriodsHandler
    : IRequestHandler<GetAccountingPeriodsQuery, Result<IReadOnlyList<AccountingPeriodDto>>>
{
    private readonly IAccountingRepository _repo;
    private readonly ICurrentSubscriber    _subscriber;

    public GetAccountingPeriodsHandler(IAccountingRepository repo, ICurrentSubscriber subscriber)
    {
        _repo       = repo;
        _subscriber = subscriber;
    }

    public async Task<Result<IReadOnlyList<AccountingPeriodDto>>> Handle(
        GetAccountingPeriodsQuery query, CancellationToken ct)
    {
        var periods = await _repo.GetPeriodsAsync(_subscriber.SubscriberId, ct);
        var dtos = periods.Select(p => new AccountingPeriodDto(
            p.Id, p.Year, p.Month, p.Name, p.IsClosed, p.ClosedBy, p.ClosedAt))
            .ToList();
        return Result<IReadOnlyList<AccountingPeriodDto>>.Success(dtos);
    }
}
