using ERP.Application.Common.Interfaces;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Seeding.Steps;

/// <summary>
/// Estructura organizacional mínima de la empresa: Sucursal Principal, Bodega Principal,
/// Establecimiento y Punto de Emisión (código 001). Debe ejecutarse antes que
/// <see cref="ElectronicDocumentsBootstrapStep"/>, que consulta el punto de emisión creado aquí.
/// Todos los pasos son idempotentes (verifican existencia antes de insertar).
/// </summary>
public sealed partial class OrganizationBootstrapStep : ICompanyBootstrapStep
{
    public int Order => CompanyBootstrapStepOrder.Organization;

    private const string MainBranchName = "Sucursal Principal";
    /// <summary>
    /// Dirección real de la empresa (Tipo B): nunca se inventa. Se persiste vacía porque
    /// <c>Branch.Address</c> es <c>NOT NULL</c> a nivel de columna — el admin la completa después
    /// desde <c>UpdateBranch</c>. Ver política de Bootstrap en <c>CLAUDE.md</c>.
    /// </summary>
    private const string PendingAddress = "";
    private const string MainWarehouseName = "Bodega Principal";
    private const string MainEstablishmentCode = "001";
    private const string MainEstablishmentName = "Establecimiento Principal";
    private const string MainEmissionPointCode = "001";
    private const string MainEmissionPointName = "Punto de Emisión Principal";

    private readonly ErpDbContext _db;
    private readonly ILogger<OrganizationBootstrapStep> _logger;

    public OrganizationBootstrapStep(ErpDbContext db, ILogger<OrganizationBootstrapStep> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task ExecuteAsync(CompanyBootstrapContext context, CancellationToken cancellationToken = default)
    {
        var (tenantId, companyId, actorId) = context;

        var branchId = await SeedMainBranchAsync(tenantId, companyId, actorId, cancellationToken);
        await SeedMainWarehouseAsync(tenantId, companyId, branchId, actorId, cancellationToken);

        var establishmentId = await SeedMainEstablishmentAsync(tenantId, companyId, branchId, actorId, cancellationToken);
        await SeedMainEmissionPointAsync(tenantId, companyId, establishmentId, actorId, cancellationToken);
    }

    private async Task<Guid> SeedMainBranchAsync(Guid tenantId, Guid companyId, Guid actorId, CancellationToken cancellationToken)
    {
        var branchCode = $"SUC-{companyId.ToString()[..8].ToUpperInvariant()}";

        var existing = await _db.Branches.IgnoreQueryFilters()
            .Where(b => b.TenantId == tenantId && b.Code == branchCode)
            .Select(b => (Guid?)b.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing.HasValue)
        {
            LogBranchSkipped(branchCode, companyId);
            return existing.Value;
        }

        var branch = Branch.CreateSystemSeeded(
            tenantId: tenantId,
            name: MainBranchName,
            address: PendingAddress,
            code: branchCode,
            description: null,
            reference: null,
            postalCode: null,
            phone: null,
            secondaryPhone: null,
            email: null,
            website: null,
            managerName: null,
            managerPosition: null,
            managerEmail: null,
            managerPhone: null,
            countryId: null,
            provinceId: null,
            cantonId: null,
            parishId: null,
            latitude: null,
            longitude: null,
            openingDate: null,
            internalNotes: null,
            isMainBranch: true,
            createdBy: actorId,
            companyId: companyId);

        _db.Branches.Add(branch);
        await _db.SaveChangesAsync(cancellationToken);

        LogBranchSeeded(branchCode, companyId, branch.Id);
        return branch.Id;
    }

    private async Task SeedMainWarehouseAsync(Guid tenantId, Guid companyId, Guid branchId, Guid actorId, CancellationToken cancellationToken)
    {
        var warehouseCode = $"WH-{companyId.ToString()[..8].ToUpperInvariant()}";

        var exists = await _db.Warehouses.IgnoreQueryFilters()
            .AnyAsync(w => w.CompanyId == companyId && w.Code == warehouseCode, cancellationToken);

        if (exists)
        {
            LogWarehouseSkipped(warehouseCode, companyId);
            return;
        }

        var warehouse = Warehouse.CreateSystemSeeded(
            tenantId: tenantId,
            branchId: branchId,
            name: MainWarehouseName,
            code: warehouseCode,
            storageType: null,
            address: null,
            phone: null,
            email: null,
            manager: null,
            latitude: null,
            longitude: null,
            capacity: null,
            dailyDispatchGoal: null,
            createdBy: actorId,
            companyId: companyId,
            isMain: true);

        _db.Warehouses.Add(warehouse);
        await _db.SaveChangesAsync(cancellationToken);

        LogWarehouseSeeded(warehouseCode, companyId, warehouse.Id);
    }

    private async Task<Guid> SeedMainEstablishmentAsync(Guid tenantId, Guid companyId, Guid branchId, Guid actorId, CancellationToken cancellationToken)
    {
        var existing = await _db.Establishments.IgnoreQueryFilters()
            .Where(e => e.TenantId == tenantId
                     && e.BranchId == branchId
                     && e.Code == MainEstablishmentCode)
            .Select(e => (Guid?)e.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing.HasValue)
        {
            LogEstablishmentSkipped(MainEstablishmentCode, branchId);
            return existing.Value;
        }

        var establishment = Establishment.CreateSystemSeeded(
            tenantId: tenantId,
            branchId: branchId,
            companyId: companyId,
            code: MainEstablishmentCode,
            name: MainEstablishmentName,
            address: PendingAddress,
            phone: null,
            isMain: true,
            createdBy: actorId);

        _db.Establishments.Add(establishment);
        await _db.SaveChangesAsync(cancellationToken);

        LogEstablishmentSeeded(MainEstablishmentCode, branchId, establishment.Id);
        return establishment.Id;
    }

    private async Task<Guid> SeedMainEmissionPointAsync(Guid tenantId, Guid companyId, Guid establishmentId, Guid actorId, CancellationToken cancellationToken)
    {
        var existing = await _db.EmissionPoints.IgnoreQueryFilters()
            .Where(ep => ep.TenantId == tenantId
                      && ep.EstablishmentId == establishmentId
                      && ep.Code == MainEmissionPointCode)
            .Select(ep => (Guid?)ep.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing.HasValue)
        {
            LogEmissionPointSkipped(MainEmissionPointCode, establishmentId);
            return existing.Value;
        }

        var emissionPoint = EmissionPoint.CreateSystemSeeded(
            tenantId: tenantId,
            companyId: companyId,
            establishmentId: establishmentId,
            code: MainEmissionPointCode,
            name: MainEmissionPointName,
            emissionType: ERP.Domain.Modules.Company.Enums.EmissionType.Electronic,
            isDefault: true,
            createdBy: actorId);

        _db.EmissionPoints.Add(emissionPoint);
        await _db.SaveChangesAsync(cancellationToken);

        LogEmissionPointSeeded(MainEmissionPointCode, establishmentId, emissionPoint.Id);
        return emissionPoint.Id;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Branch {Code} already exists for company {CompanyId}. Skipping.")]
    private partial void LogBranchSkipped(string code, Guid companyId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Branch {Code} seeded for company {CompanyId} (id={BranchId}).")]
    private partial void LogBranchSeeded(string code, Guid companyId, Guid branchId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Warehouse {Code} already exists for company {CompanyId}. Skipping.")]
    private partial void LogWarehouseSkipped(string code, Guid companyId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Warehouse {Code} seeded for company {CompanyId} (id={WarehouseId}).")]
    private partial void LogWarehouseSeeded(string code, Guid companyId, Guid warehouseId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Establishment {Code} already exists for branch {BranchId}. Skipping.")]
    private partial void LogEstablishmentSkipped(string code, Guid branchId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Establishment {Code} seeded for branch {BranchId} (id={EstabId}).")]
    private partial void LogEstablishmentSeeded(string code, Guid branchId, Guid estabId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "EmissionPoint {Code} already exists for establishment {EstabId}. Skipping.")]
    private partial void LogEmissionPointSkipped(string code, Guid estabId);

    [LoggerMessage(Level = LogLevel.Information, Message = "EmissionPoint {Code} seeded for establishment {EstabId} (id={EmPointId}).")]
    private partial void LogEmissionPointSeeded(string code, Guid estabId, Guid emPointId);
}
