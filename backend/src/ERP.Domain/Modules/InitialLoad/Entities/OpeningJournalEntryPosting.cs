using ERP.Domain.Common;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.InitialLoad.Constants;
using ERP.Domain.Modules.InitialLoad.Enums;

namespace ERP.Domain.Modules.InitialLoad.Entities;

/// <summary>
/// IL-8A — una VERSIÓN del ASI de apertura de una empresa: el asiento que reclasifica la cuenta
/// puente "Saldos de apertura" a sus cuentas definitivas, a la fecha <c>Company.OpeningBalanceDate</c>.
/// Solo una versión es vigente por empresa (<see cref="IsCurrent"/>, índice único parcial); las
/// reemplazadas se conservan como historial y nunca se borran ni se reutilizan. Cada versión tiene
/// su propio <see cref="Entity.Id"/>, que es el <c>SourceEventId</c> del hecho
/// <c>InitialLoad</c>/<see cref="OpeningBalancePostingFacts.OpeningJournalEntry"/>: los reintentos de
/// una versión conservan el Id (la idempotencia del Posting Engine impide un segundo asiento) y un
/// reemplazo es una versión nueva con Id nuevo.
/// <see cref="OpeningBalancePostingStatus.Pending"/> → <see cref="OpeningBalancePostingStatus.Posted"/>
/// (terminal) o <see cref="OpeningBalancePostingStatus.Failed"/> (reintentable con las mismas o nuevas
/// líneas). La versión vigente publicada vuelve inmutable la fecha de apertura. Corrección (reverso
/// completo + versión nueva) = IL-8B.
/// </summary>
public sealed class OpeningJournalEntryPosting : AuditableEntity, ITenantScopedEntity, ICompanyOperationalEntity
{
    public Guid CompanyId { get; private set; }

    /// <summary>1, 2, ... por empresa (única por empresa); el reemplazo de una versión es la siguiente.</summary>
    public int Version { get; private set; }

    /// <summary>Única versión vigente de la empresa; false = reemplazada (historial).</summary>
    public bool IsCurrent { get; private set; }
    public DateTime? SupersededAt { get; private set; }
    public DateOnly EntryDate { get; private set; }

    /// <summary>Σ Debe (= Σ Haber) del asiento, con el redondeo monetario vigente.</summary>
    public decimal TotalAmount { get; private set; }
    public int LineCount { get; private set; }
    public OpeningBalancePostingStatus Status { get; private set; }
    public Guid? JournalEntryId { get; private set; }
    public DateTime? PostedAt { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public int Attempts { get; private set; }

    private OpeningJournalEntryPosting() { }

    /// <summary>
    /// Nueva versión vigente con Id (= <c>SourceEventId</c>) nuevo. <paramref name="version"/> la
    /// asigna el llamador (siguiente a la última de la empresa); la unicidad de versión y de vigente
    /// la garantizan los índices.
    /// </summary>
    public static OpeningJournalEntryPosting CreatePending(
        Guid tenantId,
        Guid companyId,
        int version,
        DateOnly entryDate,
        decimal totalAmount,
        int lineCount,
        Guid createdBy
    )
    {
        if (companyId == Guid.Empty)
            throw new ArgumentException("La empresa es obligatoria.", nameof(companyId));
        if (version < 1)
            throw new ArgumentOutOfRangeException(nameof(version), "La versión empieza en 1.");

        var posting = new OpeningJournalEntryPosting
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            Version = version,
            IsCurrent = true,
            Status = OpeningBalancePostingStatus.Pending,
        };
        posting.SetAttemptData(entryDate, totalAmount, lineCount);
        posting.SetCreated(createdBy);
        return posting;
    }

    /// <summary>
    /// Prepara un nuevo intento de un ASI aún no publicado con la fecha y las líneas vigentes (la
    /// fecha de apertura y el contenido pueden haber cambiado tras un fallo). Nunca sobre uno publicado.
    /// </summary>
    public void PrepareAttempt(DateOnly entryDate, decimal totalAmount, int lineCount, Guid updatedBy)
    {
        EnsureCurrent();
        if (Status == OpeningBalancePostingStatus.Posted)
            throw new DomainRuleViolationException(
                "El asiento de apertura ya está publicado; su corrección requiere el flujo de reverso."
            );
        SetAttemptData(entryDate, totalAmount, lineCount);
        SetUpdated(updatedBy);
    }

    /// <summary>Registra el asiento publicado. Idempotente con el mismo asiento; nunca lo sustituye.</summary>
    public void MarkPosted(Guid journalEntryId, Guid updatedBy)
    {
        EnsureCurrent();
        if (journalEntryId == Guid.Empty)
            throw new ArgumentException("El asiento es obligatorio.", nameof(journalEntryId));
        if (Status == OpeningBalancePostingStatus.Posted)
        {
            if (JournalEntryId == journalEntryId)
                return;
            throw new DomainRuleViolationException(
                "El asiento de apertura ya está publicado con otro asiento."
            );
        }

        Status = OpeningBalancePostingStatus.Posted;
        JournalEntryId = journalEntryId;
        PostedAt = DateTime.UtcNow;
        ErrorCode = null;
        ErrorMessage = null;
        Attempts++;
        SetUpdated(updatedBy);
    }

    /// <summary>Registra un intento fallido (reintentable). Un ASI publicado no puede fallar.</summary>
    public void MarkFailed(string? errorCode, string? errorMessage, Guid updatedBy)
    {
        EnsureCurrent();
        if (Status == OpeningBalancePostingStatus.Posted)
            throw new DomainRuleViolationException(
                "El asiento de apertura ya está publicado; su corrección requiere el flujo de reverso."
            );

        Status = OpeningBalancePostingStatus.Failed;
        ErrorCode = Truncate(errorCode, OpeningBalancePosting.ErrorCodeMaxLength);
        ErrorMessage = Truncate(errorMessage, OpeningBalancePosting.ErrorMessageMaxLength);
        Attempts++;
        SetUpdated(updatedBy);
    }

    /// <summary>
    /// Deja esta versión publicada como historial (no vigente) para permitir su reemplazo por una
    /// versión nueva. Solo cambia el estado del registro: NO reversa el asiento; el flujo de
    /// corrección de IL-8B debe reversarlo por completo antes, en la misma transacción.
    /// </summary>
    public void MarkSuperseded(Guid updatedBy)
    {
        EnsureCurrent();
        if (Status != OpeningBalancePostingStatus.Posted)
            throw new DomainRuleViolationException(
                "Solo un asiento de apertura publicado se reemplaza; una versión no publicada se reintenta."
            );
        IsCurrent = false;
        SupersededAt = DateTime.UtcNow;
        SetUpdated(updatedBy);
    }

    private void EnsureCurrent()
    {
        if (!IsCurrent)
            throw new DomainRuleViolationException(
                "Esta versión del asiento de apertura fue reemplazada y es solo historial."
            );
    }

    private void SetAttemptData(DateOnly entryDate, decimal totalAmount, int lineCount)
    {
        var rounded = OpeningBalancePosting.RoundAmount(totalAmount);
        if (rounded <= 0m)
            throw new DomainRuleViolationException("El total del asiento de apertura debe ser mayor a cero.");
        if (lineCount < 2)
            throw new DomainRuleViolationException("El asiento de apertura necesita al menos dos líneas.");
        EntryDate = entryDate;
        TotalAmount = rounded;
        LineCount = lineCount;
    }

    private static string? Truncate(string? value, int max) =>
        value is { Length: > 0 } && value.Length > max ? value[..max] : value;
}
