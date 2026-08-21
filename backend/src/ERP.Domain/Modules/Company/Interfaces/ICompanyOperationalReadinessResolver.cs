namespace ERP.Domain.Modules.Company.Interfaces;

/// <summary>
/// COMPANY-OPERATING-SETUP-01: estado calculado de un ítem revisado del checklist de preparación
/// operativa. No es una fuente de configuración — es un resultado de solo lectura recalculado en
/// cada consulta a partir de las fuentes de verdad existentes (Company, Branch, Establishment,
/// EmissionPoint, Warehouse, CashRegister, PriceList, SriSettings, y los resolvers tipados ya
/// existentes: IInvoiceDefaultsResolver, ISalesFiscalPolicyResolver, ICompanyBrandingResolver,
/// ICatalogConfigurationResolver). Nunca se persiste.
/// </summary>
public enum ReadinessStatus
{
    Ready,
    Warning,
    Missing,
    NotApplicable,
}

/// <summary>
/// Info: solo informativo, nunca afecta ninguna bandera CanX. Warning: recomendable, no bloquea
/// operación. Blocking: si el ítem no está Ready, la bandera CanX de su <see cref="ReadinessBlockingArea"/>
/// (si tiene una) queda en false.
/// </summary>
public enum ReadinessSeverity
{
    Info,
    Warning,
    Blocking,
}

/// <summary>Área operativa que un ítem Blocking condiciona. Null en ítems sin efecto en ninguna bandera CanX.</summary>
public enum ReadinessBlockingArea
{
    Sales,
    ElectronicInvoicing,
    Inventory,
    CashRegister,
}

/// <summary>
/// Pantalla owner real donde se corrige un ítem — identificador semántico, NUNCA una ruta de SPA.
/// El backend no conoce el router de React; el frontend traduce cada valor a su ruta concreta
/// (ver <c>operationalReadinessActionRoutes.ts</c>). Ningún otro DTO de este backend expone rutas
/// de frontend — este enum es el patrón correcto, no una excepción.
/// </summary>
public enum ReadinessActionTarget
{
    CompanyProfile,
    CompanyBranding,
    CompanyFiscalSettings,
    CompanySalesSettings,
    DecimalSettings,
    Branches,
    Establishments,
    EmissionPoints,
    Warehouses,
    CashRegisters,
    PriceLists,
    ElectronicInvoicingSettings,
    Items,
}

/// <summary>
/// Un ítem revisado. Deliberadamente NO transporta texto — <c>Code</c> es el identificador estable
/// que el frontend usa para resolver label/mensaje/porqué-importa vía i18n
/// (<c>operationalReadiness.items.{Code}.*</c>). <c>ActionTarget</c> dice QUÉ pantalla corrige el
/// ítem, nunca CÓMO llegar ahí. El backend calcula el estado; el frontend decide cómo presentarlo
/// y navegar — nunca al revés (regla explícita de COMPANY-OPERATING-SETUP-01).
/// </summary>
public sealed record ReadinessItem(
    string Code,
    ReadinessStatus Status,
    ReadinessSeverity Severity,
    ReadinessBlockingArea? BlockingArea,
    ReadinessActionTarget? ActionTarget
);

public sealed record ReadinessSection(
    string Code,
    ReadinessStatus Status,
    IReadOnlyList<ReadinessItem> Items
);

/// <summary>
/// Resultado agregado de preparación operativa de una empresa. CanSell/CanIssueElectronicInvoices/
/// CanUseInventory quedan en false si algún ítem Blocking de su área respectiva no está Ready.
/// CanUseCashRegister es una excepción deliberada: no existe hoy una regla de dominio que exija
/// caja para poder vender (ver doc-comment de <see cref="ICompanyOperationalReadinessResolver"/>),
/// así que se calcula directamente de la existencia de al menos una caja activa, sin depender de
/// ningún ítem Blocking.
/// </summary>
public sealed record CompanyOperationalReadinessResult(
    ReadinessStatus OverallStatus,
    bool CanSell,
    bool CanIssueElectronicInvoices,
    bool CanUseInventory,
    bool CanUseCashRegister,
    IReadOnlyList<ReadinessSection> Sections
);

/// <summary>
/// Único punto de cálculo del checklist de preparación operativa de una empresa. Orquesta fuentes
/// de verdad y resolvers ya existentes — no lee org_settings directamente, no persiste estado, no
/// introduce una fuente de configuración nueva. Ver docs/architecture/ (COMPANY-OPERATING-SETUP-01)
/// para el detalle de qué revisa cada sección.
///
/// Decisión explícita registrada aquí (no inventada en frontend): no existe en el dominio actual
/// una regla que condicione la venta a tener una caja (CashRegister) configurada/abierta — el flujo
/// de venta manual (SalesPage) es independiente del módulo de Caja. Por eso CashRegister nunca es
/// Severity.Blocking para el área Sales; su propia bandera CanUseCashRegister se calcula aparte.
/// </summary>
public interface ICompanyOperationalReadinessResolver
{
    Task<CompanyOperationalReadinessResult> GetAsync(
        Guid tenantId,
        Guid companyId,
        CancellationToken cancellationToken = default
    );
}
