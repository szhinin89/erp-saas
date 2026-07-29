using ERP.Application.Common.Interfaces;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Seeding.Steps;

/// <summary>
/// Caja Principal de la empresa (Tipo A — infraestructura del sistema, ver política de Bootstrap
/// en <c>CLAUDE.md</c>). Depende de <see cref="OrganizationBootstrapStep"/> (sucursal principal) y
/// de <see cref="ElectronicDocumentsBootstrapStep"/> (punto de emisión por defecto), y localiza
/// ambos consultando la base de datos — nunca por referencia directa a esos steps. Idempotente.
/// </summary>
public sealed partial class CajaBootstrapStep : ICompanyBootstrapStep
{
    public int Order => CompanyBootstrapStepOrder.Caja;

    private const string MainCashRegisterCode = "001";
    private const string MainCashRegisterName = "Caja Principal";

    private readonly ErpDbContext _db;
    private readonly ILogger<CajaBootstrapStep> _logger;

    public CajaBootstrapStep(ErpDbContext db, ILogger<CajaBootstrapStep> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task ExecuteAsync(CompanyBootstrapContext context, CancellationToken cancellationToken = default)
    {
        var (tenantId, companyId, actorId) = context;

        var branchId = await _db.Branches.IgnoreQueryFilters()
            .Where(b => b.TenantId == tenantId && b.CompanyId == companyId && b.IsMainBranch)
            .Select(b => (Guid?)b.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (!branchId.HasValue)
        {
            LogNoMainBranch(companyId);
            return;
        }

        var exists = await _db.CashRegisters.IgnoreQueryFilters()
            .AnyAsync(cr => cr.TenantId == tenantId
                         && cr.BranchId == branchId.Value
                         && cr.Code == MainCashRegisterCode, cancellationToken);

        if (exists)
        {
            LogCashRegisterSkipped(MainCashRegisterCode, branchId.Value);
            return;
        }

        var emissionPointId = await _db.EmissionPoints.IgnoreQueryFilters()
            .Where(ep => ep.TenantId == tenantId && ep.CompanyId == companyId && ep.IsDefault)
            .Select(ep => (Guid?)ep.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var cashRegister = CashRegister.CreateSystemSeeded(
            tenantId: tenantId,
            companyId: companyId,
            branchId: branchId.Value,
            code: MainCashRegisterCode,
            name: MainCashRegisterName,
            createdBy: actorId,
            emissionPointId: emissionPointId);

        _db.CashRegisters.Add(cashRegister);
        await _db.SaveChangesAsync(cancellationToken);

        LogCashRegisterSeeded(MainCashRegisterCode, branchId.Value, cashRegister.Id);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "No main Branch found for company {CompanyId}. Skipping cash register seeding.")]
    private partial void LogNoMainBranch(Guid companyId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "CashRegister {Code} already exists for branch {BranchId}. Skipping.")]
    private partial void LogCashRegisterSkipped(string code, Guid branchId);

    [LoggerMessage(Level = LogLevel.Information, Message = "CashRegister {Code} seeded for branch {BranchId} (id={CashRegisterId}).")]
    private partial void LogCashRegisterSeeded(string code, Guid branchId, Guid cashRegisterId);
}
