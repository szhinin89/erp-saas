using ERP.Domain.Common;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.InitialLoad.Constants;
using ERP.Domain.Modules.InitialLoad.Enums;

namespace ERP.Domain.Modules.InitialLoad.Entities;

/// <summary>
/// IL-7A — estado contable de la apertura de UN <see cref="ImportBatch"/> de saldos (inventario,
/// CxC o CxP): uno por lote (índice único por empresa + lote), con el monto ya resuelto desde los
/// datos confirmados del dominio (nunca staging/Excel) y la fecha <c>Company.OpeningBalanceDate</c>.
/// <see cref="OpeningBalancePostingStatus.Pending"/> → <see cref="OpeningBalancePostingStatus.Posted"/>
/// (terminal) o <see cref="OpeningBalancePostingStatus.Failed"/> (reintentable). Nunca revierte la
/// carga operativa: es un registro aparte del lote.
/// </summary>
public sealed class OpeningBalancePosting : AuditableEntity, ITenantScopedEntity, ICompanyOperationalEntity
{
    public const int FactTypeMaxLength = 60;
    public const int ErrorCodeMaxLength = 100;
    public const int ErrorMessageMaxLength = 2000;

    public Guid CompanyId { get; private set; }
    public Guid ImportBatchId { get; private set; }
    public ImportType ImportType { get; private set; }
    public string FactType { get; private set; } = null!;
    public DateOnly EntryDate { get; private set; }
    public decimal Amount { get; private set; }
    public OpeningBalancePostingStatus Status { get; private set; }
    public Guid? JournalEntryId { get; private set; }
    public DateTime? PostedAt { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public int Attempts { get; private set; }

    private OpeningBalancePosting() { }

    /// <summary>
    /// Redondeo monetario del monto de apertura: mismo estándar que el resto de montos contables
    /// (numeric(18,2) de <c>journal_entry_lines</c>, <see cref="FiscalPrecision.TaxAmount"/>,
    /// <see cref="MidpointRounding.AwayFromZero"/>). El costo de inventario llega con más decimales.
    /// </summary>
    public static decimal RoundAmount(decimal amount) =>
        Math.Round(amount, FiscalPrecision.TaxAmount, MidpointRounding.AwayFromZero);

    public static OpeningBalancePosting CreatePending(
        Guid tenantId,
        Guid companyId,
        Guid importBatchId,
        ImportType importType,
        DateOnly entryDate,
        decimal amount,
        Guid createdBy
    )
    {
        if (companyId == Guid.Empty)
            throw new ArgumentException("La empresa es obligatoria.", nameof(companyId));
        if (importBatchId == Guid.Empty)
            throw new ArgumentException("El lote de importación es obligatorio.", nameof(importBatchId));
        var factType = OpeningBalancePostingFacts.ForImportType(importType)
            ?? throw new DomainRuleViolationException(
                "Este tipo de carga inicial no genera asiento de apertura."
            );
        var rounded = RoundAmount(amount);
        if (rounded <= 0m)
            throw new DomainRuleViolationException(
                "El monto de apertura del lote debe ser mayor a cero."
            );

        var posting = new OpeningBalancePosting
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            ImportBatchId = importBatchId,
            ImportType = importType,
            FactType = factType,
            EntryDate = entryDate,
            Amount = rounded,
            Status = OpeningBalancePostingStatus.Pending,
        };
        posting.SetCreated(createdBy);
        return posting;
    }

    /// <summary>Registra el asiento publicado. Idempotente con el mismo asiento; nunca lo sustituye.</summary>
    public void MarkPosted(Guid journalEntryId, Guid updatedBy)
    {
        if (journalEntryId == Guid.Empty)
            throw new ArgumentException("El asiento es obligatorio.", nameof(journalEntryId));
        if (Status == OpeningBalancePostingStatus.Posted)
        {
            if (JournalEntryId == journalEntryId)
                return;
            throw new DomainRuleViolationException(
                "La apertura del lote ya está contabilizada con otro asiento."
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

    /// <summary>Registra un intento fallido (reintentable). Una apertura ya contabilizada no puede fallar.</summary>
    public void MarkFailed(string? errorCode, string? errorMessage, Guid updatedBy)
    {
        if (Status == OpeningBalancePostingStatus.Posted)
            throw new DomainRuleViolationException(
                "La apertura del lote ya está contabilizada; corríjala con el reverso del asiento."
            );

        Status = OpeningBalancePostingStatus.Failed;
        ErrorCode = Truncate(errorCode, ErrorCodeMaxLength);
        ErrorMessage = Truncate(errorMessage, ErrorMessageMaxLength);
        Attempts++;
        SetUpdated(updatedBy);
    }

    private static string? Truncate(string? value, int max) =>
        value is { Length: > 0 } && value.Length > max ? value[..max] : value;
}
