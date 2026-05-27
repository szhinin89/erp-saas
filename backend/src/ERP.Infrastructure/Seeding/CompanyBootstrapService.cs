using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Seeding;

/// <summary>
/// Crea datos operativos mínimos para cada nueva empresa.
/// Todos los pasos son idempotentes (verifican existencia antes de insertar).
/// </summary>
public sealed class CompanyBootstrapService : ICompanyBootstrapService
{
    private const string MainBranchName         = "Sucursal Principal";
    private const string MainBranchAddress      = "Dirección principal";
    private const string MainWarehouseName      = "Bodega Principal";
    private const string MainEstablishmentCode  = "001";
    private const string MainEstablishmentName  = "Establecimiento Principal";
    private const string MainEmissionPointCode  = "001";
    private const string MainEmissionPointName  = "Punto de Emisión Principal";

    private static readonly string[] SeedDocTypeCodes = ["01", "04", "05", "07"];

    private readonly ErpDbContext                         _db;
    private readonly IPlatformQueryAccessor               _platform;
    private readonly ILogger<CompanyBootstrapService>     _logger;

    public CompanyBootstrapService(
        ErpDbContext db,
        IPlatformQueryAccessor platform,
        ILogger<CompanyBootstrapService> logger)
    {
        _db       = db;
        _platform = platform;
        _logger   = logger;
    }

    public async Task BootstrapCompanyAsync(Guid subscriberId, Guid companyId, Guid actorId, CancellationToken ct = default)
    {
        _logger.LogInformation("Bootstrapping company {CompanyId} (subscriber {SubscriberId})…", companyId, subscriberId);

        var branchId        = await SeedMainBranchAsync(subscriberId, companyId, actorId, ct);
        await SeedMainWarehouseAsync(subscriberId, companyId, branchId, actorId, ct);

        var establishmentId = await SeedMainEstablishmentAsync(subscriberId, companyId, branchId, actorId, ct);
        var emissionPointId = await SeedMainEmissionPointAsync(subscriberId, companyId, establishmentId, actorId, ct);
        await SeedDocumentSequencesAsync(subscriberId, companyId, emissionPointId, ct);

        _logger.LogInformation("Company {CompanyId} bootstrap complete.", companyId);
    }

    private async Task<Guid> SeedMainEstablishmentAsync(Guid subscriberId, Guid companyId, Guid branchId, Guid actorId, CancellationToken ct)
    {
        var existing = await _platform
            .Unfiltered(_db.Establishments, PlatformQueryReason.Seeding)
            .Where(e => e.SubscriberId == subscriberId
                     && e.BranchId     == branchId
                     && e.Code         == MainEstablishmentCode)
            .Select(e => (Guid?)e.Id)
            .FirstOrDefaultAsync(ct);

        if (existing.HasValue)
        {
            _logger.LogDebug("Establishment {Code} already exists for branch {BranchId}. Skipping.", MainEstablishmentCode, branchId);
            return existing.Value;
        }

        var establishment = Establishment.Create(
            subscriberId: subscriberId,
            branchId:     branchId,
            companyId:    companyId,
            code:         MainEstablishmentCode,
            name:         MainEstablishmentName,
            address:      MainBranchAddress,
            phone:        null,
            isMain:       true,
            createdBy:    actorId);

        _db.Establishments.Add(establishment);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Establishment {Code} seeded for branch {BranchId} (id={EstabId}).", MainEstablishmentCode, branchId, establishment.Id);
        return establishment.Id;
    }

    private async Task<Guid> SeedMainEmissionPointAsync(Guid subscriberId, Guid companyId, Guid establishmentId, Guid actorId, CancellationToken ct)
    {
        var existing = await _platform
            .Unfiltered(_db.EmissionPoints, PlatformQueryReason.Seeding)
            .Where(ep => ep.SubscriberId    == subscriberId
                      && ep.EstablishmentId == establishmentId
                      && ep.Code            == MainEmissionPointCode)
            .Select(ep => (Guid?)ep.Id)
            .FirstOrDefaultAsync(ct);

        if (existing.HasValue)
        {
            _logger.LogDebug("EmissionPoint {Code} already exists for establishment {EstabId}. Skipping.", MainEmissionPointCode, establishmentId);
            return existing.Value;
        }

        var emissionPoint = EmissionPoint.Create(
            subscriberId:    subscriberId,
            companyId:       companyId,
            establishmentId: establishmentId,
            code:            MainEmissionPointCode,
            name:            MainEmissionPointName,
            isDefault:       true,
            createdBy:       actorId);

        _db.EmissionPoints.Add(emissionPoint);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("EmissionPoint {Code} seeded for establishment {EstabId} (id={EmPointId}).", MainEmissionPointCode, establishmentId, emissionPoint.Id);
        return emissionPoint.Id;
    }

    private async Task SeedDocumentSequencesAsync(Guid subscriberId, Guid companyId, Guid emissionPointId, CancellationToken ct)
    {
        foreach (var docTypeCode in SeedDocTypeCodes)
        {
            var exists = await _platform
                .Unfiltered(_db.DocumentSequences, PlatformQueryReason.Seeding)
                .AnyAsync(ds => ds.EmissionPointId == emissionPointId
                             && ds.DocTypeCode     == docTypeCode, ct);

            if (exists)
            {
                _logger.LogDebug("DocumentSequence {DocType} already exists for emissionPoint {EmPointId}. Skipping.", docTypeCode, emissionPointId);
                continue;
            }

            var sequence = DocumentSequence.Create(
                subscriberId:    subscriberId,
                companyId:       companyId,
                emissionPointId: emissionPointId,
                docTypeCode:     docTypeCode);

            _db.DocumentSequences.Add(sequence);
            _logger.LogInformation("DocumentSequence {DocType} seeded for emissionPoint {EmPointId}.", docTypeCode, emissionPointId);
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task<Guid> SeedMainBranchAsync(Guid subscriberId, Guid companyId, Guid actorId, CancellationToken ct)
    {
        var branchCode = $"SUC-{companyId.ToString()[..8].ToUpperInvariant()}";

        var existing = await _platform
            .Unfiltered(_db.Branches, PlatformQueryReason.Seeding)
            .Where(b => b.SubscriberId == subscriberId && b.Code == branchCode)
            .Select(b => (Guid?)b.Id)
            .FirstOrDefaultAsync(ct);

        if (existing.HasValue)
        {
            _logger.LogDebug("Branch {Code} already exists for company {CompanyId}. Skipping.", branchCode, companyId);
            return existing.Value;
        }

        var branch = Branch.Create(
            subscriberId:    subscriberId,
            name:            MainBranchName,
            address:         MainBranchAddress,
            code:            branchCode,
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
            createdBy:       actorId,
            companyId:       companyId);

        _db.Branches.Add(branch);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Branch {Code} seeded for company {CompanyId} (id={BranchId}).", branchCode, companyId, branch.Id);
        return branch.Id;
    }

    private async Task SeedMainWarehouseAsync(Guid subscriberId, Guid companyId, Guid branchId, Guid actorId, CancellationToken ct)
    {
        var warehouseCode = $"WH-{companyId.ToString()[..8].ToUpperInvariant()}";

        var exists = await _platform
            .Unfiltered(_db.Warehouses, PlatformQueryReason.Seeding)
            .AnyAsync(w => w.CompanyId == companyId && w.Code == warehouseCode, ct);

        if (exists)
        {
            _logger.LogDebug("Warehouse {Code} already exists for company {CompanyId}. Skipping.", warehouseCode, companyId);
            return;
        }

        var warehouse = Warehouse.Create(
            subscriberId:      subscriberId,
            branchId:          branchId,
            name:              MainWarehouseName,
            code:              warehouseCode,
            storageType:       null,
            address:           null,
            phone:             null,
            email:             null,
            manager:           null,
            latitude:          null,
            longitude:         null,
            capacity:          null,
            dailyDispatchGoal: null,
            createdBy:         actorId,
            companyId:         companyId);

        _db.Warehouses.Add(warehouse);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Warehouse {Code} seeded for company {CompanyId} (id={WarehouseId}).", warehouseCode, companyId, warehouse.Id);
    }
}
