using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;

namespace ERP.Application.Modules.Accounting.Continuity;

/// <summary>
/// ACCOUNTING-12D — reglas puras del cierre anual compartidas por la generación del asiento, su
/// publicación y el readiness del último período (accounting.md §26). El ejercicio es el año calendario
/// (§22); las cuentas nominales son Ingresos, Costos y Gastos.
/// </summary>
public static class FiscalYearClosingRules
{
    /// <summary>Cuenta de contrapartida del plan de cuentas de la Company (sembrada por el bootstrap).</summary>
    public const string ResultAccountCode = "3.1.03.001";

    public static (DateOnly From, DateOnly To) YearRange(int fiscalYear) =>
        (new DateOnly(fiscalYear, 1, 1), new DateOnly(fiscalYear, 12, 31));

    public static bool IsLastPeriodOfYear(AccountingPeriod period) =>
        period.EndDate == YearRange(period.FiscalYear).To;

    public static bool IsNominal(Account account) =>
        account.AccountType is AccountType.Income or AccountType.Cost or AccountType.Expense;

    public static IReadOnlyList<Guid> NominalAccountIds(IEnumerable<Account> accounts) =>
        accounts.Where(IsNominal).Select(a => a.Id).ToList();

    /// <summary>
    /// Líneas del cierre: cada cuenta nominal con saldo se lleva a cero (saldo acreedor → Debe, saldo
    /// deudor → Haber) y la diferencia neta va a la cuenta de resultado (utilidad → Haber, pérdida → Debe).
    /// Importes redondeados con <c>AccountingDecimals</c>; el resultado es la suma exacta de las líneas, así
    /// que el asiento balancea sin residuo. <c>Result</c> = Ingresos − Costos − Gastos.
    /// </summary>
    public static (IReadOnlyList<ManualJournalLine> Lines, decimal Result) BuildLines(
        IReadOnlyList<Account> nominalAccounts,
        IReadOnlyDictionary<Guid, (decimal TotalDebit, decimal TotalCredit)> totals,
        Guid resultAccountId,
        int fiscalYear,
        int decimals
    )
    {
        var description = $"Cierre del ejercicio {fiscalYear}";
        var lines = new List<ManualJournalLine>();
        foreach (var account in nominalAccounts.OrderBy(a => a.Code.Value, ERP.Domain.Modules.Accounting.ValueObjects.AccountCodeComparer.Instance))
        {
            if (!totals.TryGetValue(account.Id, out var t))
                continue;
            var balance = Math.Round(t.TotalDebit - t.TotalCredit, decimals, MidpointRounding.AwayFromZero);
            if (balance > 0)
                lines.Add(new ManualJournalLine(account.Id, description, 0m, balance));
            else if (balance < 0)
                lines.Add(new ManualJournalLine(account.Id, description, -balance, 0m));
        }

        var result = lines.Sum(l => l.Debit) - lines.Sum(l => l.Credit);
        var resultDescription = $"Resultado del ejercicio {fiscalYear}";
        if (result > 0)
            lines.Add(new ManualJournalLine(resultAccountId, resultDescription, 0m, result));
        else if (result < 0)
            lines.Add(new ManualJournalLine(resultAccountId, resultDescription, -result, 0m));
        return (lines, result);
    }

    /// <summary>Mismas cuentas e importes por lado, sin importar el orden.</summary>
    public static bool SameLines(IEnumerable<JournalEntryLine> current, IEnumerable<ManualJournalLine> expected)
    {
        static IEnumerable<(Guid, decimal, decimal)> Key(IEnumerable<(Guid A, decimal D, decimal C)> l) =>
            l.OrderBy(x => x.A).ThenBy(x => x.D).ThenBy(x => x.C);
        return Key(current.Select(l => (l.AccountId, l.Debit, l.Credit)))
            .SequenceEqual(Key(expected.Select(l => (l.AccountId, l.Debit, l.Credit))));
    }
}
