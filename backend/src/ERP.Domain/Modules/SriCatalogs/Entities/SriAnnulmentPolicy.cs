using ERP.Domain.Exceptions;

namespace ERP.Domain.Modules.SriCatalogs.Entities;

/// <summary>Concepto global interno; datos oficiales por HasData/migraciones (ADR-037).</summary>
public sealed class SriAnnulmentPolicy
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
}

/// <summary>Versión normativa por fecha de emisión, sin scope tenant/company.</summary>
public sealed class SriAnnulmentPolicyVersion
{
    public Guid Id { get; set; }
    public Guid PolicyId { get; set; }
    public DateOnly? ValidFrom { get; set; }
    public DateOnly? ValidUntil { get; set; }
    public int? DayOfFollowingMonth { get; set; }
    public int? DaysAfterIssue { get; set; }
    public bool ExtendToNextBusinessDay { get; set; }
    public Guid NormativeSourceId { get; set; }

    public DateOnly OrdinaryDeadline(DateOnly issueDate)
    {
        if ((DayOfFollowingMonth is null) == (DaysAfterIssue is null))
            throw new DomainRuleViolationException("Política global SRI de anulación inválida.");
        if (DaysAfterIssue is int days && days > 0) return issueDate.AddDays(days);
        if (DayOfFollowingMonth is not int day || day < 1 || day > 28)
            throw new DomainRuleViolationException("Día de anulación SRI inválido.");
        var next = new DateOnly(issueDate.Year, issueDate.Month, 1).AddMonths(1);
        return new DateOnly(next.Year, next.Month, day);
    }

    /// <summary>Solo usar con calendario oficial aplicable (incluye feriados locales).
    /// Sin calendario, OrdinaryDeadline sigue siendo informativa, no un límite legal definitivo.</summary>
    public DateOnly Deadline(DateOnly issueDate, Func<DateOnly, bool> isBusinessDay)
    {
        var date = OrdinaryDeadline(issueDate);
        if (!ExtendToNextBusinessDay) return date;
        for (var offset = 0; offset < 366; offset++, date = date.AddDays(1))
            if (isBusinessDay(date)) return date;
        throw new DomainRuleViolationException("Calendario hábil SRI inválido.");
    }
}
