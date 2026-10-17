using ERP.Application.Common;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Accounting.Entities;

namespace ERP.Application.Modules.Accounting.Posting;

/// <summary>
/// Adapta los invariantes únicos de JournalEntry a Result para el pipeline automático.
/// Las cuentas y el período se validan mediante sus guardas comunes antes de publicar.
/// </summary>
internal sealed class JournalValidator
{
    private const string ValidationFailedCode = "VALIDATION_FAILED";

    public Result<JournalEntry> Validate(JournalEntry entry)
    {
        try
        {
            entry.EnsurePostable();
        }
        catch (DomainRuleViolationException ex)
        {
            return Result<JournalEntry>.ValidationFailure(ex.Message, ValidationFailedCode);
        }
        catch (ArgumentException ex)
        {
            return Result<JournalEntry>.ValidationFailure(ex.Message, ValidationFailedCode);
        }

        return Result<JournalEntry>.Success(entry);
    }
}
