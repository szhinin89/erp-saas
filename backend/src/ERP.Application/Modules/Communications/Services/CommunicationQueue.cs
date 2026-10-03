using ERP.Application.Common;
using ERP.Application.Modules.Communications.DTOs;
using ERP.Domain.Modules.Communications.Entities;
using ERP.Domain.Modules.Communications.Interfaces;

namespace ERP.Application.Modules.Communications.Services;

public sealed class CommunicationQueue : ICommunicationQueue
{
    private readonly ICommunicationOutboxRepository _outbox;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;
    private readonly ICurrentBranch _currentBranch;
    private readonly ICurrentUser _currentUser;
    private readonly ICommunicationSettingsResolver _settings;

    public CommunicationQueue(
        ICommunicationOutboxRepository outbox,
        IUnitOfWork unitOfWork,
        ICurrentTenant currentTenant,
        ICurrentCompany currentCompany,
        ICurrentBranch currentBranch,
        ICurrentUser currentUser,
        ICommunicationSettingsResolver settings
    )
    {
        _outbox = outbox;
        _unitOfWork = unitOfWork;
        _currentTenant = currentTenant;
        _currentCompany = currentCompany;
        _currentBranch = currentBranch;
        _currentUser = currentUser;
        _settings = settings;
    }

    public async Task<QueuedCommunicationDto> QueueEmailAsync(
        QueueEmailRequest request,
        CancellationToken ct = default
    )
    {
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var existing = await _outbox.GetByIdempotencyKeyAsync(
                _currentTenant.TenantId,
                _currentCompany.CompanyId,
                request.IdempotencyKey,
                ct
            );

            if (existing is not null)
                return new QueuedCommunicationDto(existing.Id, WasAlreadyQueued: true);
        }

        // ZH-COMMUNICATIONS-DELIVERY-HARDENING-01 — MaxRetries se fija AL ENCOLAR desde el perfil
        // resuelto (communications.email.max_retries / fallback de instancia) y queda copiado en la
        // fila: un cambio posterior de configuración no reinterpreta comunicaciones ya creadas.
        var maxRetries = request.MaxRetries
            ?? (await _settings.ResolveEmailAsync(ct)).MaxRetries;

        var actorId = _currentUser.UserId == Guid.Empty ? Guid.Empty : _currentUser.UserId;
        var branchId = request.BranchId.HasValue
            ? request.BranchId
            : _currentBranch.HasBranchContext
                ? _currentBranch.BranchId
                : (Guid?)null;
        var communication = CommunicationOutbox.CreateEmail(
            _currentTenant.TenantId,
            _currentCompany.CompanyId,
            branchId,
            request.Purpose,
            request.RecipientName,
            request.RecipientEmail,
            request.Subject,
            request.BodyHtml,
            request.BodyText,
            request.Priority,
            request.ScheduledAtUtc,
            maxRetries,
            request.CorrelationType,
            request.CorrelationId,
            request.IdempotencyKey,
            actorId
        );

        foreach (var attachment in request.Attachments ?? [])
        {
            communication.AddAttachment(
                attachment.AttachmentType,
                attachment.FileName,
                attachment.ContentType,
                attachment.FileStoragePath,
                attachment.BinaryContent,
                actorId
            );
        }

        await _outbox.AddAsync(communication, ct);
        if (request.SaveImmediately)
            await _unitOfWork.SaveChangesAsync(ct);

        return new QueuedCommunicationDto(communication.Id, WasAlreadyQueued: false);
    }
}
