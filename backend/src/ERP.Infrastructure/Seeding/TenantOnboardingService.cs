using ERP.Application.Common.Interfaces;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Seeding;

/// <summary>
/// Runs all default-data creation steps when a new tenant is registered.
/// Add new steps by adding a private method and calling it from <see cref="OnboardAsync"/>.
/// Every step is idempotent (checks existence before inserting).
/// </summary>
public sealed class TenantOnboardingService : ITenantOnboardingService
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
    private readonly ILogger<TenantOnboardingService> _logger;

    public TenantOnboardingService(
        ErpDbContext db,
        IDefaultProfileSeeder profileSeeder,
        ILogger<TenantOnboardingService> logger)
    {
        _db            = db;
        _profileSeeder = profileSeeder;
        _logger        = logger;
    }

    // ── Entry point ───────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task OnboardAsync(Guid tenantId, Guid actorId, CancellationToken ct = default)
    {
        _logger.LogInformation("Onboarding tenant {TenantId}…", tenantId);

        // ── Step 1: access profiles ──────────────────────────────────────────
        await _profileSeeder.SeedForTenantAsync(tenantId, actorId, ct);

        // ── Step 2: Consumidor Final customer ────────────────────────────────
        await SeedConsumidorFinalAsync(tenantId, actorId, ct);

        // ── Step 3: main branch (must exist before warehouse) ────────────────
        var branchId = await SeedMainBranchAsync(tenantId, actorId, ct);

        // ── Step 4: main warehouse linked to the branch ──────────────────────
        await SeedMainWarehouseAsync(tenantId, branchId, actorId, ct);

        // ────────────────────────────────────────────────────────────────────
        // Add new onboarding steps here ↓
        // await SeedDefaultTaxSettingsAsync(tenantId, actorId, ct);
        // ────────────────────────────────────────────────────────────────────

        _logger.LogInformation("Tenant {TenantId} onboarding complete.", tenantId);
    }

    // ── Step implementations ─────────────────────────────────────────────────

    /// <summary>
    /// Creates the "CONSUMIDOR FINAL" customer (CI 9999999999).
    /// Required for SRI invoicing when the buyer is unidentified.
    /// </summary>
    private async Task SeedConsumidorFinalAsync(Guid tenantId, Guid actorId, CancellationToken ct)
    {
        var exists = await _db.Customers
            .IgnoreQueryFilters()
            .AnyAsync(c => c.TenantId == tenantId
                        && c.IdentificationNumber == ConsumidorFinalIdNumber, ct);

        if (exists)
        {
            _logger.LogDebug("Consumidor Final already exists for tenant {TenantId}. Skipping.", tenantId);
            return;
        }

        var customer = Customer.Create(
            tenantId:            tenantId,
            identificationType:  ConsumidorFinalIdType,
            identificationNumber: ConsumidorFinalIdNumber,
            legalName:           ConsumidorFinalName,
            tradeName:           null,
            addressLine:         null,
            phone:               null,
            email:               null,
            notes:               "Default customer for SRI invoices without identified buyer.",
            createdBy:           actorId);

        _db.Customers.Add(customer);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Consumidor Final customer seeded for tenant {TenantId}.", tenantId);
    }

    /// <summary>
    /// Creates the main branch (Sucursal Principal).
    /// Returns the branch Id so the warehouse can link to it.
    /// </summary>
    private async Task<Guid> SeedMainBranchAsync(Guid tenantId, Guid actorId, CancellationToken ct)
    {
        var existing = await _db.Branches
            .IgnoreQueryFilters()
            .Where(b => b.TenantId == tenantId && b.Code == MainBranchCode)
            .Select(b => (Guid?)b.Id)
            .FirstOrDefaultAsync(ct);

        if (existing.HasValue)
        {
            _logger.LogDebug("Main branch already exists for tenant {TenantId}. Skipping.", tenantId);
            return existing.Value;
        }

        var branch = Branch.Create(
            tenantId:        tenantId,
            name:            MainBranchName,
            address:         MainBranchAddr,
            code:            MainBranchCode,
            branchType:      null,
            reference:       null,
            phones:          null,
            email:           null,
            managerName:     null,
            countryId:       "EC",
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

        _logger.LogInformation("Main branch seeded for tenant {TenantId} (id={BranchId}).", tenantId, branch.Id);
        return branch.Id;
    }

    /// <summary>
    /// Creates the main warehouse (Bodega Principal) linked to <paramref name="branchId"/>.
    /// </summary>
    private async Task SeedMainWarehouseAsync(Guid tenantId, Guid branchId, Guid actorId, CancellationToken ct)
    {
        var exists = await _db.Warehouses
            .IgnoreQueryFilters()
            .AnyAsync(w => w.TenantId == tenantId && w.Code == MainWarehouseCode, ct);

        if (exists)
        {
            _logger.LogDebug("Main warehouse already exists for tenant {TenantId}. Skipping.", tenantId);
            return;
        }

        var warehouse = Warehouse.Create(
            tenantId:          tenantId,
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

        _logger.LogInformation("Main warehouse seeded for tenant {TenantId} (id={WarehouseId}).", tenantId, warehouse.Id);
    }
}
