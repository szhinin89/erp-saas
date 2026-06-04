using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Seeding;

/// <summary>
/// Runs all subscriber-level default-data creation steps when a new tenant is registered.
/// Company-level data (Branch, Warehouse) is handled by <see cref="ICompanyBootstrapService"/>.
/// Every step is idempotent (checks existence before inserting).
/// </summary>
public sealed class SubscriberOnboardingService : ISubscriberOnboardingService
{
    // ── Constants ─────────────────────────────────────────────────────────────

    /// <summary>Consumidor Final — código SRI 07, número estándar ecuatoriano.</summary>
    private const string ConsumidorFinalIdType   = "07";
    private const string ConsumidorFinalIdNumber = "9999999999";
    private const string ConsumidorFinalName     = "CONSUMIDOR FINAL";

    // ── Dependencies ──────────────────────────────────────────────────────────

    private readonly ErpDbContext             _db;
    private readonly IDefaultProfileSeeder    _profileSeeder;
    private readonly IPlatformQueryAccessor _platform;
    private readonly ILogger<SubscriberOnboardingService> _logger;

    public SubscriberOnboardingService(
        ErpDbContext db,
        IDefaultProfileSeeder profileSeeder,
        IPlatformQueryAccessor platform,
        ILogger<SubscriberOnboardingService> logger)
    {
        _db            = db;
        _profileSeeder = profileSeeder;
        _platform      = platform;
        _logger        = logger;
    }

    // ── Entry point ───────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task OnboardAsync(Guid subscriberId, Guid actorId, CancellationToken ct = default)
    {
        _logger.LogInformation("Onboarding subscriber {SubscriberId}…", subscriberId);

        // ── Step 1: access profiles ──────────────────────────────────────────
        await _profileSeeder.SeedForSubscriberAsync(subscriberId, actorId, ct);

        // ── Step 2: Consumidor Final customer ────────────────────────────────
        await SeedConsumidorFinalAsync(subscriberId, actorId, ct);

        // ── Company-level data (Branch, Warehouse) ───────────────────────────
        // Handled by ICompanyBootstrapService — called from CreateCompanyHandler.

        _logger.LogInformation("Subscriber {SubscriberId} onboarding complete.", subscriberId);
    }

    // ── Step implementations ─────────────────────────────────────────────────

    private async Task SeedConsumidorFinalAsync(Guid subscriberId, Guid actorId, CancellationToken ct)
    {
        var exists = await _platform
            .Unfiltered(_db.BusinessPartners, PlatformQueryReason.Seeding)
            .AnyAsync(b => b.SubscriberId == subscriberId
                        && b.Identification.Number == ConsumidorFinalIdNumber, ct);

        if (exists)
        {
            _logger.LogDebug("Consumidor Final already exists for tenant {SubscriberId}. Skipping.", subscriberId);
            return;
        }

        var bp = BusinessPartner.Create(
            subscriberId:        subscriberId,
            identificationType:  ConsumidorFinalIdType,
            identificationNumber: ConsumidorFinalIdNumber,
            personType:          PersonType.Natural,
            legalName:           ConsumidorFinalName,
            createdBy:           actorId);

        var customerRole = BusinessPartnerRole.Create(
            subscriberId:      subscriberId,
            businessPartnerId: bp.Id,
            roleType:          RoleType.Customer,
            assignedBy:        actorId);

        _db.BusinessPartners.Add(bp);
        _db.BusinessPartnerRoles.Add(customerRole);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Consumidor Final seeded with Customer role for tenant {SubscriberId}.", subscriberId);
    }
}
