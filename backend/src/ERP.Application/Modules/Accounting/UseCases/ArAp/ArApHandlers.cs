using MediatR;
using ERP.Application.Common;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Interfaces;

namespace ERP.Application.Modules.Accounting.UseCases.ArAp;

public sealed class CreateArEntryCommandHandler : IRequestHandler<CreateArEntryCommand, Result<Guid>>
{
    private readonly IArApRepository  _repo;
    private readonly ICurrentUser     _user;
    private readonly ICurrentSubscriber _subscriber;

    public CreateArEntryCommandHandler(IArApRepository repo, ICurrentUser user, ICurrentSubscriber subscriber)
    {
        _repo       = repo;
        _user       = user;
        _subscriber = subscriber;
    }

    public async Task<Result<Guid>> Handle(CreateArEntryCommand cmd, CancellationToken ct)
    {
        var entry = AccountsReceivableEntry.Create(
            subscriberId:     _subscriber.SubscriberId,
            companyId:        cmd.CompanyId,
            businessPartnerId: cmd.BusinessPartnerId,
            reference:        cmd.Reference,
            issueDate:        cmd.IssueDate,
            dueDate:          cmd.DueDate,
            amount:           cmd.Amount,
            createdBy:        _user.UserId,
            salesBillId:      cmd.SalesBillId,
            currency:         cmd.Currency);

        await _repo.AddArEntryAsync(entry, ct);
        await _repo.SaveChangesAsync(ct);
        return Result<Guid>.Success(entry.Id);
    }
}

public sealed class ApplyArPaymentCommandHandler : IRequestHandler<ApplyArPaymentCommand, Result<bool>>
{
    private readonly IArApRepository  _repo;
    private readonly ICurrentUser     _user;
    private readonly ICurrentSubscriber _subscriber;

    public ApplyArPaymentCommandHandler(IArApRepository repo, ICurrentUser user, ICurrentSubscriber subscriber)
    {
        _repo       = repo;
        _user       = user;
        _subscriber = subscriber;
    }

    public async Task<Result<bool>> Handle(ApplyArPaymentCommand cmd, CancellationToken ct)
    {
        var entry = await _repo.GetArEntryByIdAsync(cmd.ArEntryId, _subscriber.SubscriberId, ct);
        if (entry is null)
            return Result<bool>.NotFound("Entrada de CxC no encontrada.");

        entry.ApplyPayment(cmd.Amount, _user.UserId);

        var payment = PaymentApplication.CreateForAr(
            subscriberId:    _subscriber.SubscriberId,
            companyId:       entry.CompanyId,
            arEntryId:       entry.Id,
            amount:          cmd.Amount,
            applicationDate: cmd.PaymentDate,
            createdBy:       _user.UserId,
            paymentReference: cmd.PaymentReference,
            notes:           cmd.Notes);

        await _repo.AddPaymentApplicationAsync(payment, ct);
        await _repo.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

public sealed class GetArAgingReportQueryHandler : IRequestHandler<GetArAgingReportQuery, Result<ArAgingReportDto>>
{
    private readonly IArApRepository  _repo;
    private readonly ICurrentSubscriber _subscriber;

    public GetArAgingReportQueryHandler(IArApRepository repo, ICurrentSubscriber subscriber)
    {
        _repo       = repo;
        _subscriber = subscriber;
    }

    public async Task<Result<ArAgingReportDto>> Handle(GetArAgingReportQuery query, CancellationToken ct)
    {
        var entries = await _repo.GetOpenArEntriesAsync(_subscriber.SubscriberId, query.CompanyId, ct);
        var asOf    = query.AsOf.Date;

        var lines = entries.Select(e =>
        {
            var remaining = e.RemainingAmount;
            var days      = e.DaysOverdue(asOf);

            var current  = days == 0            ? remaining : 0m;
            var d1To30   = days is > 0 and <= 30  ? remaining : 0m;
            var d31To60  = days is > 30 and <= 60 ? remaining : 0m;
            var d61To90  = days is > 60 and <= 90 ? remaining : 0m;
            var over90   = days > 90              ? remaining : 0m;

            return new ArAgingLineDto(e.Id, e.BusinessPartnerId, e.Reference, e.DueDate,
                current, d1To30, d31To60, d61To90, over90, remaining);
        }).ToList();

        var report = new ArAgingReportDto(
            Lines:        lines,
            TotalCurrent: lines.Sum(l => l.Current),
            Total1To30:   lines.Sum(l => l.Days1To30),
            Total31To60:  lines.Sum(l => l.Days31To60),
            Total61To90:  lines.Sum(l => l.Days61To90),
            TotalOver90:  lines.Sum(l => l.Over90),
            GrandTotal:   lines.Sum(l => l.Total),
            AsOf:         asOf);

        return Result<ArAgingReportDto>.Success(report);
    }
}

public sealed class CreateApEntryCommandHandler : IRequestHandler<CreateApEntryCommand, Result<Guid>>
{
    private readonly IArApRepository  _repo;
    private readonly ICurrentUser     _user;
    private readonly ICurrentSubscriber _subscriber;

    public CreateApEntryCommandHandler(IArApRepository repo, ICurrentUser user, ICurrentSubscriber subscriber)
    {
        _repo       = repo;
        _user       = user;
        _subscriber = subscriber;
    }

    public async Task<Result<Guid>> Handle(CreateApEntryCommand cmd, CancellationToken ct)
    {
        var entry = AccountsPayableEntry.Create(
            subscriberId:     _subscriber.SubscriberId,
            companyId:        cmd.CompanyId,
            businessPartnerId: cmd.BusinessPartnerId,
            reference:        cmd.Reference,
            issueDate:        cmd.IssueDate,
            dueDate:          cmd.DueDate,
            amount:           cmd.Amount,
            createdBy:        _user.UserId,
            purchBillId:      cmd.PurchBillId,
            currency:         cmd.Currency);

        await _repo.AddApEntryAsync(entry, ct);
        await _repo.SaveChangesAsync(ct);
        return Result<Guid>.Success(entry.Id);
    }
}

public sealed class ApplyApPaymentCommandHandler : IRequestHandler<ApplyApPaymentCommand, Result<bool>>
{
    private readonly IArApRepository  _repo;
    private readonly ICurrentUser     _user;
    private readonly ICurrentSubscriber _subscriber;

    public ApplyApPaymentCommandHandler(IArApRepository repo, ICurrentUser user, ICurrentSubscriber subscriber)
    {
        _repo       = repo;
        _user       = user;
        _subscriber = subscriber;
    }

    public async Task<Result<bool>> Handle(ApplyApPaymentCommand cmd, CancellationToken ct)
    {
        var entry = await _repo.GetApEntryByIdAsync(cmd.ApEntryId, _subscriber.SubscriberId, ct);
        if (entry is null)
            return Result<bool>.NotFound("Entrada de CxP no encontrada.");

        entry.ApplyPayment(cmd.Amount, _user.UserId);

        var payment = PaymentApplication.CreateForAp(
            subscriberId:    _subscriber.SubscriberId,
            companyId:       entry.CompanyId,
            apEntryId:       entry.Id,
            amount:          cmd.Amount,
            applicationDate: cmd.PaymentDate,
            createdBy:       _user.UserId,
            paymentReference: cmd.PaymentReference,
            notes:           cmd.Notes);

        await _repo.AddPaymentApplicationAsync(payment, ct);
        await _repo.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

public sealed class GetApAgingReportQueryHandler : IRequestHandler<GetApAgingReportQuery, Result<ApAgingReportDto>>
{
    private readonly IArApRepository  _repo;
    private readonly ICurrentSubscriber _subscriber;

    public GetApAgingReportQueryHandler(IArApRepository repo, ICurrentSubscriber subscriber)
    {
        _repo       = repo;
        _subscriber = subscriber;
    }

    public async Task<Result<ApAgingReportDto>> Handle(GetApAgingReportQuery query, CancellationToken ct)
    {
        var entries = await _repo.GetOpenApEntriesAsync(_subscriber.SubscriberId, query.CompanyId, ct);
        var asOf    = query.AsOf.Date;

        var lines = entries.Select(e =>
        {
            var remaining = e.RemainingAmount;
            var days      = e.DaysOverdue(asOf);

            var current  = days == 0            ? remaining : 0m;
            var d1To30   = days is > 0 and <= 30  ? remaining : 0m;
            var d31To60  = days is > 30 and <= 60 ? remaining : 0m;
            var d61To90  = days is > 60 and <= 90 ? remaining : 0m;
            var over90   = days > 90              ? remaining : 0m;

            return new ApAgingLineDto(e.Id, e.BusinessPartnerId, e.Reference, e.DueDate,
                current, d1To30, d31To60, d61To90, over90, remaining);
        }).ToList();

        var report = new ApAgingReportDto(
            Lines:        lines,
            TotalCurrent: lines.Sum(l => l.Current),
            Total1To30:   lines.Sum(l => l.Days1To30),
            Total31To60:  lines.Sum(l => l.Days31To60),
            Total61To90:  lines.Sum(l => l.Days61To90),
            TotalOver90:  lines.Sum(l => l.Over90),
            GrandTotal:   lines.Sum(l => l.Total),
            AsOf:         asOf);

        return Result<ApAgingReportDto>.Success(report);
    }
}
