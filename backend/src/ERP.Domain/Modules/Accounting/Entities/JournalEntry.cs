using ERP.Domain.Common;
using ERP.Domain.Modules.Accounting.Enums;

namespace ERP.Domain.Modules.Accounting.Entities;

/// <summary>
/// Asiento contable. CompanyId-scoped obligatorio (ADR-026 §2). Incluye desde Fase 3.5.3 la
/// colección de líneas de partida doble (<see cref="JournalEntryLine"/>, ver <see cref="AddLine"/>)
/// y el invariante de balance (<see cref="EnsureBalanced"/>), invocado por <see cref="Post"/>
/// (Fase 5.2, ADR-026 §6/§8) junto con la asignación de <see cref="EntryNumber"/> (Fase 5.3,
/// mismo patrón arquitectónico de <c>DocumentSequence</c>/ADR-019, ver
/// <c>JournalEntrySequence</c>). <see cref="Reverse"/> (Fase 5.4, ADR-026 §6/§9) nunca modifica
/// los importes del asiento original — crea un asiento nuevo con las líneas invertidas y deja
/// trazabilidad bidireccional (<see cref="OriginalJournalEntryId"/>/<see cref="ReverseJournalEntryId"/>).
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
    /// <summary>
    /// Ejercicio fiscal del período contable de este asiento (denormalizado desde
    /// <c>AccountingPeriod.FiscalYear</c> al momento de <see cref="Create"/>). Necesario para el
    /// índice único (CompanyId, FiscalYear, EntryNumber) de la numeración definitiva (Fase 5.3) sin
    /// requerir un join contra <c>AccountingPeriod</c>.
    /// </summary>
    public int FiscalYear { get; private set; }
    public string SourceModule { get; private set; } = null!;
    public string SourceEventType { get; private set; } = null!;
    public Guid SourceEventId { get; private set; }
    public string Description { get; private set; } = null!;
    public JournalEntryStatus Status { get; private set; }
    public DateTime? PostedAtUtc { get; private set; }
    /// <summary>
    /// Numeración definitiva del asiento (Fase 5.3), correlativa por (CompanyId, FiscalYear) vía
    /// <c>JournalEntrySequence</c>. Nula hasta la publicación — <see cref="Post"/> es el único
    /// punto que la asigna, exactamente una vez (no existe setter público independiente), por lo
    /// que queda inmutable una vez publicado el asiento.
    /// </summary>
    public int? EntryNumber { get; private set; }
    /// <summary>
    /// Fase 5.4: si este asiento ES un reverso, apunta al asiento original que revierte. Nulo en
    /// cualquier asiento que no sea, en sí mismo, un reverso.
    /// </summary>
    public Guid? OriginalJournalEntryId { get; private set; }
    /// <summary>
    /// Fase 5.4: si este asiento FUE reversado, apunta al asiento de reverso que lo invalida.
    /// Nulo mientras el asiento no haya sido reversado — <see cref="Reverse"/> es el único punto
    /// que lo asigna, exactamente una vez (el chequeo de <see cref="Status"/> en
    /// <see cref="Reverse"/> impide una segunda asignación).
    /// </summary>
    public Guid? ReverseJournalEntryId { get; private set; }
    public DateTime? ReversedAtUtc { get; private set; }
    public string? ReverseReason { get; private set; }

    private readonly List<JournalEntryLine> _lines = new();
    public IReadOnlyCollection<JournalEntryLine> Lines => _lines.AsReadOnly();

    private JournalEntry() { }

    public static JournalEntry Create(
        Guid tenantId,
        Guid companyId,
        DateOnly entryDate,
        Guid accountingPeriodId,
        int fiscalYear,
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
            FiscalYear = fiscalYear,
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

    /// <summary>
    /// Publicación definitiva del asiento (Fase 5.2/5.3, ADR-026 §6/§7/§8): solo un asiento en
    /// <see cref="JournalEntryStatus.Draft"/> puede publicarse — impide tanto publicar un asiento
    /// ya <see cref="JournalEntryStatus.Posted"/> como uno ya <see cref="JournalEntryStatus.Reversed"/>
    /// (el reverso, cuando exista en una fase posterior, es la única vía para invalidar un asiento
    /// publicado). Invoca <see cref="EnsureBalanced"/> antes de cambiar de estado — un asiento
    /// desbalanceado nunca llega a <see cref="JournalEntryStatus.Posted"/> ni recibe
    /// <paramref name="entryNumber"/>. <paramref name="entryNumber"/> debe provenir siempre de
    /// <c>IJournalEntrySequenceRepository.ReserveNextNumberAsync</c> — este método no genera
    /// numeración, solo la asigna de forma atómica junto con el cambio de estado.
    /// </summary>
    public void Post(Guid postedBy, int entryNumber)
    {
        if (Status != JournalEntryStatus.Draft)
            throw new InvalidOperationException(
                $"Solo un asiento en estado Draft puede publicarse (estado actual: {Status}).");
        if (entryNumber < 1)
            throw new ArgumentOutOfRangeException(
                nameof(entryNumber), entryNumber, "El número de asiento debe ser mayor a cero.");

        EnsureBalanced();

        Status = JournalEntryStatus.Posted;
        PostedAtUtc = DateTime.UtcNow;
        EntryNumber = entryNumber;
        SetUpdated(postedBy);
    }

    /// <summary>
    /// Reverso contable (Fase 5.4, ADR-026 §6/§9): solo un asiento <see cref="JournalEntryStatus.Posted"/>
    /// puede reversarse — un <see cref="JournalEntryStatus.Draft"/> nunca tuvo efecto contable
    /// real (nada que revertir) y un asiento ya <see cref="JournalEntryStatus.Reversed"/> no puede
    /// reversarse de nuevo (el mismo chequeo de estado lo impide, sin bandera adicional). Nunca
    /// modifica los importes de <c>this</c>: crea y devuelve un <see cref="JournalEntry"/> nuevo,
    /// con las mismas líneas y Débito/Crédito invertidos (balanceado automáticamente, porque
    /// invertir un asiento ya balanceado preserva Σ Debit == Σ Credit), dejando <c>this</c>
    /// intacto salvo por su cambio de <see cref="Status"/> y los campos de trazabilidad. El
    /// bloqueo por período cerrado/bloqueado es responsabilidad de la capa de Application (mismo
    /// patrón que <c>PostingPeriodGuard</c> aplica para <see cref="Post"/>) — este método no
    /// conoce <c>AccountingPeriod</c>. <paramref name="reverseEntryNumber"/> debe provenir
    /// siempre de <c>IJournalEntrySequenceRepository.ReserveNextNumberAsync</c>, igual que en
    /// <see cref="Post"/>.
    /// </summary>
    public JournalEntry Reverse(Guid reversedBy, int reverseEntryNumber, string reason)
    {
        if (Status != JournalEntryStatus.Posted)
            throw new InvalidOperationException(
                $"Solo un asiento Posted puede reversarse (estado actual: {Status}).");
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("El motivo del reverso es obligatorio.", nameof(reason));

        var trimmedReason = reason.Trim();

        var reversal = Create(
            TenantId, CompanyId, EntryDate, AccountingPeriodId, FiscalYear,
            "Accounting", "Reversal", Id,
            $"Reverso del asiento N° {EntryNumber} — {trimmedReason}", reversedBy);

        foreach (var line in _lines)
            reversal.AddLine(line.AccountId, line.Description, line.Credit, line.Debit);

        reversal.Post(reversedBy, reverseEntryNumber);
        reversal.OriginalJournalEntryId = Id;

        Status = JournalEntryStatus.Reversed;
        ReversedAtUtc = DateTime.UtcNow;
        ReverseReason = trimmedReason;
        ReverseJournalEntryId = reversal.Id;
        SetUpdated(reversedBy);

        return reversal;
    }
}
