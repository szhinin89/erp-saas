using ERP.Domain.Common;
using ERP.Domain.Modules.Contabilidad.Rules;
using ERP.Domain.Modules.Contabilidad.ValueObjects;
using ERP.Domain.Modules.Contabilidad.Events;

namespace ERP.Domain.Modules.Contabilidad.Entities;

public class JournalEntry : DocumentEntity
{
    private readonly List<JournalEntryLine> _lines = new();

    public string Reference { get; private set; } = null!;
    public DateTime Date { get; private set; }
    public string Description { get; private set; } = null!;

    public IReadOnlyList<JournalEntryLine> Lines => _lines.AsReadOnly();

    private JournalEntry() { }

    public static JournalEntry Create(
        Guid tenantId,
        string reference,
        DateTime date,
        string description,
        Guid createdBy)
    {
        var entry = new JournalEntry
        {
            Id          = Guid.NewGuid(),
            TenantId    = tenantId,
            Reference   = reference,
            Date        = date,
            Description = description,
        };

        entry.SetCreated(createdBy);
        return entry;
    }

    public void AddLine(Guid accountId, Money debit, Money credit)
    {
        if (!IsEditable)
            throw new InvalidOperationException(
                "No se puede modificar un asiento contabilizado o anulado.");

        _lines.Add(JournalEntryLine.Create(Id, TenantId, accountId, debit, credit));
    }

    /// <summary>
    /// Valida el balance contable antes de confirmar el asiento.
    /// </summary>
    public new void Post(Guid userId)
    {
        AccountingRules.ValidateBalance(_lines);
        base.Post(userId);
        RaiseDomainEvent(new JournalEntryCreatedEvent(Id, TenantId));
    }
}