using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Seeding;

/// <summary>
/// Runs all default-data creation steps when a new tenant is registered.
/// Add new steps by adding a private method and calling it from <see cref="OnboardAsync"/>.
/// Every step is idempotent (checks existence before inserting).
/// </summary>
public sealed class SubscriberOnboardingService : ISubscriberOnboardingService
{
    // ── Constants ─────────────────────────────────────────────────────────────

    /// <summary>Ecuador SRI standard identification for "Consumidor Final".</summary>
    private const string ConsumidorFinalIdType   = "CI";
    private const string ConsumidorFinalIdNumber = "9999999999";
    private const string ConsumidorFinalName     = "CONSUMIDOR FINAL";

    private const string MainBranchCode  = "SUC-001";
    private const string MainBranchName  = "Sucursal Principal";
    private const string MainBranchAddr  = "Dirección principal";

    private const string MainWarehouseCode = "WH-001";
    private const string MainWarehouseName = "Bodega Principal";

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
        _logger.LogInformation("Onboarding tenant {SubscriberId}…", subscriberId);

        // ── Step 1: access profiles ──────────────────────────────────────────
        await _profileSeeder.SeedForTenantAsync(subscriberId, actorId, ct);

        // ── Step 2: Consumidor Final customer ────────────────────────────────
        await SeedConsumidorFinalAsync(subscriberId, actorId, ct);

        // ── Step 3: main branch (must exist before warehouse) ────────────────
        var branchId = await SeedMainBranchAsync(subscriberId, actorId, ct);

        // ── Step 4: main warehouse linked to the branch ──────────────────────
        await SeedMainWarehouseAsync(subscriberId, branchId, actorId, ct);

        // ────────────────────────────────────────────────────────────────────
        // Add new onboarding steps here ↓
        // await SeedDefaultTaxSettingsAsync(subscriberId, actorId, ct);
        // ────────────────────────────────────────────────────────────────────

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
            legalName:           ConsumidorFinalName,
            createdBy:           actorId);

        _db.BusinessPartners.Add(bp);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Consumidor Final seeded for tenant {SubscriberId}.", subscriberId);
    }

    /// <summary>
    /// Creates the main branch (Sucursal Principal).
    /// Returns the branch Id so the warehouse can link to it.
    /// </summary>
    private async Task<Guid> SeedMainBranchAsync(Guid subscriberId, Guid actorId, CancellationToken ct)
    {
        var existing = await _platform
            .Unfiltered(_db.Branches, PlatformQueryReason.Seeding)
            .Where(b => b.SubscriberId == subscriberId && b.Code == MainBranchCode)
            .Select(b => (Guid?)b.Id)
            .FirstOrDefaultAsync(ct);

        if (existing.HasValue)
        {
            _logger.LogDebug("Main branch already exists for tenant {SubscriberId}. Skipping.", subscriberId);
            return existing.Value;
        }

        var branch = Branch.Create(
            subscriberId:        subscriberId,
            name:            MainBranchName,
            address:         MainBranchAddr,
            code:            MainBranchCode,
            branchType:      null,
            reference:       null,
            phones:          null,
            email:           null,
            managerName:     null,
            countryId:       null,
            provinceId:      null,
            cantonId:        null,
            parishId:        null,
            latitude:        null,
            longitude:       null,
            storageCapacity: null,
            dailySalesGoal:  null,
            rechargeOption:  null,
            isMainBranch:    true,
            createdBy:       actorId);

        _db.Branches.Add(branch);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Main branch seeded for tenant {SubscriberId} (id={BranchId}).", subscriberId, branch.Id);
        return branch.Id;
    }

    /// <summary>
    /// Creates the main warehouse (Bodega Principal) linked to <paramref name="branchId"/>.
    /// </summary>
    private async Task SeedMainWarehouseAsync(Guid subscriberId, Guid branchId, Guid actorId, CancellationToken ct)
    {
        var exists = await _platform
            .Unfiltered(_db.Warehouses, PlatformQueryReason.Seeding)
            .AnyAsync(w => w.SubscriberId == subscriberId && w.Code == MainWarehouseCode, ct);

        if (exists)
        {
            _logger.LogDebug("Main warehouse already exists for tenant {SubscriberId}. Skipping.", subscriberId);
            return;
        }

        var warehouse = Warehouse.Create(
            subscriberId:          subscriberId,
            branchId:          branchId,
            name:              MainWarehouseName,
            code:              MainWarehouseCode,
            storageType:       null,
            address:           null,
            phone:             null,
            email:             null,
            manager:           null,
            latitude:          null,
            longitude:         null,
            capacity:          null,
            dailyDispatchGoal: null,
            createdBy:         actorId);

        _db.Warehouses.Add(warehouse);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Main warehouse seeded for tenant {SubscriberId} (id={WarehouseId}).", subscriberId, warehouse.Id);
    }
}
