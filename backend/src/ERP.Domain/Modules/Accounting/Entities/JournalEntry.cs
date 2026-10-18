using ERP.Domain.Common;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Accounting.Enums;

namespace ERP.Domain.Modules.Accounting.Entities;

/// <summary>
/// Asiento contable. CompanyId-scoped obligatorio (ADR-026 §2). Incluye desde Fase 3.5.3 la
/// colección de líneas de partida doble (<see cref="JournalEntryLine"/>, ver <see cref="AddLine"/>)
/// y el invariante de balance (<see cref="EnsureBalanced"/>), invocado por <see cref="Post"/>
/// (Fase 5.2, ADR-026 §6/§8) junto con la asignación de <see cref="EntryNumber"/> (Fase 5.3,
/// mismo patrón arquitectónico de <c>DocumentSequence</c>/ADR-019, ver
/// <c>JournalEntrySequence</c>). <see cref="Reverse"/> (Fase 5.4, ADR-026 §6/§9) nunca modifica
/// los importes del asiento original — crea un asiento nuevo con las líneas invertidas y conserva
/// referencia al original sin modificarlo; la relación inversa se consulta desde el reverso.
/// Trazabilidad: <see cref="OriginalJournalEntryId"/> en el reverso (único por original).
/// </summary>
/// <remarks>
/// El motor automático y los comandos manuales comparten este agregado. Los comandos manuales
/// registran Entity Audit dentro de su transacción; no producen un hecho comercial ficticio.
/// </remarks>
public sealed class JournalEntry : AuditableEntity, ITenantScopedEntity, ICompanyOperationalEntity
{
    public Guid CompanyId { get; private set; }
    public DateOnly EntryDate { get; private set; }
    public Guid? AccountingPeriodId { get; private set; }
    public const string ManualModule = "Accounting";
    public const string ManualFact = "ManualJournalEntry";
    public bool IsManual => SourceModule == ManualModule && SourceEventType == ManualFact;

    /// <summary>
    /// ACCOUNTING-12D — asiento de cierre anual: generado por el sistema (líneas calculadas, no editables)
    /// y publicado/descartado/reversado por el usuario con el mismo flujo de los manuales.
    /// </summary>
    public const string FiscalYearClosingFact = "FiscalYearClosing";
    public bool IsFiscalYearClosing => SourceModule == ManualModule && SourceEventType == FiscalYearClosingFact;

    /// <summary>Asientos cuya publicación decide un usuario (manual o cierre anual).</summary>
    public bool IsUserPublished => IsManual || IsFiscalYearClosing;
    public string? Reference { get; private set; }
    public int Version { get; private set; }
    public DateTime? DiscardedAtUtc { get; private set; }
    public Guid? DiscardedBy { get; private set; }
    public string? DiscardReason { get; private set; }

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

    public DateTime? ReversedAtUtc { get; private set; }
    public string? ReverseReason { get; private set; }

    /// <summary>Longitud de <c>journal_entries.description</c> (JournalEntryConfiguration).</summary>
    public const int DescriptionMaxLength = 500;

    /// <summary>Longitud de <c>journal_entries.reverse_reason</c>: estándar del motivo de reverso.</summary>
    public const int ReverseReasonMaxLength = 500;

    private readonly List<JournalEntryLine> _lines = new();
    public IReadOnlyCollection<JournalEntryLine> Lines => _lines.AsReadOnly();

    private JournalEntry() { }

    public static JournalEntry Create(
        Guid tenantId,
        Guid companyId,
        DateOnly entryDate,
        Guid? accountingPeriodId,
        int fiscalYear,
        string sourceModule,
        string sourceEventType,
        Guid sourceEventId,
        string description,
        Guid createdBy
    )
    {
        if (string.IsNullOrWhiteSpace(sourceModule))
            throw new ArgumentException(
                "El módulo de origen es obligatorio.",
                nameof(sourceModule)
            );
        if (string.IsNullOrWhiteSpace(sourceEventType))
            throw new ArgumentException(
                "El tipo de hecho contable de origen es obligatorio.",
                nameof(sourceEventType)
            );
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
        EnsureDraft();
        var line = JournalEntryLine.Create(
            Id,
            TenantId,
            accountId,
            description,
            debit,
            credit,
            (short)_lines.Count
        );
        _lines.Add(line);
    }

    /// <summary>
    /// Invariante exacto de balance. EnsurePostable agrega los requisitos de estructura y líneas.
    /// </summary>
    /// <summary>
    /// SALES-JOURNAL-ENTRY-SILENT-FAILURE-01 Lote 2 introdujo aquí una tolerancia temporal de
    /// hasta 1 centavo para destrabar asientos de Ventas mientras se investigaba la causa raíz —
    /// ver historial de git blame de este método para el comentario original. Esa causa raíz
    /// (<c>SalesInvoiceDetail.LineSubtotal</c> sin redondear alimentando
    /// <c>SalesInvoice.Subtotal</c>/<c>AuthorizedSubtotal</c>, mientras <c>TaxInclusiveTotal</c>
    /// sí redondeaba por línea) fue corregida de raíz en
    /// SALES-INVOICE-ROUNDING-SUBTOTAL-GRANDTOTAL-01 (Fase 6A): <c>SalesInvoiceDetail
    /// .LineSubtotalRounded</c> ahora deriva Subtotal de <c>TaxableBase</c> (ya redondeada a 2
    /// decimales por línea, mismo criterio que <c>TaxInclusiveTotal</c>) — con eso,
    /// <c>AuthorizedSubtotal - AuthorizedTotalDiscount + AuthorizedTotalTax</c> es exactamente
    /// igual a <c>AuthorizedGrandTotal</c> (sin IRBPNR — gap distinto, ya documentado, fuera de
    /// alcance). No se encontró ningún otro escenario legítimo de residuo de sub-centavo en el
    /// sistema (cuotas de PaymentSchedule: la última cuota absorbe el remanente exacto, no toca
    /// JournalEntry; el resto de los traductores de Posting usan montos ya resueltos en
    /// numeric(18,2) por su módulo de origen) — por lo tanto esta tolerancia se revierte a
    /// igualdad EXACTA. Cualquier diferencia real (cuenta equivocada, monto mal mapeado, un futuro
    /// caso de prorrateo con residuo legítimo) debe seguir bloqueando la publicación del asiento,
    /// nunca aceptarse en silencio.
    /// </summary>
    public void EnsureBalanced()
    {
        var totalDebit = _lines.Sum(l => l.Debit);
        var totalCredit = _lines.Sum(l => l.Credit);
        if (totalDebit != totalCredit)
            throw new DomainRuleViolationException(
                $"El asiento no está balanceado: Débitos ({totalDebit:F2}) distintos de Créditos ({totalCredit:F2})."
            );
    }

    /// <summary>
    /// Publicación definitiva del asiento (Fase 5.2/5.3, ADR-026 §6/§7/§8): solo un asiento en
    /// <see cref="JournalEntryStatus.Draft"/> puede publicarse — impide tanto publicar un asiento
    /// ya <see cref="JournalEntryStatus.Posted"/> como uno <see cref="JournalEntryStatus.Discarded"/>
    /// (el reverso es la única vía para invalidar un asiento publicado). Invoca <see cref="EnsureBalanced"/> antes de cambiar de estado — un asiento
    /// desbalanceado nunca llega a <see cref="JournalEntryStatus.Posted"/> ni recibe
    /// <paramref name="entryNumber"/>. <paramref name="entryNumber"/> debe provenir siempre de
    /// <c>IJournalEntrySequenceRepository.ReserveNextNumberAsync</c> — este método no genera
    /// numeración, solo la asigna de forma atómica junto con el cambio de estado.
    /// </summary>
    public void Post(Guid postedBy, int entryNumber)
    {
        if (Status != JournalEntryStatus.Draft)
            throw new DomainRuleViolationException(
                $"Solo un asiento en estado Draft puede publicarse (estado actual: {Status})."
            );
        if (entryNumber < 1)
            throw new ArgumentOutOfRangeException(
                nameof(entryNumber),
                entryNumber,
                "El número de asiento debe ser mayor a cero."
            );

        EnsurePostable();
        if (AccountingPeriodId is null || AccountingPeriodId == Guid.Empty)
            throw new DomainRuleViolationException("El asiento requiere un período contable.");

        Status = JournalEntryStatus.Posted;
        PostedAtUtc = DateTime.UtcNow;
        EntryNumber = entryNumber;
        if (IsUserPublished) Version++;
        SetUpdated(postedBy);
    }

    public void EnsurePostable()
    {
        if (_lines.Count < 2)
            throw new DomainRuleViolationException("El asiento debe tener al menos dos líneas.");
        foreach (var line in _lines) line.EnsureValid();
        EnsureAccountSides(_lines);
        EnsureBalanced();
    }

    public static JournalEntry CreateManual(Guid tenantId, Guid companyId, Guid identity,
        DateOnly date, Guid? periodId, int fiscalYear, string concept, string? reference,
        IReadOnlyList<ManualJournalLine> lines, int decimals, Guid actor)
    {
        if (tenantId == Guid.Empty || companyId == Guid.Empty || identity == Guid.Empty)
            throw new ArgumentException("La identidad y el ámbito son obligatorios.");
        var entry = Create(tenantId, companyId, date, periodId, fiscalYear,
            ManualModule, ManualFact, identity, concept, actor);
        entry.ReplaceManual(date, periodId, fiscalYear, concept, reference, lines, decimals, actor);
        return entry;
    }

    /// <summary>
    /// ACCOUNTING-12D — Draft del cierre anual con las líneas calculadas por Application. Debe poder
    /// publicarse tal cual (balance, mínimo de líneas, lados), por eso se valida al crearlo.
    /// </summary>
    public static JournalEntry CreateFiscalYearClosing(Guid tenantId, Guid companyId, Guid identity,
        DateOnly date, Guid periodId, int fiscalYear, string concept, IReadOnlyList<ManualJournalLine> lines,
        int decimals, Guid actor)
    {
        if (tenantId == Guid.Empty || companyId == Guid.Empty || identity == Guid.Empty || periodId == Guid.Empty)
            throw new ArgumentException("La identidad, el período y el ámbito son obligatorios.");
        var entry = Create(tenantId, companyId, date, periodId, fiscalYear,
            ManualModule, FiscalYearClosingFact, identity, concept, actor);
        entry.ApplyDraft(date, periodId, fiscalYear, concept, null, lines, decimals, actor);
        entry.EnsurePostable();
        return entry;
    }

    public void ReplaceManual(DateOnly date, Guid? periodId, int fiscalYear, string concept,
        string? reference, IReadOnlyList<ManualJournalLine> lines, int decimals, Guid actor)
    {
        EnsureDraft();
        if (!IsManual) throw new DomainRuleViolationException("Solo puede editarse un asiento manual.");
        ApplyDraft(date, periodId, fiscalYear, concept, reference, lines, decimals, actor);
    }

    private void ApplyDraft(DateOnly date, Guid? periodId, int fiscalYear, string concept,
        string? reference, IReadOnlyList<ManualJournalLine> lines, int decimals, Guid actor)
    {
        if (date == default || string.IsNullOrWhiteSpace(concept) || concept.Trim().Length > DescriptionMaxLength
            || reference?.Length > DescriptionMaxLength || lines.Count > short.MaxValue)
            throw new ArgumentException("Fecha, concepto, referencia o líneas inválidos.");
        // Validate the complete replacement before mutating the aggregate.
        var replacement = lines.Select((l, index) =>
        {
            if (l.Description?.Length > DescriptionMaxLength || l.Debit > 9999999999999999.99m
                || l.Credit > 9999999999999999.99m
                || Math.Round(l.Debit, decimals, MidpointRounding.AwayFromZero) != l.Debit
                || Math.Round(l.Credit, decimals, MidpointRounding.AwayFromZero) != l.Credit)
                throw new ArgumentException("Importe o descripción fuera de la precisión/capacidad contable.");
            return JournalEntryLine.Create(Id, TenantId, l.AccountId, l.Description, l.Debit, l.Credit, (short)index);
        }).ToList();
        EnsureAccountSides(replacement);
        EntryDate = date;
        AccountingPeriodId = periodId;
        FiscalYear = fiscalYear;
        Description = concept.Trim();
        Reference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim();
        _lines.Clear();
        _lines.AddRange(replacement);
        Version++;
        SetUpdated(actor);
    }

    public void SetManualPostingPeriod(Guid periodId, int fiscalYear)
    {
        EnsureManualDraft();
        AccountingPeriodId = periodId;
        FiscalYear = fiscalYear;
    }

    public void Discard(Guid actor, string reason)
    {
        EnsureManualDraft();
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > ReverseReasonMaxLength)
            throw new ArgumentException("El motivo es obligatorio y admite hasta 500 caracteres.");
        Status = JournalEntryStatus.Discarded;
        DiscardReason = reason.Trim();
        DiscardedBy = actor;
        DiscardedAtUtc = DateTime.UtcNow;
        Version++;
        SetUpdated(actor);
    }

    private void EnsureDraft()
    {
        if (Status != JournalEntryStatus.Draft)
            throw new DomainRuleViolationException("Solo un asiento Draft puede modificarse.");
    }

    private static void EnsureAccountSides(IReadOnlyCollection<JournalEntryLine> lines)
    {
        var debitAccounts = lines.Where(l => l.Debit > 0).Select(l => l.AccountId).ToHashSet();
        if (lines.Any(l => l.Credit > 0 && debitAccounts.Contains(l.AccountId)))
            throw new DomainRuleViolationException("Una misma cuenta no puede recibir Débito y Crédito dentro del mismo asiento.");
    }

    private void EnsureManualDraft()
    {
        EnsureDraft();
        if (!IsUserPublished) throw new DomainRuleViolationException("Solo puede editarse un asiento manual.");
    }

    /// <summary>Crea el espejo histórico sin modificar este asiento ni sus líneas.</summary>
    public JournalEntry Reverse(Guid reversedBy, int reverseEntryNumber, string reason,
        DateOnly effectiveDate, Guid? destinationPeriodId, int destinationFiscalYear)
    {
        if (Status != JournalEntryStatus.Posted)
            throw new DomainRuleViolationException("Solo un asiento Posted puede reversarse.");
        if (effectiveDate == default || destinationPeriodId is null || destinationPeriodId == Guid.Empty)
            throw new ArgumentException("La fecha efectiva y el período destino son obligatorios.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > ReverseReasonMaxLength)
            throw new ArgumentException("El motivo del reverso es obligatorio y admite hasta 500 caracteres.", nameof(reason));

        var reversal = Create(TenantId, CompanyId, effectiveDate, destinationPeriodId,
            destinationFiscalYear, "Accounting", "Reversal", Id,
            Truncate($"Reverso del asiento N° {EntryNumber} — {reason.Trim()}", DescriptionMaxLength), reversedBy);
        foreach (var line in _lines.OrderBy(l => l.SortOrder))
            reversal._lines.Add(JournalEntryLine.Create(reversal.Id, TenantId, line.AccountId,
                line.Description, line.Credit, line.Debit, line.SortOrder));
        reversal.Post(reversedBy, reverseEntryNumber);
        reversal.OriginalJournalEntryId = Id;
        reversal.ReverseReason = reason.Trim();
        reversal.ReversedAtUtc = reversal.PostedAtUtc;
        return reversal;
    }

    private static string Truncate(string value, int max) => value.Length > max ? value[..max] : value;
}

public sealed record ManualJournalLine(Guid AccountId, string? Description, decimal Debit, decimal Credit);
