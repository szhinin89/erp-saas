using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.InitialLoad.Constants;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Seeding.Steps;

/// <summary>
/// IL-7A — configuración contable de la apertura de la Carga Inicial: cuenta puente patrimonial
/// "3.1.04.001 Saldos de apertura" (bajo el agrupador "3.1.04") y las reglas
/// <c>InitialLoad/OpeningInventory|OpeningReceivables|OpeningPayables</c>, que una empresa nueva
/// recibe por <see cref="ExecuteAsync"/>. Si 3.1.04 o 3.1.04.001 ya existen con otro sentido (no son
/// "Saldos de apertura" patrimonial acreedora) no se siembra ni la cuenta ni las reglas: nunca se
/// contabiliza la apertura contra una cuenta ajena.
/// </summary>
public sealed partial class AccountingBootstrapStep
{
    internal const string OpeningBalanceGroupAccountCode = "3.1.04";
    internal const string OpeningBalanceAccountCode = "3.1.04.001";
    internal const string OpeningBalanceAccountName = "Saldos de apertura";

    private static bool IsOpeningBalanceAccountCode(string code) =>
        code is OpeningBalanceGroupAccountCode or OpeningBalanceAccountCode;

    private static bool IsOpeningBalanceRule(MinimalPostingRule rule) =>
        rule.SourceModule == OpeningBalancePostingFacts.SourceModule;

    /// <summary>
    /// <c>null</c> si 3.1.04/3.1.04.001 no existen o ya son la cuenta puente (patrimonial,
    /// acreedora, "Saldos de apertura"); si no, la descripción del conflicto.
    /// </summary>
    internal static string? OpeningBalanceAccountConflict(
        IEnumerable<(string Code, string Name, AccountType Type, AccountNature Nature)> accounts
    )
    {
        var conflicts = accounts
            .Where(a => IsOpeningBalanceAccountCode(a.Code))
            .Where(a =>
                a.Type != AccountType.Equity
                || a.Nature != AccountNature.Credit
                || !string.Equals(
                    a.Name.Trim(),
                    OpeningBalanceAccountName,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            .OrderBy(a => a.Code, StringComparer.Ordinal)
            .Select(a => $"{a.Code} '{a.Name}' ({a.Type}/{a.Nature})")
            .ToList();
        return conflicts.Count == 0
            ? null
            : $"código ocupado por {string.Join(", ", conflicts)}";
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Opening balance account not seeded for company {CompanyId}: {Conflict}. "
            + "InitialLoad opening posting rules are not seeded either (no bridge account)."
    )]
    private partial void LogOpeningBalanceAccountConflict(string conflict, Guid companyId);
}
