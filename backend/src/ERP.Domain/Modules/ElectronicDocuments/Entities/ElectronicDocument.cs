using ERP.Domain.Common;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Events;
using ERP.Domain.Modules.ElectronicDocuments.ValueObjects;

namespace ERP.Domain.Modules.ElectronicDocuments.Entities;

/// <summary>
/// Administra el ciclo de vida electrónico (estado, clave de acceso, autorización, archivos XML,
/// reintentos, trazabilidad) de un comprobante SRI, sin conocer ni almacenar información comercial
/// del documento de origen (cliente, ítems, impuestos, totales, forma de pago, etc.).
///
/// La referencia al documento de origen es deliberadamente débil (<see cref="SourceModule"/> +
/// <see cref="SourceEntityId"/>, sin FK física) para que este módulo permanezca transversal y
/// desacoplado de Sales, Purchases, Accounting e Inventory — ninguno de esos módulos es referenciado
/// desde aquí, ni por proyecto ni por namespace.
///
/// Se persiste en <see cref="ElectronicDocumentState.Draft"/> ANTES de correr el pipeline de
/// generación (ver <c>ElectronicDocumentIssuer</c>) — a propósito, para que ninguna factura de
/// punto de emisión electrónico quede invisible en el Monitor si algo falla. Si alguna etapa
/// previa a la firma (proveedor, XML, XSD, firma, almacenamiento) falla, <see cref="MarkFailed"/>
/// transiciona la misma fila a <see cref="ElectronicDocumentState.Failed"/> con el motivo real —
/// nunca se descarta el registro ni se pierde el intento.
/// </summary>
public sealed class ElectronicDocument : AuditableEntity, ITenantScopedEntity, ICompanyOperationalEntity
{
    public const int SourceModuleMaxLen = 100;
    public const int PathMaxLen = 500;
    public const int VersionMaxLen = 20;

    public Guid CompanyId { get; private set; }
    public ElectronicDocumentType DocumentType { get; private set; }

    /// <summary>Nombre del módulo dueño del documento de origen (p.ej. "Sales"). No es un using ni una FK.</summary>
    public string SourceModule { get; private set; } = null!;
    public Guid SourceEntityId { get; private set; }

    public ElectronicDocumentState CurrentState { get; private set; }

    /// <summary>Ambiente SRI ("1"=Pruebas, "2"=Producción — Ficha Técnica SRI Tabla 4, mismos códigos que <c>SriSettings.Environment</c>) resuelto por el proveedor de datos — null si el pipeline falló antes de conocerlo.</summary>
    public string? Environment { get; private set; }

    public AccessKey? AccessKey { get; private set; }
    public AuthorizationNumber? AuthorizationNumber { get; private set; }
    public DateTime? AuthorizationDate { get; private set; }

    /// <summary>Versión del esquema "factura" del XML generado (p.ej. "1.1.0") — la declara el XmlBuilder.</summary>
    public string? XmlVersion { get; private set; }
    /// <summary>Versión del XSD contra el que se validó el XML — la declara el SchemaValidator.</summary>
    public string? SchemaVersion { get; private set; }

    /// <summary>Ruta opaca de <c>IFileStorage</c> al XML generado (sin firmar) — nunca una ruta manual.</summary>
    public string? XmlDraftPath { get; private set; }
    /// <summary>Ruta opaca de <c>IFileStorage</c> al XML firmado.</summary>
    public string? SignedXmlPath { get; private set; }
    /// <summary>Ruta opaca de <c>IFileStorage</c> al XML autorizado por el SRI — aún no se persiste en esta fase.</summary>
    public string? AuthorizedXmlPath { get; private set; }

    public int RetryCount { get; private set; }
    public DateTime? LastAttemptUtc { get; private set; }

    /// <summary>Estado del que provino un DeadLetter — permite a <see cref="Reactivate"/> restaurarlo. Null fuera de DeadLetter.</summary>
    public ElectronicDocumentState? PreDeadLetterState { get; private set; }

    /// <summary>Motivo real del fallo más reciente en <see cref="ElectronicDocumentState.Failed"/> — null fuera de ese estado.</summary>
    public string? LastError { get; private set; }

    private ElectronicDocument() { }

    /// <summary>
    /// Registra el documento en memoria en estado Draft. <paramref name="id"/> es opcional para
    /// permitir que el llamador (que ya generó y almacenó los XML usando ese mismo Id como parte
    /// de la ruta de <c>IFileStorage</c>) lo fije explícitamente — si se omite, se genera aquí.
    /// </summary>
    public static ElectronicDocument Create(
        Guid tenantId,
        Guid companyId,
        ElectronicDocumentType documentType,
        string sourceModule,
        Guid sourceEntityId,
        Guid createdBy,
        Guid? id = null)
    {
        if (string.IsNullOrWhiteSpace(sourceModule))
            throw new ArgumentException("El módulo de origen es obligatorio.", nameof(sourceModule));
        if (sourceModule.Length > SourceModuleMaxLen)
            throw new ArgumentException($"El módulo de origen no puede superar {SourceModuleMaxLen} caracteres.", nameof(sourceModule));
        if (sourceEntityId == Guid.Empty)
            throw new ArgumentException("El identificador del documento de origen es obligatorio.", nameof(sourceEntityId));

        var document = new ElectronicDocument
        {
            Id = id ?? Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            DocumentType = documentType,
            SourceModule = sourceModule.Trim(),
            SourceEntityId = sourceEntityId,
            CurrentState = ElectronicDocumentState.Draft,
            RetryCount = 0,
        };
        document.SetCreated(createdBy);
        document.RaiseDomainEvent(new ElectronicDocumentCreatedEvent(
            tenantId, document.Id, documentType, document.SourceModule, sourceEntityId));

        return document;
    }

    /// <summary>
    /// Registra el ambiente SRI resuelto por el proveedor de datos (ver <c>ElectronicDocumentIssuer.RunPipelineAsync</c>,
    /// justo después de que el proveedor tiene éxito). No es una transición de estado — es
    /// información capturada oportunistamente, tan pronto se conoce.
    /// </summary>
    public void SetEnvironment(string environment)
    {
        if (string.IsNullOrWhiteSpace(environment))
            throw new ArgumentException("El ambiente es obligatorio.", nameof(environment));
        Environment = environment.Trim();
    }

    /// <summary>Transición Draft/Failed→XmlGenerated: el XML ya fue construido, validado contra XSD y almacenado.</summary>
    public void MarkXmlGenerated(string xmlDraftPath, string xmlVersion, string schemaVersion, Guid updatedBy)
    {
        if (CurrentState is not (ElectronicDocumentState.Draft or ElectronicDocumentState.Failed))
            throw new InvalidOperationException($"Solo se puede marcar el XML como generado desde Draft o Failed (estado actual: {CurrentState}).");
        if (string.IsNullOrWhiteSpace(xmlDraftPath))
            throw new ArgumentException("La ruta del XML generado es obligatoria.", nameof(xmlDraftPath));
        if (xmlDraftPath.Length > PathMaxLen)
            throw new ArgumentException($"La ruta del XML generado no puede superar {PathMaxLen} caracteres.", nameof(xmlDraftPath));

        var fromState = CurrentState;
        XmlDraftPath = xmlDraftPath;
        XmlVersion = string.IsNullOrWhiteSpace(xmlVersion) ? null : xmlVersion.Trim();
        SchemaVersion = string.IsNullOrWhiteSpace(schemaVersion) ? null : schemaVersion.Trim();
        LastError = null;
        CurrentState = ElectronicDocumentState.XmlGenerated;
        LastAttemptUtc = DateTime.UtcNow;
        SetUpdated(updatedBy);

        RaiseDomainEvent(new ElectronicDocumentXmlGeneratedEvent(TenantId, Id, DocumentType, fromState, CurrentState));
    }

    /// <summary>
    /// Transición Draft/Failed→Failed: falló alguna etapa del pipeline previa a la firma
    /// (proveedor de datos, construcción de XML, validación XSD, firma, almacenamiento). El
    /// documento permanece visible y reintentable en el Monitor — nunca se pierde el registro.
    /// </summary>
    public void MarkFailed(string reason, Guid updatedBy)
    {
        if (CurrentState is not (ElectronicDocumentState.Draft or ElectronicDocumentState.Failed))
            throw new InvalidOperationException($"Solo se puede marcar como fallido desde Draft o Failed (estado actual: {CurrentState}).");
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("El motivo del fallo es obligatorio.", nameof(reason));

        var fromState = CurrentState;
        LastError = reason.Trim();
        CurrentState = ElectronicDocumentState.Failed;
        RetryCount++;
        LastAttemptUtc = DateTime.UtcNow;
        SetUpdated(updatedBy);

        RaiseDomainEvent(new ElectronicDocumentFailedEvent(TenantId, Id, DocumentType, fromState, CurrentState, LastError));
    }

    /// <summary>Transición XmlGenerated→Signed: el XML firmado ya fue almacenado.</summary>
    public void MarkSigned(string signedXmlPath, AccessKey accessKey, Guid updatedBy)
    {
        if (CurrentState != ElectronicDocumentState.XmlGenerated)
            throw new InvalidOperationException($"Solo se puede marcar como firmado desde XmlGenerated (estado actual: {CurrentState}).");
        if (string.IsNullOrWhiteSpace(signedXmlPath))
            throw new ArgumentException("La ruta del XML firmado es obligatoria.", nameof(signedXmlPath));
        if (signedXmlPath.Length > PathMaxLen)
            throw new ArgumentException($"La ruta del XML firmado no puede superar {PathMaxLen} caracteres.", nameof(signedXmlPath));

        var fromState = CurrentState;
        SignedXmlPath = signedXmlPath;
        AccessKey = accessKey ?? throw new ArgumentNullException(nameof(accessKey));
        CurrentState = ElectronicDocumentState.Signed;
        LastAttemptUtc = DateTime.UtcNow;
        SetUpdated(updatedBy);

        RaiseDomainEvent(new ElectronicDocumentSignedEvent(TenantId, Id, DocumentType, fromState, CurrentState));
    }

    /// <summary>Transición Signed→Sent: el XML firmado fue enviado al servicio de recepción del SRI.</summary>
    public void MarkSent(Guid updatedBy)
    {
        if (CurrentState != ElectronicDocumentState.Signed)
            throw new InvalidOperationException($"Solo se puede marcar como enviado desde Signed (estado actual: {CurrentState}).");

        var fromState = CurrentState;
        LastError = null;
        CurrentState = ElectronicDocumentState.Sent;
        LastAttemptUtc = DateTime.UtcNow;
        SetUpdated(updatedBy);

        RaiseDomainEvent(new ElectronicDocumentSentEvent(TenantId, Id, DocumentType, fromState, CurrentState));
    }

    /// <summary>Transición Sent→Received: el SRI acusó recibo del comprobante (respuesta RECIBIDA de recepción).</summary>
    public void MarkReceived(Guid updatedBy)
    {
        if (CurrentState != ElectronicDocumentState.Sent)
            throw new InvalidOperationException($"Solo se puede marcar como recibido desde Sent (estado actual: {CurrentState}).");

        var fromState = CurrentState;
        LastError = null;
        CurrentState = ElectronicDocumentState.Received;
        LastAttemptUtc = DateTime.UtcNow;
        SetUpdated(updatedBy);

        RaiseDomainEvent(new ElectronicDocumentReceivedEvent(TenantId, Id, DocumentType, fromState, CurrentState));
    }

    /// <summary>
    /// Transición Sent/Received→Authorized: el SRI autorizó el comprobante. Se acepta desde
    /// ambos estados porque la consulta de autorización (<c>SriSoapClient.CheckAuthorizationAsync</c>)
    /// puede resolver directamente sin haber pasado por un acuse de recibo explícito.
    /// </summary>
    public void MarkAuthorized(
        AuthorizationNumber authorizationNumber, DateTime authorizationDate, string? authorizedXmlPath, Guid updatedBy)
    {
        if (CurrentState is not (ElectronicDocumentState.Sent or ElectronicDocumentState.Received))
            throw new InvalidOperationException($"Solo se puede autorizar desde Sent o Received (estado actual: {CurrentState}).");
        if (authorizedXmlPath is not null && authorizedXmlPath.Length > PathMaxLen)
            throw new ArgumentException($"La ruta del XML autorizado no puede superar {PathMaxLen} caracteres.", nameof(authorizedXmlPath));

        var fromState = CurrentState;
        AuthorizationNumber = authorizationNumber ?? throw new ArgumentNullException(nameof(authorizationNumber));
        AuthorizationDate = authorizationDate;
        AuthorizedXmlPath = authorizedXmlPath;
        LastError = null;
        CurrentState = ElectronicDocumentState.Authorized;
        LastAttemptUtc = DateTime.UtcNow;
        SetUpdated(updatedBy);

        RaiseDomainEvent(new ElectronicDocumentAuthorizedEvent(TenantId, Id, DocumentType, fromState, CurrentState));
    }

    /// <summary>
    /// Transición Sent/Received→Rejected: el SRI rechazó el comprobante (sin reintento posible).
    /// <paramref name="sriMessages"/> es opcional — cuando el rechazo proviene de una respuesta
    /// SRI real (recepción o autorización), lleva los mensajes estructurados tal cual el SRI los
    /// envió, para que <c>ElectronicDocumentSriMessageAuditHandler</c> los persista individualmente.
    /// </summary>
    public void MarkRejected(string reason, Guid updatedBy, IReadOnlyList<SriMessage>? sriMessages = null)
    {
        if (CurrentState is not (ElectronicDocumentState.Sent or ElectronicDocumentState.Received))
            throw new InvalidOperationException($"Solo se puede rechazar desde Sent o Received (estado actual: {CurrentState}).");
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("El motivo de rechazo es obligatorio.", nameof(reason));

        var fromState = CurrentState;
        LastError = reason.Trim();
        CurrentState = ElectronicDocumentState.Rejected;
        LastAttemptUtc = DateTime.UtcNow;
        SetUpdated(updatedBy);

        RaiseDomainEvent(new ElectronicDocumentRejectedEvent(TenantId, Id, DocumentType, fromState, CurrentState, LastError, sriMessages));
    }

    /// <summary>
    /// Transición Failed/Signed/Sent/Received→DeadLetter: se agotaron los reintentos automáticos
    /// (del pipeline previo a la firma, o de envío/consulta al SRI — política de reintento en
    /// Application) sin resolver el documento. Guarda el estado de origen en
    /// <see cref="PreDeadLetterState"/> para que <see cref="Reactivate"/> pueda restaurarlo.
    /// </summary>
    public void MarkDeadLetter(string reason, Guid updatedBy)
    {
        if (CurrentState is not (ElectronicDocumentState.Failed or ElectronicDocumentState.Signed
            or ElectronicDocumentState.Sent or ElectronicDocumentState.Received))
            throw new InvalidOperationException($"Solo se puede marcar como DeadLetter desde Failed, Signed, Sent o Received (estado actual: {CurrentState}).");
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("El motivo del DeadLetter es obligatorio.", nameof(reason));

        var fromState = CurrentState;
        PreDeadLetterState = fromState;
        LastError = reason.Trim();
        CurrentState = ElectronicDocumentState.DeadLetter;
        LastAttemptUtc = DateTime.UtcNow;
        SetUpdated(updatedBy);

        RaiseDomainEvent(new ElectronicDocumentDeadLetterEvent(TenantId, Id, DocumentType, fromState, CurrentState, LastError));
    }

    /// <summary>
    /// Registra un intento de reintento (envío o consulta de autorización) que todavía no
    /// resolvió el documento — no cambia de estado, solo incrementa <see cref="RetryCount"/> y
    /// <see cref="LastAttemptUtc"/>. Se llama antes de la llamada de red real (mismo patrón de
    /// checkpoint del resto del agregado) para no perder el conteo si el proceso falla a mitad
    /// del intento.
    /// </summary>
    public void MarkRetryAttempted(Guid updatedBy)
    {
        if (CurrentState is not (ElectronicDocumentState.Signed or ElectronicDocumentState.Received))
            throw new InvalidOperationException($"Solo se puede reintentar desde Signed o Received (estado actual: {CurrentState}).");

        RetryCount++;
        LastAttemptUtc = DateTime.UtcNow;
        SetUpdated(updatedBy);

        RaiseDomainEvent(new ElectronicDocumentRetryAttemptedEvent(TenantId, Id, DocumentType, CurrentState, CurrentState, RetryCount));
    }

    /// <summary>
    /// Transición DeadLetter→(Signed|Received): reactivación manual previa a un reintento forzado.
    /// Restaura el estado que tenía el documento antes de agotar los reintentos automáticos.
    /// </summary>
    public void Reactivate(Guid updatedBy)
    {
        if (CurrentState != ElectronicDocumentState.DeadLetter)
            throw new InvalidOperationException($"Solo se puede reactivar un documento en DeadLetter (estado actual: {CurrentState}).");
        if (PreDeadLetterState is null)
            throw new InvalidOperationException("El documento no tiene un estado previo a DeadLetter registrado.");

        var fromState = CurrentState;
        CurrentState = PreDeadLetterState.Value;
        PreDeadLetterState = null;
        SetUpdated(updatedBy);

        RaiseDomainEvent(new ElectronicDocumentReactivatedEvent(TenantId, Id, DocumentType, fromState, CurrentState));
    }

    /// <summary>
    /// Transición Authorized→Cancelled: el comprobante ya autorizado por el SRI fue anulado.
    /// Solo válida desde Authorized — anular un documento que nunca llegó a autorizarse no es
    /// un concepto SRI real (un borrador o un envío fallido simplemente no se reintenta).
    /// </summary>
    public void MarkCancelled(string reason, Guid updatedBy)
    {
        if (CurrentState != ElectronicDocumentState.Authorized)
            throw new InvalidOperationException($"Solo se puede anular un documento Authorized (estado actual: {CurrentState}).");
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("El motivo de anulación es obligatorio.", nameof(reason));

        var fromState = CurrentState;
        CurrentState = ElectronicDocumentState.Cancelled;
        LastAttemptUtc = DateTime.UtcNow;
        SetUpdated(updatedBy);

        RaiseDomainEvent(new ElectronicDocumentCancelledEvent(TenantId, Id, DocumentType, fromState, CurrentState, reason.Trim()));
    }
}
