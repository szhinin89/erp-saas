using ERP.Domain.Common;
using ERP.Domain.Modules.Ride.Enums;
using ERP.Domain.Modules.Ride.Events;
using ERP.Domain.Modules.Ride.ValueObjects;

namespace ERP.Domain.Modules.Ride.Entities;

/// <summary>
/// Metadatos de una huella (fingerprint) de RIDE generada para un <c>ElectronicDocument</c>.
/// Aggregate Root de Ride. Nunca almacena el PDF en sí (ADR-025 §15, vive en <c>IFileStorage</c>)
/// y nunca conoce <c>ElectronicDocuments</c> ni <c>Sales</c> — <see cref="ElectronicDocumentId"/>
/// es una referencia débil (Guid, sin FK física), mismo criterio que <c>ElectronicDocument.SourceEntityId</c>.
///
/// La huella (<see cref="SourceXmlHash"/> + <see cref="TemplateVersion"/> + <see cref="BrandingVersion"/>
/// + <see cref="RendererVersion"/> + <see cref="RideSpecificationVersion"/>) identifica de forma única
/// un resultado de generación (ADR-025 §14) — <see cref="RidePdfState.Generated"/> es terminal para una huella dada:
/// una vez generada, la única forma de "regenerar" es <see cref="MarkRegenerated"/>, nunca
/// <see cref="MarkGenerated"/> de nuevo, protegiendo un registro exitoso de ser degradado por un
/// reintento fallido no solicitado.
/// </summary>
public sealed class RidePdfDocument : AuditableEntity, ITenantScopedEntity, ICompanyOperationalEntity
{
    public const int PathMaxLen = 500;
    public const int VersionMaxLen = 50;
    public const int TemplateIdMaxLen = 100;
    public const int ReasonMaxLen = 1000;

    public Guid CompanyId { get; private set; }
    public Guid ElectronicDocumentId { get; private set; }
    public RideDocumentType DocumentType { get; private set; }

    public RideContentHash SourceXmlHash { get; private set; } = null!;
    public string TemplateId { get; private set; } = null!;
    public string TemplateVersion { get; private set; } = null!;
    public string BrandingVersion { get; private set; } = null!;
    public string RendererVersion { get; private set; } = null!;
    public string RideSpecificationVersion { get; private set; } = null!;

    public RidePdfState State { get; private set; }
    public string? StoragePath { get; private set; }
    public string? LastError { get; private set; }
    public DateTime? GeneratedAtUtc { get; private set; }
    public int RetryCount { get; private set; }

    private RidePdfDocument() { }

    public static RidePdfDocument Create(
        Guid tenantId,
        Guid companyId,
        Guid electronicDocumentId,
        RideDocumentType documentType,
        RideContentHash sourceXmlHash,
        string templateId,
        string templateVersion,
        string brandingVersion,
        string rendererVersion,
        string rideSpecificationVersion,
        Guid createdBy,
        Guid? id = null)
    {
        if (electronicDocumentId == Guid.Empty)
            throw new ArgumentException("El documento electrónico de origen es obligatorio.", nameof(electronicDocumentId));
        ArgumentNullException.ThrowIfNull(sourceXmlHash);
        ValidateVersion(templateId, TemplateIdMaxLen, nameof(templateId), "El identificador de plantilla");
        ValidateVersion(templateVersion, VersionMaxLen, nameof(templateVersion), "La versión de plantilla");
        ValidateVersion(brandingVersion, VersionMaxLen, nameof(brandingVersion), "La versión de branding");
        ValidateVersion(rendererVersion, VersionMaxLen, nameof(rendererVersion), "La versión del renderer");
        ValidateVersion(rideSpecificationVersion, VersionMaxLen, nameof(rideSpecificationVersion), "La versión de especificación de Ride");

        return new RidePdfDocument
        {
            Id = id ?? Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            ElectronicDocumentId = electronicDocumentId,
            DocumentType = documentType,
            SourceXmlHash = sourceXmlHash,
            TemplateId = templateId.Trim(),
            TemplateVersion = templateVersion.Trim(),
            BrandingVersion = brandingVersion.Trim(),
            RendererVersion = rendererVersion.Trim(),
            RideSpecificationVersion = rideSpecificationVersion.Trim(),
            State = RidePdfState.Pending,
        }.WithCreated(createdBy);
    }

    private RidePdfDocument WithCreated(Guid createdBy)
    {
        SetCreated(createdBy);
        return this;
    }

    /// <summary>Transición Pending/Failed/PendingSource→Generated: primer éxito para esta huella.</summary>
    public void MarkGenerated(string storagePath, DateTime generatedAtUtc, Guid updatedBy)
    {
        if (State is not (RidePdfState.Pending or RidePdfState.Failed or RidePdfState.PendingSource))
            throw new InvalidOperationException(
                $"Solo se puede marcar como generado desde Pending, Failed o PendingSource (estado actual: {State}). " +
                "Una huella ya Generated se actualiza con MarkRegenerated.");
        ValidateStoragePath(storagePath);

        StoragePath = storagePath.Trim();
        GeneratedAtUtc = generatedAtUtc;
        LastError = null;
        State = RidePdfState.Generated;
        SetUpdated(updatedBy);

        RaiseDomainEvent(new RidePdfGeneratedEvent(TenantId, Id, ElectronicDocumentId, DocumentType, StoragePath));
    }

    /// <summary>Transición Generated→Generated: la huella ya existía y se regeneró explícitamente (RegenerateRideCommand).</summary>
    public void MarkRegenerated(string storagePath, DateTime generatedAtUtc, Guid updatedBy)
    {
        if (State != RidePdfState.Generated)
            throw new InvalidOperationException(
                $"Solo se puede regenerar una huella que ya esté Generated (estado actual: {State}). " +
                "Un primer éxito se registra con MarkGenerated.");
        ValidateStoragePath(storagePath);

        StoragePath = storagePath.Trim();
        GeneratedAtUtc = generatedAtUtc;
        LastError = null;
        SetUpdated(updatedBy);

        RaiseDomainEvent(new RidePdfRegeneratedEvent(TenantId, Id, ElectronicDocumentId, DocumentType, StoragePath));
    }

    /// <summary>Transición Pending/Failed/PendingSource→Failed: el intento de generación falló.</summary>
    public void MarkFailed(string reason, Guid updatedBy)
    {
        if (State is not (RidePdfState.Pending or RidePdfState.Failed or RidePdfState.PendingSource))
            throw new InvalidOperationException(
                $"Solo se puede marcar como fallido desde Pending, Failed o PendingSource (estado actual: {State}). " +
                "Una huella Generated nunca se degrada a Failed.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("El motivo del fallo es obligatorio.", nameof(reason));
        if (reason.Length > ReasonMaxLen)
            throw new ArgumentException($"El motivo del fallo no puede superar {ReasonMaxLen} caracteres.", nameof(reason));

        LastError = reason.Trim();
        State = RidePdfState.Failed;
        RetryCount++;
        SetUpdated(updatedBy);

        RaiseDomainEvent(new RidePdfGenerationFailedEvent(TenantId, Id, ElectronicDocumentId, DocumentType, LastError));
    }

    /// <summary>
    /// Transición Pending/Failed/PendingSource→PendingSource: el XML autorizado todavía no está
    /// disponible en ElectronicDocuments (H1, ADR-025) — reintentable, no es un error. Sin evento
    /// de dominio propio (no está en la lista congelada de eventos de ADR-025 §6).
    /// </summary>
    public void MarkPendingSource(Guid updatedBy)
    {
        if (State is not (RidePdfState.Pending or RidePdfState.Failed or RidePdfState.PendingSource))
            throw new InvalidOperationException(
                $"Solo se puede marcar como pendiente de fuente desde Pending, Failed o PendingSource (estado actual: {State}). " +
                "Una huella Generated nunca se degrada a PendingSource.");

        LastError = null;
        State = RidePdfState.PendingSource;
        SetUpdated(updatedBy);
    }

    private static void ValidateVersion(string value, int maxLen, string paramName, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{label} es obligatoria.", paramName);
        if (value.Length > maxLen)
            throw new ArgumentException($"{label} no puede superar {maxLen} caracteres.", paramName);
    }

    private static void ValidateStoragePath(string storagePath)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
            throw new ArgumentException("La ruta de almacenamiento del PDF es obligatoria.", nameof(storagePath));
        if (storagePath.Length > PathMaxLen)
            throw new ArgumentException($"La ruta de almacenamiento no puede superar {PathMaxLen} caracteres.", nameof(storagePath));
    }
}
