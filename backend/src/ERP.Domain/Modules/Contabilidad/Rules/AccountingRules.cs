using ERP.Domain.Modules.Contabilidad.Entities;

namespace ERP.Domain.Modules.Contabilidad.Rules;

public static class AccountingRules
{
    public static void ValidateBalance(IReadOnlyList<JournalEntryLine> lines)
    {
        if (lines.Count < 2)
            throw new InvalidOperationException("Un asiento debe tener al menos 2 lineas.");

        var totalDebit  = lines.Sum(l => l.Debit.Amount);
        var totalCredit = lines.Sum(l => l.Credit.Amount);

        if (totalDebit != totalCredit)
            throw new InvalidOperationException(
                $"El asiento no cuadra: Debe={totalDebit} Haber={totalCredit}");
    }
}
