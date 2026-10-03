using ERP.Application.Common;
using ERP.Application.Modules.Communications.DTOs;
using ERP.Application.Modules.Communications.Templates;
using ERP.Domain.Modules.Communications.Constants;
using ERP.Domain.Modules.Communications.Entities;
using ERP.Domain.Modules.Communications.Enums;
using ERP.Domain.Modules.Communications.Interfaces;
using ERP.Domain.Modules.Communications.ValueObjects;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ERP.Application.Modules.Communications.Services;

/// <summary>
/// ZH-COMMUNICATIONS-CONTRACT-01 — encola con alcance EXPLÍCITO e identidad central:
/// <list type="bullet">
/// <item>El alcance sale de la request; el contexto ambiente solo se usa como guarda: una request
/// Company para otra empresa que la del contexto autenticado se rechaza (el caller no es autoridad
/// del scope).</item>
/// <item><see cref="CommunicationIdentity"/> construye la IdempotencyKey (sin el template: cambiar un
/// template nunca produce otra comunicación lógica).</item>
/// <item><c>MaxRetries</c> se copia al encolar desde el perfil del mismo alcance (no se reinterpreta luego).</item>
/// <item>PostgreSQL es la autoridad de idempotencia (<see cref="ICommunicationOutboxRepository.EnqueueAsync"/>).</item>
/// </list>
/// ZH-COMMUNICATIONS-TEMPLATES-01 — el contenido se resuelve (<see cref="ICommunicationTemplateResolver"/>)
/// y renderiza (<see cref="CommunicationTemplateRenderer"/>) AL ENCOLAR: la fila guarda asunto/cuerpo
/// renderizados y la TemplateKey/versión/fuente; el envío nunca re-renderiza. Un fallo de template NO
/// lanza ni se pierde: se persiste la comunicación como <c>Failed</c> (categoría Configuration/Permanent,
/// sin contenido, con las variables si el propósito no es sensible) y se devuelve su código en
/// <see cref="QueuedCommunicationDto.TemplateFailureCode"/>. Nunca revierte el hecho de negocio.
/// </summary>
public sealed partial class CommunicationQueue : ICommunicationQueue
{
    private readonly ICommunicationOutboxRepository _outbox;
    private readonly ICurrentCompany _currentCompany;
    private readonly ICurrentUser _currentUser;
    private readonly ICommunicationSettingsResolver _settings;
    private readonly ICommunicationTemplateResolver _templates;
    private readonly ILogger<CommunicationQueue> _logger;

    public CommunicationQueue(
        ICommunicationOutboxRepository outbox,
        ICurrentCompany currentCompany,
        ICurrentUser currentUser,
        ICommunicationSettingsResolver settings,
        ICommunicationTemplateResolver templates,
        ILogger<CommunicationQueue> logger
    )
    {
        _outbox = outbox;
        _currentCompany = currentCompany;
        _currentUser = currentUser;
        _settings = settings;
        _templates = templates;
        _logger = logger;
    }

    public async Task<QueuedCommunicationDto> EnqueueAsync(CommunicationRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Template);
        EnsureScopeMatchesAuthenticatedCompany(request.Scope);

        var identity = CommunicationIdentity.For(
            request.Scope,
            request.Purpose,
            request.Channel,
            request.Source,
            request.RecipientRole
        );

        var maxRetries = request.MaxRetries
            ?? (await _settings.ResolveEmailAsync(request.Scope, ct)).MaxRetries;

        CommunicationOutbox communication;
        string? templateFailureCode = null;
        try
        {
            var rendered = await RenderAsync(request, identity, ct);
            communication = CommunicationOutbox.CreateEmail(
                identity,
                request.RecipientName,
                request.RecipientEmail,
                rendered.Usage,
                rendered.Subject,
                rendered.Html,
                rendered.Text,
                request.Priority,
                request.ScheduledAtUtc,
                maxRetries,
                _currentUser.UserId
            );
        }
        catch (CommunicationTemplateException failure)
        {
            // Un fallo de template no revierte el hecho de negocio NI pierde la intención: queda una fila
            // Failed (misma identidad, sin contenido inventado, nunca enviable) con las variables para
            // re-renderizarla tras corregir el template.
            templateFailureCode = failure.Code;
            communication = TemplateFailure(request, identity, failure, maxRetries);
        }

        foreach (var attachment in request.Attachments ?? [])
        {
            communication.AddAttachment(
                attachment.AttachmentType,
                attachment.FileName,
                attachment.ContentType,
                attachment.FileStoragePath,
                attachment.BinaryContent,
                _currentUser.UserId
            );
        }

        var result = await _outbox.EnqueueAsync(communication, ct);
        if (templateFailureCode is null)
            LogTemplateUsed(result.Id, communication.TemplateKey!, communication.TemplateVersion!.Value, communication.TemplateSource!.Value, request.Source.Id, !result.Created);
        else
            LogTemplateFailed(result.Id, identity.Purpose, templateFailureCode, request.Source.Id, !result.Created);

        return new QueuedCommunicationDto(result.Id, WasAlreadyQueued: !result.Created, templateFailureCode);
    }

    private CommunicationOutbox TemplateFailure(
        CommunicationRequest request,
        CommunicationIdentity identity,
        CommunicationTemplateException failure,
        int maxRetries
    )
    {
        var category = failure.Code == ApiResponseCodes.Communications.TemplateRenderFailed
            ? CommunicationFailureCategory.Permanent
            : CommunicationFailureCategory.Configuration;
        var payload = CommunicationPurposes.Get(identity.Purpose).IsSensitive
            ? null
            : JsonSerializer.Serialize(request.Template.ToVariables());

        return CommunicationOutbox.CreateEmailTemplateFailure(
            identity,
            request.RecipientName,
            request.RecipientEmail,
            category,
            $"{failure.Code}: {failure.Message}",
            payload,
            request.Priority,
            maxRetries,
            _currentUser.UserId
        );
    }

    private async Task<RenderedCommunicationTemplate> RenderAsync(
        CommunicationRequest request,
        CommunicationIdentity identity,
        CancellationToken ct
    )
    {
        // TemplateKey = Purpose (ADR-039 D12): el modelo debe pertenecer al template del propósito.
        if (!string.Equals(request.Template.TemplateKey, identity.Purpose, StringComparison.Ordinal))
            throw new CommunicationTemplateException(
                ApiResponseCodes.Communications.TemplateRenderFailed,
                $"El modelo del template {request.Template.TemplateKey} no corresponde al propósito {identity.Purpose}."
            );

        var definition = await _templates.ResolveAsync(request.Scope, identity.Purpose, ct);
        if (!definition.IsSuccess)
            throw new CommunicationTemplateException(definition.Code!, definition.Error!);

        var rendered = CommunicationTemplateRenderer.Render(definition.Value!, request.Template);
        if (!rendered.IsSuccess)
            throw new CommunicationTemplateException(rendered.Code!, rendered.Error!);

        return rendered.Value!;
    }

    private void EnsureScopeMatchesAuthenticatedCompany(CommunicationScope scope)
    {
        if (scope.Kind != CommunicationScopeKind.Company || !_currentCompany.HasCompanyContext)
            return;

        if (scope.CompanyId != _currentCompany.CompanyId)
            throw new InvalidOperationException(
                "El alcance explícito de la comunicación no coincide con la empresa del contexto autenticado."
            );
    }

    // Solo metadatos del template: nunca variables, asunto ni cuerpo.
    [LoggerMessage(EventId = 4210, EventName = "CommunicationTemplateRendered", Level = LogLevel.Information,
        Message = "Communications: {CommunicationId} rendered with template {TemplateKey} v{TemplateVersion} ({TemplateSource}) source={SourceId} alreadyQueued={WasAlreadyQueued}")]
    private partial void LogTemplateUsed(Guid communicationId, string templateKey, int templateVersion, CommunicationTemplateSource templateSource, Guid sourceId, bool wasAlreadyQueued);

    [LoggerMessage(EventId = 4211, EventName = "CommunicationTemplateFailed", Level = LogLevel.Warning,
        Message = "Communications: {CommunicationId} recorded as Failed without content: template {TemplateKey} failed with {FailureCode} source={SourceId} alreadyQueued={WasAlreadyQueued}; fix the template and requeue")]
    private partial void LogTemplateFailed(Guid communicationId, string templateKey, string failureCode, Guid sourceId, bool wasAlreadyQueued);
}
