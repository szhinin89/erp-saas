using ERP.Domain.Common;

namespace ERP.Domain.Modules.Accounting.Entities;

/// <summary>
/// Período contable mensual. Cuando está cerrado (<see cref="IsClosed"/> = true)
/// no se pueden registrar ni modificar asientos en él.
/// </summary>
public sealed class AccountingPeriod : AuditableEntity, ISubscriberScopedEntity
{
    public const int NameMaxLen = 50;

    public int     Year       { get; private set; }
    public int     Month      { get; private set; }
    public string  Name       { get; private set; } = null!;
    public bool    IsClosed   { get; private set; }
    public Guid?   ClosedBy   { get; private set; }
    public DateTime? ClosedAt { get; private set; }

    private AccountingPeriod() { }

    public static AccountingPeriod Create(
        Guid   subscriberId,
        int    year,
        int    month,
        Guid   createdBy)
    {
        if (year < 2000 || year > 2100)
            throw new ArgumentException("Year fuera de rango válido.", nameof(year));
        if (month < 1 || month > 12)
            throw new ArgumentException("Month debe estar entre 1 y 12.", nameof(month));

        var p = new AccountingPeriod
        {
            Id           = Guid.NewGuid(),
            SubscriberId = subscriberId,
            Year         = year,
            Month        = month,
            Name         = $"{year:D4}-{month:D2}",
            IsClosed     = false,
        };
        p.SetCreated(createdBy);
        return p;
    }

    public void Close(Guid userId)
    {
        if (IsClosed)
            throw new InvalidOperationException($"El período {Name} ya está cerrado.");
        IsClosed = true;
        ClosedBy = userId;
        ClosedAt = DateTime.UtcNow;
        SetUpdated(userId);
    }

    public void Reopen(Guid userId)
    {
        if (!IsClosed)
            throw new InvalidOperationException($"El período {Name} no está cerrado.");
        IsClosed = false;
        ClosedBy = null;
        ClosedAt = null;
        SetUpdated(userId);
    }
}
