using ERP.Application.Modules.Accounting.Continuity;
using ERP.Domain.Modules.Accounting.Entities;

namespace ERP.Application.Tests.Accounting;

/// <summary>Readiness contable sin motivos — para tests de reportes que no evalúan la completitud.</summary>
internal sealed class CompleteAccounting : IAccountingCompletenessEvaluator
{
    public static readonly CompleteAccounting Instance = new();

    public Task<AccountingCompleteness> EvaluateAsync(
        Guid tenantId, Guid companyId, DateOnly? from, DateOnly to, CancellationToken ct) =>
        Task.FromResult(new AccountingCompleteness(true, to, []));

    public Task<AccountingCompleteness> EvaluateClosureAsync(
        Guid tenantId, Guid companyId, AccountingPeriod period, CancellationToken ct) =>
        Task.FromResult(new AccountingCompleteness(true, period.EndDate, []));
}
