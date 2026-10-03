using ERP.Application.Common;
using ERP.Application.Modules.Communications.DTOs;
using ERP.Domain.Modules.Communications.Entities;
using ERP.Domain.Modules.Communications.Enums;
using ERP.Domain.Modules.Communications.Interfaces;
using ERP.Domain.Modules.Communications.ValueObjects;

namespace ERP.Application.Modules.Communications.Services;

/// <summary>
/// ZH-COMMUNICATIONS-CONTRACT-01 — encola con alcance EXPLÍCITO e identidad central:
/// <list type="bullet">
/// <item>El alcance sale de la request; el contexto ambiente solo se usa como guarda: una request
/// Company para otra empresa que la del contexto autenticado se rechaza (el caller no es autoridad
/// del scope).</item>
/// <item><see cref="CommunicationIdentity"/> construye la IdempotencyKey.</item>
/// <item><c>MaxRetries</c> se copia al encolar desde el perfil del mismo alcance (no se reinterpreta luego).</item>
/// <item>PostgreSQL es la autoridad de idempotencia (<see cref="ICommunicationOutboxRepository.EnqueueAsync"/>).</item>
/// </list>
/// </summary>
public sealed class CommunicationQueue : ICommunicationQueue
{
    private readonly ICommunicationOutboxRepository _outbox;
    private readonly ICurrentCompany _currentCompany;
    private readonly ICurrentUser _currentUser;
    private readonly ICommunicationSettingsResolver _settings;

    public CommunicationQueue(
        ICommunicationOutboxRepository outbox,
        ICurrentCompany currentCompany,
        ICurrentUser currentUser,
        ICommunicationSettingsResolver settings
    )
    {
        _outbox = outbox;
        _currentCompany = currentCompany;
        _currentUser = currentUser;
        _settings = settings;
    }

    public async Task<QueuedCommunicationDto> EnqueueAsync(CommunicationRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
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

        var communication = CommunicationOutbox.CreateEmail(
            identity,
            request.RecipientName,
            request.RecipientEmail,
            request.Subject,
            request.BodyHtml,
            request.BodyText,
            request.Priority,
            request.ScheduledAtUtc,
            maxRetries,
            _currentUser.UserId
        );

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
        return new QueuedCommunicationDto(result.Id, WasAlreadyQueued: !result.Created);
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
}
