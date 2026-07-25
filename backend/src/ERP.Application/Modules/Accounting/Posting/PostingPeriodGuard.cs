using ERP.Application.Common;
using ERP.Domain.Modules.Accounting.Entities;

namespace ERP.Application.Modules.Accounting.Posting;

/// <summary>Invoca AccountingPeriod.EnsureOpenForPosting() — no duplica la regla del Domain (ADR-026 §6.1).</summary>
internal sealed class PostingPeriodGuard
{
    public Result<AccountingPeriod> Ensure(AccountingPeriod period)
    {
        try
        {
            period.EnsureOpenForPosting();
            return Result<AccountingPeriod>.Success(period);
        }
        catch (InvalidOperationException ex)
        {
            return Result<AccountingPeriod>.ValidationFailure(ex.Message, "PERIOD_NOT_OPEN");
        }
    }
}
