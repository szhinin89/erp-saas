using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;

namespace ERP.Application.Modules.Accounting.UseCases.AccountingPeriods;

/// <summary>
/// ACCOUNTING-07 — invariantes cross-aggregate de la estructura de períodos de una Company
/// (docs/architecture/accounting.md §8), compartidos por la creación manual y la generación del
/// ejercicio. El aggregate solo valida rango, ejercicio y número 1..12; aquí: sin solape, sin huecos
/// (contigüidad con el anterior y el siguiente), nunca detrás de un período Closed/Locked y número
/// correlativo dentro del ejercicio. El solape tiene además la exclusión PostgreSQL como defensa final.
/// </summary>
public static class AccountingPeriodStructure
{
    /// <summary>
    /// Primer ejercicio mensual (decisión ACCOUNTING-07): los ejercicios anteriores conservan el período
    /// anual sembrado y no se dividen retroactivamente.
    /// </summary>
    public const int MonthlyFromFiscalYear = 2027;

    public sealed record Slot(int FiscalYear, int PeriodNumber, DateOnly StartDate, DateOnly EndDate);

    /// <summary>Error de estructura: <see cref="IsOverlap"/> se responde como conflicto (409), el resto como validación (422).</summary>
    public sealed record Violation(string Message, bool IsOverlap);

    /// <summary>Los 12 meses naturales del ejercicio, PeriodNumber = mes.</summary>
    public static IReadOnlyList<Slot> MonthlyLayout(int fiscalYear) =>
        Enumerable
            .Range(1, AccountingPeriod.MaxPeriodsPerFiscalYear)
            .Select(month => new Slot(
                fiscalYear,
                month,
                new DateOnly(fiscalYear, month, 1),
                new DateOnly(fiscalYear, month, DateTime.DaysInMonth(fiscalYear, month))
            ))
            .ToList();

    public static Violation? Validate(IReadOnlyCollection<AccountingPeriod> existing, Slot slot)
    {
        var overlapping = existing.FirstOrDefault(p => p.StartDate <= slot.EndDate && p.EndDate >= slot.StartDate);
        if (overlapping is not null)
            return new(
                $"El rango de fechas se superpone con el período {Label(overlapping)} de esta empresa.",
                IsOverlap: true
            );

        var closedAfter = existing
            .Where(p => p.StartDate > slot.EndDate && p.Status != PeriodStatus.Open)
            .MinBy(p => p.StartDate);
        if (closedAfter is not null)
            return new(
                $"No se puede crear un período anterior al período {Label(closedAfter)}, que está {StatusLabel(closedAfter.Status)}.",
                IsOverlap: false
            );

        var previous = existing.Where(p => p.EndDate < slot.StartDate).MaxBy(p => p.EndDate);
        var next = existing.Where(p => p.StartDate > slot.EndDate).MinBy(p => p.StartDate);

        if (previous is not null && slot.StartDate != previous.EndDate.AddDays(1))
            return new(
                $"Los períodos deben ser contiguos: el período debe comenzar el {previous.EndDate.AddDays(1):yyyy-MM-dd}, "
                    + $"día siguiente al fin del período {Label(previous)}.",
                IsOverlap: false
            );
        if (next is not null && slot.EndDate.AddDays(1) != next.StartDate)
            return new(
                $"Los períodos deben ser contiguos: el período debe terminar el {next.StartDate.AddDays(-1):yyyy-MM-dd}, "
                    + $"día anterior al inicio del período {Label(next)}.",
                IsOverlap: false
            );

        var expectedNumber = ExpectedNumber(slot, previous, next);
        if (expectedNumber is { } expected && slot.PeriodNumber != expected)
            return new(
                $"El número de período debe ser {expected} dentro del ejercicio {slot.FiscalYear}.",
                IsOverlap: false
            );

        return null;
    }

    private static int? ExpectedNumber(
        Slot slot,
        AccountingPeriod? previous,
        AccountingPeriod? next
    )
    {
        if (previous is { } p && p.FiscalYear == slot.FiscalYear)
            return p.PeriodNumber + 1;
        if (next is { } n && n.FiscalYear == slot.FiscalYear)
            return n.PeriodNumber - 1;
        // Primer período del ejercicio que empieza el 1-ene (o que continúa el ejercicio anterior).
        if (slot.StartDate == new DateOnly(slot.FiscalYear, 1, 1) || previous is not null)
            return 1;
        // Primer período de la Company iniciado a mitad de año: el número queda a criterio (1..12).
        return null;
    }

    private static string Label(AccountingPeriod p) => $"{p.FiscalYear}-{p.PeriodNumber}";

    private static string StatusLabel(PeriodStatus status) =>
        status == PeriodStatus.Locked ? "bloqueado" : "cerrado";
}
