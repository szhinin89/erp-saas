using FluentAssertions;

namespace ERP.Infrastructure.Tests.Persistence;

/// <summary>
/// Evita nuevos usos directos de <c>IgnoreQueryFilters()</c> fuera del wrapper y excepciones documentadas.
/// </summary>
public sealed class IgnoreQueryFiltersAuditTests
{
    // ZH-AUTH-IGNORE-QUERY-FILTERS-GOVERNANCE-08 — los repositorios que antes aparecían aquí
    // (AccessRepository, UserSessionRepository, CompanyUserBranchRepository,
    // CompanyUserPreferencesRepository, EmissionPointRepository, EstablishmentRepository,
    // DocumentSequenceRepository, CashRegisterRepository, CashSessionRepository,
    // OrgSettingsRepository, ClassificationCatalogRepositoryBase) migraron de
    // `.IgnoreQueryFilters()` directo a `.AsPlatformQuery()` (mismo bypass, mismos predicados
    // manuales de tenant/company/membership ya verificados — cero cambio de comportamiento) y ya
    // no necesitan estar en esta lista. Quedan aquí únicamente los archivos que genuinamente
    // llaman `.IgnoreQueryFilters()` sin pasar por el wrapper: bootstrap/seeding/backfill
    // (cross-tenant por diseño, fuera del ciclo de vida de un repositorio request-scoped) y
    // health checks de plataforma.
    private static readonly HashSet<string> AllowedRelativePaths = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        "src/ERP.Infrastructure/Persistence/PlatformQueryAccessor.cs",
        "src/ERP.Infrastructure/MasterData/Reconciliation/BusinessPartnerReconciliationService.cs", // platform-level reconciliation
        "src/ERP.Infrastructure/Seeding/Steps/OrganizationBootstrapStep.cs", // bootstrap needs cross-tenant visibility
        "src/ERP.Infrastructure/Seeding/Steps/ElectronicDocumentsBootstrapStep.cs", // idem
        "src/ERP.Infrastructure/Seeding/Steps/InventoryBootstrapStep.cs", // idem
        "src/ERP.Infrastructure/Seeding/Steps/SalesBootstrapStep.cs", // idem
        "src/ERP.Infrastructure/Seeding/Steps/AccountingBootstrapStep.cs", // bootstrap needs cross-tenant visibility (hallazgo pre-existente, corregido en ACCOUNTING-PAYMENT-METHOD-ACCOUNT-MAPPING-14: faltaba en esta allowlist desde ACCOUNTING-INITIAL-CHART-SEED-11)
        "src/ERP.Infrastructure/Seeding/Steps/ExpensesCatalogBootstrapStep.cs", // bootstrap needs cross-tenant visibility; filtro explícito TenantId+CompanyId igual que AccountingBootstrapStep
        "src/ERP.Infrastructure/Seeding/AccountingChartBackfillService.cs", // backfill dev-only de companies ya existentes — mismo motivo que MasterDataClassificationBackfillService (hallazgo pre-existente, corregido en ACCOUNTING-PAYMENT-METHOD-ACCOUNT-MAPPING-14)
        "src/ERP.Infrastructure/Seeding/ExpensesCatalogBackfillService.cs", // EXPENSES-CATALOG-BOOTSTRAP-09-FIX: backfill dev-only de companies ya existentes — mismo motivo que AccountingChartBackfillService
        "src/ERP.Infrastructure/Seeding/DefaultProfileSeeder.cs", // seeder needs cross-tenant visibility
        "src/ERP.Infrastructure/Seeding/Steps/CajaBootstrapStep.cs", // bootstrap needs cross-tenant visibility
        "src/ERP.Infrastructure/Seeding/E2E/E2ESeedService.cs", // provisioning E2E fuera de Production, bajo bandera explícita: mismo motivo que los *BootstrapStep (bootstrap needs cross-tenant visibility)
        "src/ERP.API/Health/MembershipConsistencyHealthCheck.cs", // health check sin contexto de tenant: mismo motivo que BusinessPartnerReconciliationService (chequeo de integridad cross-tenant de solo lectura)
        "src/ERP.Infrastructure/Seeding/MasterDataClassificationSeeder.cs", // CLASS-BP-CATALOGS-01: reutilizado por bootstrap step (request-scoped) y backfill (multi-tenant, sin contexto HTTP ambiente); filtro explícito TenantId+CompanyId, fail-closed
        "src/ERP.Infrastructure/Seeding/MasterDataClassificationBackfillService.cs", // CLASS-BP-CATALOGS-01: itera todas las (TenantId, CompanyId) existentes para el backfill de empresas ya creadas — mismo motivo que los *BootstrapStep
        "src/ERP.Infrastructure/Seeding/Steps/DocumentFlowPolicyBootstrapStep.cs", // DOCUMENT-FLOW-POLICY-01: bootstrap needs cross-tenant visibility, mismo motivo que ExpensesCatalogBootstrapStep; filtro explícito TenantId+CompanyId reaplicado en la query
        "src/ERP.Infrastructure/Seeding/DocumentFlowPolicyBackfillService.cs", // DOCUMENT-FLOW-POLICY-01: backfill dev-only de companies ya existentes — mismo motivo que ExpensesCatalogBackfillService
    };

    [Fact]
    public void IgnoreQueryFilters_is_only_used_in_allowlisted_files()
    {
        var backendRoot = ResolveBackendRoot();
        var violations = new List<string>();

        foreach (
            var file in Directory.EnumerateFiles(backendRoot, "*.cs", SearchOption.AllDirectories)
        )
        {
            if (
                file.Contains(
                    $"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}",
                    StringComparison.OrdinalIgnoreCase
                )
            )
                continue;
            if (
                file.Contains(
                    $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                    StringComparison.OrdinalIgnoreCase
                )
            )
                continue;
            if (
                file.Contains(
                    $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                    StringComparison.OrdinalIgnoreCase
                )
            )
                continue;
            if (file.Contains(".Tests", StringComparison.OrdinalIgnoreCase))
                continue;

            var text = File.ReadAllText(file);
            if (!text.Contains(".IgnoreQueryFilters(", StringComparison.Ordinal))
                continue;

            var relative = Path.GetRelativePath(backendRoot, file).Replace('\\', '/');
            if (!AllowedRelativePaths.Contains(relative))
                violations.Add(relative);
        }

        violations
            .Should()
            .BeEmpty(
                "nuevos IgnoreQueryFilters deben usar IPlatformQueryAccessor; excepciones solo en la allowlist del test"
            );
    }

    private static string ResolveBackendRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (
                File.Exists(Path.Combine(dir.FullName, "ERP.sln"))
                || Directory.Exists(Path.Combine(dir.FullName, "src", "ERP.API"))
            )
                return dir.FullName;

            dir = dir.Parent;
        }

        throw new InvalidOperationException("No se encontró la raíz backend (ERP.API / ERP.sln).");
    }
}
