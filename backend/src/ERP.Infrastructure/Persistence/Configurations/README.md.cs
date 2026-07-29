namespace ERP.Infrastructure.Persistence.Configurations;

/// <summary>
/// Convención de organización de configuraciones de EF Core por módulo.
///
/// Estructura esperada:
/// ```
/// Persistence/
/// └── Configurations/
///     ├── Authentication/
///     │   ├── UserConfiguration.cs
///     │   └── IdentityUserConfiguration.cs
///     ├── Products/
///     │   ├── ProductConfiguration.cs
///     │   ├── ProductCategoryConfiguration.cs
///     │   └── ...
///     ├── SaaS/
///     │   ├── CommercialPlanConfiguration.cs
///     │   └── ...
///     ├── Accounting/
///     │   ├── AccountConfiguration.cs
///     │   └── ...
///     └── Common/
///         ├── SubscriberConfiguration.cs
///         ├── BranchConfiguration.cs
///         └── ...
/// ```
///
/// Normas:
/// 1. Cada entidad tiene su propio archivo EntityConfiguration.cs
/// 2. Implementa IEntityTypeConfiguration<TEntity>
/// 3. ErpDbContext.OnModelCreating() llama ApplyConfigurationsFromAssembly()
///    que automáticamente descubre y aplica TODAS las configuraciones
/// 4. Al agregar una nueva entidad:
///    - Crear la entidad en Domain/[ModuleName]/Entities/
///    - Crear EntityConfiguration en Persistence/Configurations/[ModuleName]/
///    - NO necesita registro manual en DbContext (automático)
/// 5. Aprovecha multi-tenant:
///    - Si ITenantScopedEntity, el filtro global en OnModelCreating() la protege
///    - Si NOT, documentar por qué (ej: Subscriber, Config global, Geografia)
/// </summary>
public abstract class ConfigurationsModularConventions { }
