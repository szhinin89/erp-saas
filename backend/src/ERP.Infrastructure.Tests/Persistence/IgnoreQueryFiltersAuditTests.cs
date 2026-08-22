using FluentAssertions;

namespace ERP.Infrastructure.Tests.Persistence;

/// <summary>
/// Evita nuevos usos directos de <c>IgnoreQueryFilters()</c> fuera del wrapper y excepciones documentadas.
/// </summary>
public sealed class IgnoreQueryFiltersAuditTests
{
    private static readonly HashSet<string> AllowedRelativePaths = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        "src/ERP.Infrastructure/Persistence/PlatformQueryAccessor.cs",
        "src/ERP.Infrastructure/MasterData/Reconciliation/BusinessPartnerReconciliationService.cs", // platform-level reconciliation
        "src/ERP.Infrastructure/Persistence/Repositories/AccessRepository.cs", // access checks need cross-tenant visibility
        "src/ERP.Infrastructure/Persistence/Repositories/BranchRepository.cs", // login flow: GetAsync/GetByIdAsync se invocan antes de que exista ICurrentTenant/ICurrentCompany ambiente confiable, misma razón que AccessRepository/CompanyUserBranchRepository
        "src/ERP.Infrastructure/Persistence/Repositories/UserSessionRepository.cs", // login flow: se invoca antes de que exista ICurrentCompany ambiente confiable, misma razón que AccessRepository
        "src/ERP.Infrastructure/Persistence/Repositories/CompanyUserBranchRepository.cs", // idem — selección de sucursal ocurre durante el propio login
        "src/ERP.Infrastructure/Persistence/Repositories/CompanyRepository.cs", // company bootstrap needs cross-tenant
        "src/ERP.Infrastructure/Persistence/Repositories/EmissionPointRepository.cs",
        "src/ERP.Infrastructure/Persistence/Repositories/EstablishmentRepository.cs",
        "src/ERP.Infrastructure/Persistence/Repositories/ProductCatalogRepository.cs",
        "src/ERP.Infrastructure/Persistence/Repositories/TenantRepository.cs", // tenant lookup is cross-tenant by nature
        "src/ERP.Infrastructure/Persistence/Repositories/DocumentSequenceRepository.cs", // advisory lock + transaction propia: bypass intencional para find-or-create atómico
        "src/ERP.Infrastructure/Seeding/Steps/OrganizationBootstrapStep.cs", // bootstrap needs cross-tenant visibility
        "src/ERP.Infrastructure/Seeding/Steps/ElectronicDocumentsBootstrapStep.cs", // idem
        "src/ERP.Infrastructure/Seeding/Steps/InventoryBootstrapStep.cs", // idem
        "src/ERP.Infrastructure/Seeding/Steps/SalesBootstrapStep.cs", // idem
        "src/ERP.Infrastructure/Seeding/DefaultProfileSeeder.cs", // seeder needs cross-tenant visibility
        "src/ERP.Infrastructure/Persistence/Repositories/Caja/CashRegisterRepository.cs", // uniqueness check por código explícito con tenantId/branchId, incluye deshabilitados
        "src/ERP.Infrastructure/Persistence/Repositories/Caja/CashSessionRepository.cs", // filtros explícitos por tenantId; existencia/lookup independiente del query filter ambiental
        "src/ERP.Infrastructure/Seeding/Steps/CajaBootstrapStep.cs", // bootstrap needs cross-tenant visibility
        "src/ERP.Infrastructure/Persistence/Repositories/CompanyUserPreferencesRepository.cs", // login flow: se resuelve antes de ICurrentCompany ambiente confiable
        "src/ERP.Infrastructure/Persistence/Repositories/Configuration/OrgSettingsRepository.cs", // filtros explícitos por tenantId/companyId; jerarquía de scope no depende del query filter ambiental
        "src/ERP.Infrastructure/Persistence/Repositories/Inventory/StockAdjustmentRepository.cs", // filtro explícito por tenantId; secuencial debe considerar registros deshabilitados, no depende del query filter ambiental
        "src/ERP.Infrastructure/Persistence/Repositories/Inventory/InventoryAdjustmentReasonRepository.cs", // INVENTORY-ADJUSTMENTS-02: uniqueness check de Code por tenant debe considerar motivos deshabilitados; filtro explícito TenantId reaplicado
        "src/ERP.Infrastructure/Seeding/E2E/E2ESeedService.cs", // provisioning E2E fuera de Production, bajo bandera explícita: mismo motivo que los *BootstrapStep (bootstrap needs cross-tenant visibility)
        "src/ERP.API/Health/MembershipConsistencyHealthCheck.cs", // health check sin contexto de tenant: mismo motivo que BusinessPartnerReconciliationService (chequeo de integridad cross-tenant de solo lectura)
        "src/ERP.Infrastructure/Seeding/MasterDataClassificationSeeder.cs", // CLASS-BP-CATALOGS-01: reutilizado por bootstrap step (request-scoped) y backfill (multi-tenant, sin contexto HTTP ambiente); filtro explícito TenantId+CompanyId, fail-closed
        "src/ERP.Infrastructure/Seeding/MasterDataClassificationBackfillService.cs", // CLASS-BP-CATALOGS-01: itera todas las (TenantId, CompanyId) existentes para el backfill de empresas ya creadas — mismo motivo que los *BootstrapStep
        "src/ERP.Infrastructure/Persistence/Repositories/MasterData/ClassificationCatalogRepositoryBase.cs", // CLASS-BP-CATALOGS-01: base compartida de los 12 repos de catálogo, consumida también desde el bootstrap step/backfill sin ICurrentTenant/ICurrentCompany ambiente; filtro explícito TenantId+CompanyId, fail-closed
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
