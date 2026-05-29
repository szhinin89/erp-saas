using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Platform.Provisioning;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Platform.Provisioning;
using ERP.Domain.Subscribers.Entities;
using ERP.Domain.Subscribers.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Auth.UseCases.PlatformOwnerSetup;

/// <summary>
/// Provisions the internal platform owner (Subscriber + Company) during first-run setup.
/// Idempotent: if an internal platform owner already exists, returns success without re-creating.
/// Uses a distributed lock to prevent concurrent executions in multi-instance environments.
/// </summary>
public sealed class PlatformOwnerSetupHandler
    : IRequestHandler<PlatformOwnerSetupCommand, Result<PlatformOwnerSetupResultDto>>
{
    private readonly ISubscriberRepository              _subscribers;
    private readonly ICompanyRepository                 _companies;
    private readonly IFirstRunSetupService              _firstRunSetup;
    private readonly IPlatformProvisioningLockService   _lock;
    private readonly IPlatformProvisioningAuditService  _audit;
    private readonly ILogger<PlatformOwnerSetupHandler> _log;

    public PlatformOwnerSetupHandler(
        ISubscriberRepository subscribers,
        ICompanyRepository companies,
        IFirstRunSetupService firstRunSetup,
        IPlatformProvisioningLockService @lock,
        IPlatformProvisioningAuditService audit,
        ILogger<PlatformOwnerSetupHandler> log)
    {
        _subscribers  = subscribers;
        _companies    = companies;
        _firstRunSetup = firstRunSetup;
        _lock          = @lock;
        _audit         = audit;
        _log           = log;
    }

    public async Task<Result<PlatformOwnerSetupResultDto>> Handle(
        PlatformOwnerSetupCommand command, CancellationToken ct)
    {
        // Validate first-run token
        if (!await _firstRunSetup.ValidateSetupTokenAsync(command.SetupToken, ct))
            return Result<PlatformOwnerSetupResultDto>.Failure(
                "Token de instalación inválido o expirado.");

        // Idempotency: return existing if already created
        var all = await _subscribers.GetAllAsync(ct);
        var existing = all.FirstOrDefault(s => s.IsInternalPlatformOwner);
        if (existing is not null)
        {
            _log.LogDebug("PlatformOwnerSetup: internal owner already exists id={Id}", existing.Id);
            var existingCompany = (await _companies.GetActiveBySubscriberIdAsync(existing.Id, ct))
                .FirstOrDefault();
            return Result<PlatformOwnerSetupResultDto>.Success(new PlatformOwnerSetupResultDto(
                existing.Id,
                existingCompany?.Id ?? Guid.Empty,
                existing.Name,
                existingCompany?.Ruc ?? command.TaxId,
                existing.Slug,
                "Platform owner already exists."));
        }

        // Distributed lock — fail fast if another instance is provisioning
        var instanceId = $"{Environment.MachineName}:{Environment.ProcessId}";
        var (acquired, expiresAt) = await _lock.TryAcquireAsync(instanceId, ct);
        if (!acquired)
        {
            return Result<PlatformOwnerSetupResultDto>.Failure(
                "El aprovisionamiento ya está en curso en otra instancia. Reintente en un momento.");
        }

        await _audit.RecordAsync(ProvisioningEventType.LockAcquired, true,
            instanceId: instanceId,
            metadata: $"{{\"expiresAt\":\"{expiresAt:O}\"}}",
            ct: ct);

        await _audit.RecordAsync(ProvisioningEventType.ProvisioningStarted, true,
            instanceId: instanceId,
            metadata: $"{{\"taxId\":\"{command.TaxId}\",\"platformName\":\"{command.PlatformName}\"}}",
            ct: ct);

        try
        {
            // Validate TaxId uniqueness
            var taxId = command.TaxId.Trim();
            var existingByRuc = await _companies.GetByRucAsync(taxId, ct);
            if (existingByRuc is not null)
            {
                await _audit.RecordAsync(ProvisioningEventType.ProvisioningFailed, false,
                    instanceId: instanceId,
                    errorMessage: $"RUC '{taxId}' ya está registrado.",
                    ct: ct);
                return Result<PlatformOwnerSetupResultDto>.Failure(
                    $"El RUC '{taxId}' ya está registrado.");
            }

            // Generate slug from platform name
            var slug = GenerateSlug(command.PlatformName);
            var existingBySlug = await _subscribers.GetBySlugAsync(slug, ct);
            if (existingBySlug is not null)
                slug = $"{slug}-{DateTime.UtcNow:yyyyMMddHHmm}";

            var actorId = Guid.Empty;

            // Create the platform owner subscriber
            var subscriber = Subscriber.CreatePlatformOwner(
                name:              command.PlatformName.Trim(),
                slug:              slug,
                createdBy:         actorId,
                preferredLanguage: command.PreferredLanguage);

            await _subscribers.AddAsync(subscriber, ct);
            await _subscribers.SaveChangesAsync(ct);

            await _audit.RecordAsync(ProvisioningEventType.OwnerCreated, true,
                instanceId:  instanceId,
                subscriberId: subscriber.Id,
                metadata:    $"{{\"slug\":\"{subscriber.Slug}\"}}",
                ct: ct);

            // Create the associated company and mark it as platform-internal
            var company = Company.CreateManaged(
                subscriberId:       subscriber.Id,
                ruc:                taxId,
                legalName:          command.LegalName.Trim(),
                mainAddress:        command.MainAddress.Trim(),
                tradeName:          command.TradeName?.Trim(),
                email:              command.Email.Trim(),
                phone:              null,
                countryCode:        "ECU",
                timezone:           string.IsNullOrWhiteSpace(command.Timezone)
                                        ? "America/Guayaquil"
                                        : command.Timezone.Trim(),
                currencyCode:       "USD",
                isProvisionalTaxId: false,
                taxIdStatus:        TaxIdStatus.Verified);

            company.MarkAsPlatformInternal();

            await _companies.AddAsync(company, ct);
            await _companies.SaveChangesAsync(ct);

            await _audit.RecordAsync(ProvisioningEventType.ProvisioningCompleted, true,
                instanceId:   instanceId,
                subscriberId: subscriber.Id,
                companyId:    company.Id,
                metadata:     $"{{\"ruc\":\"{company.Ruc}\",\"legalName\":\"{company.LegalName}\"}}",
                ct: ct);

            _log.LogInformation(
                "PlatformOwnerSetup: created subscriber={SubscriberId} company={CompanyId} taxId={TaxId}",
                subscriber.Id, company.Id, taxId);

            return Result<PlatformOwnerSetupResultDto>.Success(new PlatformOwnerSetupResultDto(
                subscriber.Id,
                company.Id,
                subscriber.Name,
                company.Ruc,
                subscriber.Slug,
                "Platform owner provisioned successfully."));
        }
        catch (Exception ex)
        {
            await _audit.RecordAsync(ProvisioningEventType.ProvisioningFailed, false,
                instanceId:   instanceId,
                errorMessage: ex.Message,
                ct: ct);
            _log.LogError(ex, "PlatformOwnerSetup failed");
            throw;
        }
        finally
        {
            await _lock.ReleaseAsync(ct);
        }
    }

    private static string GenerateSlug(string name) =>
        name.Trim().ToLowerInvariant()
            .Replace(" ", "-")
            .Replace(".", "")
            .Replace(",", "")
            .Replace("&", "and")
            .TrimEnd('-');
}
