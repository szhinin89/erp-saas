using Microsoft.EntityFrameworkCore;
using ERP.Domain.Accounting.Entities;
using ERP.Domain.Common;
using ERP.Domain.Products.Entities;
using ERP.Domain.Auth.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Domain.Security.Entities;
using ERP.Domain.Access.Entities;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Geography.Entities;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Customers.Entities;
using ERP.Domain.Subscriptions.Entities;
using ERP.Domain.Navigation.Entities;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Bodegas.Entities;
using ERP.Domain.Proveedores.Entities;
using ERP.Domain.Compras.Entities;
using ERP.Domain.Gastos.Entities;
using ERP.Domain.Inventario.Entities;
using ERP.Application.Common;
using System.Linq.Expressions;

namespace ERP.Infrastructure.Persistence;

/// <summary>
/// DbContext centralizado para todas las entidades (31 DbSets).
/// 
/// ⚠️ RIESGO ARQUITECTÓNICO: Este contexto está concentrando demasiados módulos.
/// 
/// ESTADO ACTUAL (Permitido para monolito modular):
/// - Contabilidad: Account, JournalEntry, JournalEntryLine
/// - Productos: Product, ProductLine, ProductCategory, ProductSubcategory, Brand, ProductType, TaxRate, UnitOfMeasure, Tariff
/// - Autenticación: User, IdentityUser, Membership
/// - Tenants: Tenant
/// - Seguridad: AccessProfile, AccessProfilePermission, SecurityAdminScopeAssignment
/// - Geografía: GeoCountry, GeoProvince, GeoCanton, GeoParish
/// - Auditoría: UserActivity
/// - Ventas: Customer
/// - SaaS: SaasFeatureDefinition, SaasPlan, SaasPlanFeature, TenantSaasSubscription, TenantSubscriptionFeatureOverride, TenantSubscriptionUsage
/// - UI/Config: UiNavGroup, UiNavItem, ConfigGlobal, ConfigModule, ConfigFeature
/// - Sucursales: Branch
/// 
/// PRÓXIMOS PASOS RECOMENDADOS:
/// 1. Separar configuraciones de EF Core por módulo (ver Configurations/ folder)
///    - Usar IEntityTypeConfiguration<T> en carpetas específicas
///    - Mantener ApplyConfigurationsFromAssembly() automatizado
/// 2. Evaluar separación a múltiples DbContext por módulo mayor
///    - No necesariamente crear DbContext separados HOY
///    - Pero sí preparar el código para una futura separación
/// 3. Usar convenciones estrictas en repositorios
///    - Cada módulo: IXyzRepository interfaz + XyzRepository implementación
///    - Todos los repositorios inyectan solo ErpDbContext
/// 4. Considerar Command/Query segregation (CQRS) para reportes
/// 
/// MULTI-TENANCY:
/// - Filtro automático por tenant en QueryFilter para todas las ITenantEntity
/// - CurrentTenantId retorna Guid.Empty si no autenticado (seguro por defecto)
/// - Todas las queries se filtran automáticamente, no se olvidan excepciones
/// </summary>
public class ErpDbContext : DbContext
{
    private readonly ICurrentTenant _currentTenant;

    public ErpDbContext(
        DbContextOptions<ErpDbContext> options,
        ICurrentTenant currentTenant) : base(options)
    {
        _currentTenant = currentTenant;
    }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalEntryLine> JournalEntryLines => Set<JournalEntryLine>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<TaxRate> TaxRates => Set<TaxRate>();
    public DbSet<ProductLine> ProductLines => Set<ProductLine>();
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<ProductSubcategory> ProductSubcategories => Set<ProductSubcategory>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<ProductType> ProductTypes => Set<ProductType>();
    public DbSet<UnitOfMeasure> UnitsOfMeasure => Set<UnitOfMeasure>();
    public DbSet<Tariff> Tariffs => Set<Tariff>();
    public DbSet<User> Users => Set<User>();
    public DbSet<IdentityUser> IdentityUsers => Set<IdentityUser>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<AccessProfile> AccessProfiles => Set<AccessProfile>();
    public DbSet<AccessProfilePermission> AccessProfilePermissions => Set<AccessProfilePermission>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<SecurityAdminScopeAssignment> SecurityAdminScopeAssignments => Set<SecurityAdminScopeAssignment>();
    public DbSet<GeoCountry> GeoCountries => Set<GeoCountry>();
    public DbSet<GeoProvince> GeoProvinces => Set<GeoProvince>();
    public DbSet<GeoCanton> GeoCantons => Set<GeoCanton>();
    public DbSet<GeoParish> GeoParishes => Set<GeoParish>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<UserActivity> UserActivities => Set<UserActivity>();
    public DbSet<Customer> Customers => Set<Customer>();

    // ── Logística / Bodegas ───────────────────────────────────────────────
    public DbSet<Bodega> Bodegas => Set<Bodega>();

    // ── Proveedores ───────────────────────────────────────────────────────
    public DbSet<Proveedor> Proveedores => Set<Proveedor>();

    // ── Compras ───────────────────────────────────────────────────────────
    public DbSet<CompraFactura>          CompraFacturas          => Set<CompraFactura>();
    public DbSet<CompraDetalle>          CompraDetalles          => Set<CompraDetalle>();
    public DbSet<CompraBodegaAsignacion> CompraBodegaAsignaciones => Set<CompraBodegaAsignacion>();

    // ── Gastos ────────────────────────────────────────────────────────────
    public DbSet<GastoFactura> GastoFacturas => Set<GastoFactura>();

    // ── Inventario ────────────────────────────────────────────────────────
    public DbSet<StockActual>          StockActual            => Set<StockActual>();
    public DbSet<InventarioMovimiento> InventarioMovimientos  => Set<InventarioMovimiento>();

    public DbSet<SaasFeatureDefinition> SaasFeatureDefinitions => Set<SaasFeatureDefinition>();
    public DbSet<SaasPlan> SaasPlans => Set<SaasPlan>();
    public DbSet<SaasPlanFeature> SaasPlanFeatures => Set<SaasPlanFeature>();
    public DbSet<TenantSaasSubscription> TenantSaasSubscriptions => Set<TenantSaasSubscription>();
    public DbSet<TenantSubscriptionFeatureOverride> TenantSubscriptionFeatureOverrides => Set<TenantSubscriptionFeatureOverride>();
    public DbSet<TenantSubscriptionUsage> TenantSubscriptionUsages => Set<TenantSubscriptionUsage>();

    public DbSet<UiNavGroup> UiNavGroups => Set<UiNavGroup>();
    public DbSet<UiNavItem> UiNavItems => Set<UiNavItem>();
    public DbSet<ConfigGlobal> ConfigGlobals => Set<ConfigGlobal>();
    public DbSet<ConfigModule> ConfigModules => Set<ConfigModule>();
    public DbSet<ConfigFeature> ConfigFeatures => Set<ConfigFeature>();

    /// <summary>
    /// Evaluada en cada query, no al compilar el modelo.
    /// Si el request no está autenticado, retorna Guid.Empty para que el filtro
    /// global no retorne filas (ya que TenantId nunca debe ser Guid.Empty en
    /// entidades multi-tenant). Esto permite endpoints anónimos como login/reset.
    /// </summary>
    private Guid CurrentTenantId
    {
        get
        {
            return _currentTenant.TenantId;
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ErpDbContext).Assembly);

        // Filtro global multi-tenant automático:
        // - Aplica a toda entidad NO-OWNED que implemente ITenantEntity.
        // - Evita que al agregar una nueva entidad se nos olvide registrar el filtro.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.IsOwned())
                continue;

            var clrType = entityType.ClrType;
            if (clrType == typeof(Tenant))
                continue;
            if (!typeof(ITenantEntity).IsAssignableFrom(clrType))
                continue;

            var parameter = Expression.Parameter(clrType, "e");
            var tenantProperty = Expression.Property(parameter, nameof(ITenantEntity.TenantId));
            var currentTenant = Expression.Property(Expression.Constant(this), nameof(CurrentTenantId));
            var body = Expression.Equal(tenantProperty, currentTenant);
            var lambda = Expression.Lambda(body, parameter);

            modelBuilder.Entity(clrType).HasQueryFilter(lambda);
        }

        base.OnModelCreating(modelBuilder);
    }
}
