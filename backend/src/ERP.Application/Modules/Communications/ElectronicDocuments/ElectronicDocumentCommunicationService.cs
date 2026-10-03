using ERP.Application.Common;
using ERP.Application.Modules.Communications.DTOs;
using ERP.Application.Modules.Communications.Services;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Communications.Enums;
using ERP.Domain.Modules.Communications.ValueObjects;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.Communications.ElectronicDocuments;

/// <summary>Quién pidió la comunicación (solo para observabilidad).</summary>
public enum ElectronicDocumentCommunicationTrigger
{
    AuthorizedEvent,
    Reconciliation,
}

/// <summary>
/// ZH-EDOC-COMMUNICATIONS-01 — camino ÚNICO de "comprobante autorizado → comunicación", compartido por
/// el handler del evento y la reconciliación (mismo resultado, misma identidad):
/// <list type="number">
/// <item>Solo comprobantes en estado Authorized.</item>
/// <item>Contributor por (SourceModule, DocumentType); sin contributor → Skipped SOURCE_NOT_SUPPORTED.</item>
/// <item>Preferencia <c>electronic_documents.email_on_authorization</c> de la empresa DEL DOCUMENTO.</item>
/// <item>El contributor aporta destinatario y variables; Communications agrega adjuntos POR REFERENCIA
/// (XML autorizado y RIDE, resueltos por sus módulos dueños al enviar) y encola.</item>
/// </list>
/// Nada de esto renderiza RIDE, lee archivos ni habla con SMTP: dentro de la transacción fiscal solo se
/// inserta (idempotente) una fila de outbox.
/// </summary>
public interface IElectronicDocumentCommunicationService
{
    Task<ElectronicDocumentCommunicationResult> RequestAsync(
        ElectronicDocument document,
        ElectronicDocumentCommunicationTrigger trigger,
        CancellationToken ct = default
    );
}

public sealed partial class ElectronicDocumentCommunicationService : IElectronicDocumentCommunicationService
{
    private const string DefaultIssuerName = "Empresa emisora";

    private readonly IElectronicDocumentCommunicationContributorResolver _contributors;
    private readonly IOperationalPreferencesResolver _preferences;
    private readonly ICompanyRepository _companies;
    private readonly ICommunicationQueue _queue;
    private readonly ILogger<ElectronicDocumentCommunicationService> _logger;

    public ElectronicDocumentCommunicationService(
        IElectronicDocumentCommunicationContributorResolver contributors,
        IOperationalPreferencesResolver preferences,
        ICompanyRepository companies,
        ICommunicationQueue queue,
        ILogger<ElectronicDocumentCommunicationService> logger
    )
    {
        _contributors = contributors;
        _preferences = preferences;
        _companies = companies;
        _queue = queue;
        _logger = logger;
    }

    public async Task<ElectronicDocumentCommunicationResult> RequestAsync(
        ElectronicDocument document,
        ElectronicDocumentCommunicationTrigger trigger,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(document);
        LogRequested(document.Id, document.DocumentType, document.SourceModule, trigger);

        if (document.CurrentState != ElectronicDocumentState.Authorized || document.AuthorizationNumber is null)
            return Skip(document, trigger, ApiResponseCodes.Communications.SourceNotEligible);

        var contributor = _contributors.Resolve(document.SourceModule, document.DocumentType);
        if (contributor is null)
            return Skip(document, trigger, ApiResponseCodes.Communications.SourceNotSupported);

        // Preferencia de la empresa DUEÑA del documento (tenant/empresa explícitos, no el contexto
        // ambiente: esto corre desde un evento de dominio o un job).
        var preferences = await _preferences.ResolveAsync(document.TenantId, document.CompanyId, ct);
        if (!preferences.ElectronicDocuments.EmailOnAuthorization)
            return Skip(document, trigger, "EMAIL_ON_AUTHORIZATION_DISABLED");

        var contributed = await contributor.ContributeAsync(
            new ElectronicDocumentCommunicationContext(document, await ResolveIssuerNameAsync(document, ct)),
            ct
        );
        if (!contributed.IsSuccess)
            return Skip(document, trigger, contributed.Code ?? ApiResponseCodes.Communications.SourceNotFound);

        var contribution = contributed.Value!;
        // La ruta declarada es la que usa la reconciliación para correlacionar con la outbox: el aporte
        // debe coincidir exactamente (propósito y SourceType), o un faltante nunca dejaría de serlo.
        var target = contributor.Targets[document.DocumentType];
        if (!string.Equals(contribution.Purpose, target.Purpose, StringComparison.Ordinal)
            || !string.Equals(contribution.Source.Type, target.SourceType, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"El contributor {contributor.SourceModule} devolvió un propósito u origen distinto al de su ruta {document.DocumentType}."
            );

        // Ruta oficial: el origen de la comunicación es el mismo par (módulo, id) que el comprobante. Así
        // el RIDE se resuelve con ese par y la reconciliación puede correlacionar sin otra tabla.
        if (!string.Equals(contribution.Source.Module, document.SourceModule, StringComparison.Ordinal)
            || contribution.Source.Id != document.SourceEntityId)
            throw new InvalidOperationException(
                $"El origen aportado por {contributor.SourceModule} no corresponde al documento de origen del comprobante."
            );

        var queued = await _queue.EnqueueAsync(
            new CommunicationRequest(
                Scope: CommunicationScope.Company(document.TenantId, document.CompanyId, contribution.BranchId),
                Purpose: contribution.Purpose,
                Source: contribution.Source,
                RecipientRole: contribution.RecipientRole,
                RecipientName: contribution.RecipientName,
                RecipientEmail: contribution.RecipientEmail,
                Template: contribution.TemplateModel,
                Attachments: Attachments(document, contribution.DocumentNumber),
                Priority: CommunicationPriority.Normal,
                ScheduledAtUtc: DateTime.UtcNow
            ),
            ct
        );

        var outcome = queued.WasAlreadyQueued
            ? ElectronicDocumentCommunicationOutcome.AlreadyQueued
            : ElectronicDocumentCommunicationOutcome.Queued;
        LogQueued(document.Id, document.DocumentType, contribution.Purpose, queued.Id, outcome, queued.FailureCode, trigger);
        return new ElectronicDocumentCommunicationResult(outcome, queued.Id, queued.FailureCode);
    }

    /// <summary>
    /// Adjuntos por referencia al comprobante (<c>ElectronicDocument.Id</c>): XML autorizado (si ya está
    /// almacenado) y RIDE. Ningún byte se copia ni se genera aquí.
    /// </summary>
    private static List<QueueCommunicationAttachmentDto> Attachments(ElectronicDocument document, string documentNumber)
    {
        var token = SafeFileToken(documentNumber);
        var attachments = new List<QueueCommunicationAttachmentDto>(2);
        if (!string.IsNullOrWhiteSpace(document.AuthorizedXmlPath))
            attachments.Add(
                new QueueCommunicationAttachmentDto(
                    CommunicationAttachmentType.AuthorizedXml,
                    $"{token}-autorizado.xml",
                    "application/xml",
                    ReferenceId: document.Id
                )
            );
        attachments.Add(
            new QueueCommunicationAttachmentDto(
                CommunicationAttachmentType.RidePdf,
                $"{token}-RIDE.pdf",
                "application/pdf",
                ReferenceId: document.Id
            )
        );
        return attachments;
    }

    private async Task<string> ResolveIssuerNameAsync(ElectronicDocument document, CancellationToken ct)
    {
        var company = await _companies.GetByIdAsync(document.CompanyId, ct);
        if (company is null || company.TenantId != document.TenantId)
            return DefaultIssuerName;
        return new[] { company.TradeName, company.LegalName }
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? DefaultIssuerName;
    }

    private ElectronicDocumentCommunicationResult Skip(
        ElectronicDocument document,
        ElectronicDocumentCommunicationTrigger trigger,
        string reason
    )
    {
        LogSkipped(document.Id, document.DocumentType, document.SourceModule, reason, trigger);
        return new ElectronicDocumentCommunicationResult(ElectronicDocumentCommunicationOutcome.Skipped, FailureCode: reason);
    }

    private static string SafeFileToken(string value) =>
        string.Join("-", value.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));

    // Sin PII: ids, tipos y códigos; nunca correo, nombre del destinatario ni contenido.
    [LoggerMessage(EventId = 4230, EventName = "ElectronicDocumentCommunicationRequested", Level = LogLevel.Debug,
        Message = "Communications: requested for ElectronicDocument {ElectronicDocumentId} type={DocumentType} module={SourceModule} trigger={Trigger}")]
    private partial void LogRequested(Guid electronicDocumentId, ElectronicDocumentType documentType, string sourceModule, ElectronicDocumentCommunicationTrigger trigger);

    [LoggerMessage(EventId = 4231, EventName = "ElectronicDocumentCommunicationSkipped", Level = LogLevel.Information,
        Message = "Communications: skipped for ElectronicDocument {ElectronicDocumentId} type={DocumentType} module={SourceModule} reason={Reason} trigger={Trigger}")]
    private partial void LogSkipped(Guid electronicDocumentId, ElectronicDocumentType documentType, string sourceModule, string reason, ElectronicDocumentCommunicationTrigger trigger);

    [LoggerMessage(EventId = 4232, EventName = "ElectronicDocumentCommunicationQueued", Level = LogLevel.Information,
        Message = "Communications: ElectronicDocument {ElectronicDocumentId} type={DocumentType} purpose={Purpose} -> {CommunicationId} outcome={Outcome} failure={FailureCode} trigger={Trigger}")]
    private partial void LogQueued(Guid electronicDocumentId, ElectronicDocumentType documentType, string purpose, Guid communicationId, ElectronicDocumentCommunicationOutcome outcome, string? failureCode, ElectronicDocumentCommunicationTrigger trigger);
}
