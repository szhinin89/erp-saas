using ERP.Application.Common.Interfaces;
using ERP.Domain.Configuration.Entities;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Seeding.Steps;

/// <summary>
/// ERP-PRECISION-POLICY-SSOT-CLEANUP-04 — toda empresa nace con su <see cref="CompanyPrecisionPolicy"/>
/// (perfil Estándar comercial, valores de <see cref="PrecisionPolicyDefinitions"/>). Es lo que
/// permite que la lectura NO cree políticas: la fila existe desde el aprovisionamiento. Idempotente.
/// </summary>
public sealed class PrecisionPolicyBootstrapStep : ICompanyBootstrapStep
{
    public int Order => CompanyBootstrapStepOrder.PrecisionPolicy;

    private readonly ErpDbContext _db;

    public PrecisionPolicyBootstrapStep(ErpDbContext db) => _db = db;

    public async Task ExecuteAsync(
        CompanyBootstrapContext context,
        CancellationToken cancellationToken = default
    )
    {
        var (tenantId, companyId, actorId) = context;

        var exists = await _db
            .CompanyPrecisionPolicies.IgnoreQueryFilters()
            .AnyAsync(p => p.TenantId == tenantId && p.CompanyId == companyId, cancellationToken);
        if (exists)
            return;

        await _db.CompanyPrecisionPolicies.AddAsync(
            CompanyPrecisionPolicy.CreateStandardCommercial(tenantId, companyId, actorId),
            cancellationToken
        );
        await _db.SaveChangesAsync(cancellationToken);
    }
}
