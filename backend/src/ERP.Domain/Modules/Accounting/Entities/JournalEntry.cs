using ERP.Domain.Common;
using ERP.Domain.Modules.Accounting.Enums;

namespace ERP.Domain.Modules.Accounting.Entities;

/// <summary>
/// Asiento contable. CompanyId-scoped obligatorio (ADR-026 §2). Incluye desde Fase 3.5.3 la
/// colección de líneas de partida doble (<see cref="JournalEntryLine"/>, ver <see cref="AddLine"/>)
/// y el invariante de balance (<see cref="EnsureBalanced"/>) — ninguno de los dos tiene todavía
/// un consumidor real: <c>JournalFactory</c> sigue construyendo únicamente el encabezado (sin
/// líneas) y nada invoca <c>EnsureBalanced()</c> todavía. Numeración (<c>JournalEntrySequence</c>,
/// ADR-026 §7), <c>Post()</c> y <c>Reverse()</c> completo quedan explícitamente fuera de esta fase
/// (ADR-026 §6, §8) — pertenecen a un incremento posterior del Posting Engine.
/// </summary>
/// <remarks>
/// <c>Create()</c> no levanta ningún evento de dominio todavía — a diferencia de
/// <see cref="Account"/>/<see cref="AccountingPeriod"/>/<see cref="PostingRule"/>, este
/// aggregate no tiene aún un flujo de creación formal: el <c>Create()</c> actual es un
/// placeholder de identidad, no la vía real por la que nacerán los asientos productivos (que
/// llegarán vía el Posting Engine a partir de Domain Events de Sales/Purchases/Caja/Inventory,
/// ADR-026 §3). Levantar un evento de creación ahora sería un evento sin productor real —
/// se agrega junto con <c>Post()</c>/numeración cuando el flujo formal exista.
/// </remarks>
public sealed class JournalEntry : AuditableEntity, ITenantScopedEntity, ICompanyOperationalEntity
{
    public Guid CompanyId { get; private set; }
    public DateOnly EntryDate { get; private set; }
    public Guid AccountingPeriodId { get; private set; }
    public string SourceModule { get; private set; } = null!;
    public string SourceEventType { get; private set; } = null!;
    public Guid SourceEventId { get; private set; }
    public string Description { get; private set; } = null!;
    public JournalEntryStatus Status { get; private set; }

    private readonly List<JournalEntryLine> _lines = new();
    public IReadOnlyCollection<JournalEntryLine> Lines => _lines.AsReadOnly();

    private JournalEntry() { }

    public static JournalEntry Create(
        Guid tenantId,
        Guid companyId,
        DateOnly entryDate,
        Guid accountingPeriodId,
        string sourceModule,
        string sourceEventType,
        Guid sourceEventId,
        string description,
        Guid createdBy)
    {
        if (string.IsNullOrWhiteSpace(sourceModule))
            throw new ArgumentException("El módulo de origen es obligatorio.", nameof(sourceModule));
        if (string.IsNullOrWhiteSpace(sourceEventType))
            throw new ArgumentException("El tipo de hecho contable de origen es obligatorio.", nameof(sourceEventType));
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("La descripción es obligatoria.", nameof(description));

        var entry = new JournalEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            EntryDate = entryDate,
            AccountingPeriodId = accountingPeriodId,
            SourceModule = sourceModule.Trim(),
            SourceEventType = sourceEventType.Trim(),
            SourceEventId = sourceEventId,
            Description = description.Trim(),
            Status = JournalEntryStatus.Draft,
        };
        entry.SetCreated(createdBy);
        return entry;
    }

    /// <summary>
    /// Agrega una línea de partida doble al asiento. El invariante de línea (exactamente uno de
    /// Debit/Credit mayor a cero) lo valida <see cref="JournalEntryLine.Create"/> — este método no
    /// valida balance en cada llamada, porque el balance es un invariante del asiento completo,
    /// no de una línea individual (verificar tras agregar una sola línea siempre fallaría). Ver
    /// <see cref="EnsureBalanced"/> para el invariante de agregado.
    /// </summary>
    public void AddLine(Guid accountId, string? description, decimal debit, decimal credit)
    {
        var line = JournalEntryLine.Create(Id, TenantId, accountId, description, debit, credit, (short)_lines.Count);
        _lines.Add(line);
    }

    /// <summary>
    /// Invariante de partida doble a nivel de agregado (ADR-026 §6): Σ Debit == Σ Credit. Sin
    /// consumidor todavía (ver remarks de la clase) — con <see cref="Lines"/> vacío (flujo actual
    /// del Posting Engine, que no llama a <see cref="AddLine"/>) se cumple trivialmente (0 == 0).
    /// </summary>
    public void EnsureBalanced()
    {
        var totalDebit = _lines.Sum(l => l.Debit);
        var totalCredit = _lines.Sum(l => l.Credit);
        if (totalDebit != totalCredit)
            throw new InvalidOperationException(
                $"El asiento no está balanceado: Débitos ({totalDebit:F2}) distintos de Créditos ({totalCredit:F2}).");
    }
}
