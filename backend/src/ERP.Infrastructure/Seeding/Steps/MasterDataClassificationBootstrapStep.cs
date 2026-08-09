using ERP.Application.Common.Interfaces;

namespace ERP.Infrastructure.Seeding.Steps;

/// <summary>
/// Siembra los 12 catálogos de clasificación de BusinessPartner (CLASS-BP-CATALOGS-01) para cada
/// empresa nueva. Delega toda la lógica idempotente a <see cref="MasterDataClassificationSeeder"/>
/// — la misma clase que usa el backfill de empresas existentes, para no duplicar los seed lists.
/// </summary>
public sealed class MasterDataClassificationBootstrapStep : ICompanyBootstrapStep
{
    public int Order => CompanyBootstrapStepOrder.MasterDataClassifications;

    private readonly MasterDataClassificationSeeder _seeder;

    public MasterDataClassificationBootstrapStep(MasterDataClassificationSeeder seeder)
    {
        _seeder = seeder;
    }

    public Task ExecuteAsync(
        CompanyBootstrapContext context,
        CancellationToken cancellationToken = default
    )
    {
        var (tenantId, companyId, actorId) = context;
        return _seeder.SeedAsync(tenantId, companyId, actorId, cancellationToken);
    }
}
