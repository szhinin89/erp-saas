using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Modules.Accounting.UseCases.AccountingPeriods;

public sealed record OpenAccountingPeriodCommand(int Year, int Month)
    : IRequest<Result<Guid>>, ICompanyScopedRequest;

public sealed record CloseAccountingPeriodCommand(Guid PeriodId)
    : IRequest<Result<bool>>, ISubscriberScopedRequest;

public sealed record ReopenAccountingPeriodCommand(Guid PeriodId)
    : IRequest<Result<bool>>, ISubscriberScopedRequest;

public sealed record GetAccountingPeriodsQuery()
    : IRequest<Result<IReadOnlyList<AccountingPeriodDto>>>, ISubscriberScopedRequest;

public sealed record AccountingPeriodDto(
    Guid      Id,
    int       Year,
    int       Month,
    string    Name,
    bool      IsClosed,
    Guid?     ClosedBy,
    DateTime? ClosedAt);
