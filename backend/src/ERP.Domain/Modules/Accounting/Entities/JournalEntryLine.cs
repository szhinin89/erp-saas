using ERP.Domain.Common;

namespace ERP.Domain.Modules.Accounting.Entities;

/// <summary>
/// Línea de partida doble de un <see cref="JournalEntry"/> — entidad hija del mismo aggregate,
/// sin repositorio propio (ADR-026 §6, Fase 3.5.1/3.5.3). En el flujo real del Posting Engine
/// nace vía <see cref="JournalEntry.AddLine"/> — <c>Create()</c> es público, como el resto de las
/// entidades hija de este ERP (p. ej. <c>PurchaseInvoiceDetail</c>), para permitir pruebas de
/// dominio aisladas de la línea sin pasar por el aggregate completo.
/// </summary>
public sealed class JournalEntryLine : IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid JournalEntryId { get; private set; }
    public Guid AccountId { get; private set; }
    public string? Description { get; private set; }
    public decimal Debit { get; private set; }
    public decimal Credit { get; private set; }
    public short SortOrder { get; private set; }

    private JournalEntryLine() { }

    /// <summary>
    /// Invariante de partida doble a nivel de línea (ADR-026 §6): exactamente uno de
    /// Debit/Credit es mayor a cero — nunca ambos con valor, nunca ambos en cero. El invariante
    /// de balance del asiento completo (Σ Debit == Σ Credit) es responsabilidad del aggregate
    /// <see cref="JournalEntry"/>, no de esta entidad.
    /// </summary>
    public static JournalEntryLine Create(
        Guid journalEntryId, Guid tenantId, Guid accountId, string? description,
        decimal debit, decimal credit, short sortOrder)
    {
        if (accountId == Guid.Empty)
            throw new ArgumentException("La cuenta contable es obligatoria.", nameof(accountId));
        if (debit < 0)
            throw new ArgumentException("El monto de débito no puede ser negativo.", nameof(debit));
        if (credit < 0)
            throw new ArgumentException("El monto de crédito no puede ser negativo.", nameof(credit));
        if (debit > 0 && credit > 0)
            throw new InvalidOperationException("Una línea de asiento no puede tener Débito y Crédito simultáneamente.");
        if (debit == 0 && credit == 0)
            throw new InvalidOperationException("Una línea de asiento debe tener un monto en Débito o en Crédito.");

        return new JournalEntryLine
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            JournalEntryId = journalEntryId,
            AccountId = accountId,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Debit = debit,
            Credit = credit,
            SortOrder = sortOrder,
        };
    }
}
