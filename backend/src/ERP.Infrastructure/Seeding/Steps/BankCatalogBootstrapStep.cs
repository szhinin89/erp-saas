using ERP.Application.Common.Interfaces;

namespace ERP.Infrastructure.Seeding.Steps;

/// <summary>
/// BANK-CATALOG-01: siembra el catálogo mínimo de Bancos (Ecuador) para cada empresa nueva.
/// Delega toda la lógica idempotente a <see cref="BankCatalogSeeder"/> — la misma clase que usa
/// <see cref="BankCatalogBackfillService"/> para tenants ya existentes, sin duplicar la lista.
/// </summary>
public sealed class BankCatalogBootstrapStep : ICompanyBootstrapStep
{
    public int Order => CompanyBootstrapStepOrder.BankCatalog;

    private readonly BankCatalogSeeder _seeder;

    public BankCatalogBootstrapStep(BankCatalogSeeder seeder)
    {
        _seeder = seeder;
    }

    public async Task ExecuteAsync(
        CompanyBootstrapContext context,
        CancellationToken cancellationToken = default
    )
    {
        var (tenantId, _, actorId) = context;
        await _seeder.SeedAsync(tenantId, actorId, cancellationToken);
    }
}
