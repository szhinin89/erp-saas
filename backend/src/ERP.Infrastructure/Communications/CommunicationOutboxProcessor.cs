using ERP.Application.Modules.Communications.Services;
using ERP.Domain.Modules.Communications.Enums;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Communications;

public sealed partial class CommunicationOutboxProcessor : ICommunicationOutboxProcessor
{
    private const int BatchSize = 50;
    private static readonly Guid SystemActorId = Guid.Empty;

    private readonly ErpDbContext _db;
    private readonly IEmailSender _emailSender;
    private readonly ICommunicationSettingsResolver _settingsResolver;
    private readonly ILogger<CommunicationOutboxProcessor> _logger;

    public CommunicationOutboxProcessor(
        ErpDbContext db,
        IEmailSender emailSender,
        ICommunicationSettingsResolver settingsResolver,
        ILogger<CommunicationOutboxProcessor> logger
    )
    {
        _db = db;
        _emailSender = emailSender;
        _settingsResolver = settingsResolver;
        _logger = logger;
    }

    public async Task ProcessPendingAsync(CancellationToken ct = default)
    {
        var utcNow = DateTime.UtcNow;
        var pending = await _db.CommunicationOutbox
            .IgnoreQueryFilters()
            .Include(x => x.Attachments)
            .Where(x =>
                x.Status == CommunicationStatus.Pending
                && x.ScheduledAtUtc <= utcNow
                && (x.NextAttemptAtUtc == null || x.NextAttemptAtUtc <= utcNow)
            )
            .OrderByDescending(x => x.Priority)
            .ThenBy(x => x.ScheduledAtUtc)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (pending.Count == 0)
            return;

        LogProcessingBatch(pending.Count);

        foreach (var communication in pending)
            await ProcessOneAsync(communication, ct);
    }

    private async Task ProcessOneAsync(
        Domain.Modules.Communications.Entities.CommunicationOutbox communication,
        CancellationToken ct
    )
    {
        using var context = JobExecutionContext.Begin(communication.TenantId, communication.CompanyId);
        _ = context;

        try
        {
            communication.MarkProcessing(SystemActorId);
            await _db.SaveChangesAsync(ct);

            if (communication.Channel != CommunicationChannel.Email)
                throw new NotSupportedException($"El canal {communication.Channel} todavía no tiene procesador.");

            var settings = await _settingsResolver.ResolveEmailAsync(ct);
            if (!settings.CanSend)
                throw new InvalidOperationException("La configuración SMTP de Communications está incompleta o inactiva.");

            await _emailSender.SendAsync(ToEmailMessage(communication), settings, ct);
            communication.MarkSent(SystemActorId);
            LogCommunicationSent(communication.Id, communication.TenantId, communication.CompanyId);
        }
        catch (Exception ex)
        {
            communication.MarkFailed(ex.Message, SystemActorId);
            LogCommunicationFailed(ex, communication.Id, communication.TenantId, communication.CompanyId, communication.RetryCount, communication.MaxRetries);
        }

        await _db.SaveChangesAsync(ct);
    }

    private static EmailMessage ToEmailMessage(
        Domain.Modules.Communications.Entities.CommunicationOutbox communication
    ) =>
        new(
            communication.RecipientEmail!,
            communication.RecipientName,
            communication.Subject,
            communication.BodyHtml,
            communication.BodyText,
            communication.Attachments
                .Select(a => new EmailAttachment(a.FileName, a.ContentType, a.FileStoragePath, a.BinaryContent))
                .ToList()
        );

    [LoggerMessage(Level = LogLevel.Debug, Message = "Communications: processing {Count} pending messages")]
    private partial void LogProcessingBatch(int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Communications: sent message {Id} tenant={TenantId} company={CompanyId}")]
    private partial void LogCommunicationSent(Guid id, Guid tenantId, Guid companyId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Communications: failed message {Id} tenant={TenantId} company={CompanyId} retry={RetryCount}/{MaxRetries}")]
    private partial void LogCommunicationFailed(Exception ex, Guid id, Guid tenantId, Guid companyId, int retryCount, int maxRetries);
}
