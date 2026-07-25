using ERP.Application.Common;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Interfaces;

namespace ERP.Application.Modules.Accounting.Posting;

/// <summary>Resuelve el AccountingPeriod cuyo rango contiene la fecha del hecho contable.</summary>
internal sealed class PostingPeriodResolver
{
    private readonly IAccountingPeriodRepository _accountingPeriodRepository;

    public PostingPeriodResolver(IAccountingPeriodRepository accountingPeriodRepository)
    {
        _accountingPeriodRepository = accountingPeriodRepository;
    }

    public async Task<Result<AccountingPeriod>> ResolveAsync(PostingFact fact, CancellationToken ct)
    {
        var period = await _accountingPeriodRepository.FindContainingDateAsync(
            fact.TenantId, fact.CompanyId, fact.EntryDate, ct);

        return period is null
            ? Result<AccountingPeriod>.ValidationFailure(
                "No existe un período contable abierto que contenga la fecha del asiento.", "PERIOD_NOT_OPEN")
            : Result<AccountingPeriod>.Success(period);
    }
}
