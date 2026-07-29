using ERP.Application.Common;
using ERP.Domain.Modules.Accounting.Entities;

namespace ERP.Application.Modules.Accounting.Posting;

/// <summary>
/// Valida que un JournalEntry recién construido por <see cref="JournalFactory"/> sea apto para
/// contabilizar (ADR-026 §6/§8, Fase 3.5.5). Toda regla aquí opera exclusivamente sobre el estado
/// en memoria del propio <see cref="JournalEntry"/> — no consulta repositorios ni el Plan de
/// Cuentas (existencia/actividad de cuentas queda protegida hoy únicamente por la FK real de
/// <c>JournalEntryLine.AccountId</c> a nivel de base de datos, no por este Validator — ver riesgo
/// documentado en <c>docs/STATUS.md</c> Fase 3.5.5).
/// </summary>
internal sealed class JournalValidator
{
    private const string ValidationFailedCode = "VALIDATION_FAILED";

    public Result<JournalEntry> Validate(JournalEntry entry)
    {
        if (entry.Lines.Count < 2)
            return Result<JournalEntry>.ValidationFailure(
                "El asiento debe tener al menos dos líneas (una de Débito y una de Crédito).",
                ValidationFailedCode
            );

        foreach (var line in entry.Lines)
        {
            if (line.AccountId == Guid.Empty)
                return Result<JournalEntry>.ValidationFailure(
                    "Toda línea del asiento requiere una cuenta contable.",
                    ValidationFailedCode
                );

            var hasDebit = line.Debit > 0m;
            var hasCredit = line.Credit > 0m;
            if (hasDebit == hasCredit)
                return Result<JournalEntry>.ValidationFailure(
                    "Cada línea del asiento debe tener exactamente un monto en Débito o en Crédito.",
                    ValidationFailedCode
                );
        }

        var debitAccounts = entry
            .Lines.Where(l => l.Debit > 0m)
            .Select(l => l.AccountId)
            .ToHashSet();
        var creditAccounts = entry
            .Lines.Where(l => l.Credit > 0m)
            .Select(l => l.AccountId)
            .ToHashSet();
        if (debitAccounts.Overlaps(creditAccounts))
            return Result<JournalEntry>.ValidationFailure(
                "Una misma cuenta no puede recibir Débito y Crédito dentro del mismo asiento.",
                ValidationFailedCode
            );

        var totalDebit = entry.Lines.Sum(l => l.Debit);
        var totalCredit = entry.Lines.Sum(l => l.Credit);
        if (totalDebit == 0m && totalCredit == 0m)
            return Result<JournalEntry>.ValidationFailure(
                "El asiento no puede contabilizarse con montos en cero.",
                ValidationFailedCode
            );

        try
        {
            entry.EnsureBalanced();
        }
        catch (InvalidOperationException ex)
        {
            return Result<JournalEntry>.ValidationFailure(ex.Message, ValidationFailedCode);
        }

        return Result<JournalEntry>.Success(entry);
    }
}
