using ERP.Domain.Modules.Company.Enums;

namespace ERP.Domain.Modules.Company.Interfaces;

/// <summary>
/// COMPANY-OPERATING-SETUP-01: estado calculado de un ítem revisado del checklist de preparación
/// operativa. No es una fuente de configuración — es un resultado de solo lectura recalculado en
/// cada consulta a partir de las fuentes de verdad y validadores propietarios existentes (matriz
/// completa en docs/architecture/company-readiness.md). Nunca se persiste.
/// Ready: cumplido. Missing: incumplido y Blocking. Warning: incumplido pero recomendable, o
/// cumplido con advertencia (p. ej. certificado próximo a vencer). NotApplicable: la capacidad que
/// lo exige no está activa (p. ej. emisión electrónica con punto de emisión físico).
/// </summary>
public enum ReadinessStatus
{
    Ready,
    Warning,
    Missing,
    NotApplicable,
}

/// <summary>
/// Info: solo informativo. Warning: recomendable — incumplido produce Status Warning y nunca vuelve
/// Missing el estado global. Blocking: incumplido produce Status Missing y vuelve Missing el estado
/// global. Independiente de la severidad, cada bandera CanX depende solo de los ítems que listan su
/// área en <see cref="ReadinessItem.BlockingAreas"/>.
/// </summary>
public enum ReadinessSeverity
{
    Info,
    Warning,
    Blocking,
}

/// <summary>Capacidad operativa (bandera CanX) que exige un ítem.</summary>
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
    AccountingSettings,
}

/// <summary>
/// Un ítem revisado. Deliberadamente NO transporta texto — <c>Code</c> es el identificador estable
/// que el frontend usa para resolver label/mensaje/porqué-importa vía i18n
/// (<c>operationalReadiness.items.{Code}.*</c>). <c>ActionTarget</c> dice QUÉ pantalla corrige el
/// ítem, nunca CÓMO llegar ahí. El backend calcula el estado; el frontend decide cómo presentarlo
/// y navegar — nunca al revés (regla explícita de COMPANY-OPERATING-SETUP-01).
/// <c>BlockingAreas</c>: capacidades que no están disponibles mientras el ítem esté Missing o
/// Warning (vacío si no condiciona ninguna). Un mismo dato aparece en un único ítem aunque afecte
/// varias capacidades.
/// </summary>
public sealed record ReadinessItem(
    string Code,
    ReadinessStatus Status,
    ReadinessSeverity Severity,
    IReadOnlyList<ReadinessBlockingArea> BlockingAreas,
    ReadinessActionTarget? ActionTarget
);

public sealed record ReadinessSection(
    string Code,
    ReadinessStatus Status,
    IReadOnlyList<ReadinessItem> Items
);

/// <summary>
/// Validación fiscal de la emisión de una caja, separada de su preparación operativa.
/// Verified: el validador propietario no reporta requisitos obligatorios (punto electrónico).
/// Pending: el validador reporta requisitos obligatorios o no pudo verificar.
/// NotVerified: no existe política fiscal propietaria que verificar (punto físico — brechas F-1..F-5
/// de docs/architecture/company-readiness.md); nunca se presenta como autorizado.
/// NotApplicable: la caja no tiene un punto de emisión activo.
/// </summary>
public enum ReadinessFiscalStatus
{
    Verified,
    Pending,
    NotVerified,
    NotApplicable,
}

/// <summary>
/// Diagnóstico de una caja activa de la sucursal: su punto de emisión, modalidad y los requisitos
/// que incumple. <c>RequirementCodes</c> referencia ítems de <see cref="ReadinessSection.Items"/>
/// por <see cref="ReadinessItem.Code"/> — el requisito se describe una sola vez y aquí solo se
/// indica en qué caja aplica. <c>OperationalStatus</c>: Ready si la caja puede vender con su
/// punto (las precondiciones que exigen OpenCashSession/CreateSalesDraft/AuthorizeSales), Missing
/// si no.
/// </summary>
public sealed record ReadinessCashRegister(
    Guid CashRegisterId,
    string Code,
    string Name,
    Guid? EmissionPointId,
    string? EstablishmentCode,
    string? EmissionPointCode,
    EmissionType? EmissionType,
    ReadinessStatus OperationalStatus,
    ReadinessFiscalStatus FiscalStatus,
    IReadOnlyList<string> RequirementCodes
);

/// <summary>
/// Resultado agregado de preparación operativa de la empresa en la sucursal activa
/// (<see cref="BranchId"/>): los requisitos de empresa se evalúan una vez y los de caja/bodega solo
/// con las de esa sucursal. Cada bandera CanX es false si algún ítem que lista su área en
/// <see cref="ReadinessItem.BlockingAreas"/> está Missing o Warning; CanIssueElectronicInvoices
/// además exige que alguna caja de la sucursal tenga un punto electrónico. OverallStatus: Missing si
/// hay algún ítem Missing; Warning si hay alguno Warning; si no, Ready (los NotApplicable no cuentan).
/// </summary>
public sealed record CompanyOperationalReadinessResult(
    Guid BranchId,
    ReadinessStatus OverallStatus,
    bool CanSell,
    bool CanIssueElectronicInvoices,
    bool CanUseInventory,
    bool CanUseCashRegister,
    IReadOnlyList<ReadinessSection> Sections,
    IReadOnlyList<ReadinessCashRegister> CashRegisters
);

/// <summary>
/// Único punto de cálculo del checklist de preparación operativa de una empresa. Orquesta fuentes
/// de verdad, resolvers y validadores propietarios ya existentes — no lee org_settings
/// directamente, no persiste estado, no introduce una fuente de configuración nueva. Matriz
/// dato → propietario → validador → capacidades: docs/architecture/company-readiness.md.
///
/// Cada capacidad refleja las precondiciones de su operación propietaria, nunca reglas propias: la
/// venta exige una caja abierta de la sucursal activa con punto de emisión
/// (CreateSalesDraft/OpenCashSession) y usa el punto de esa caja, no el punto por defecto de la
/// empresa. <paramref name="branchId"/> es la sucursal activa ya validada por IBranchAccessGuard.
/// </summary>
public interface ICompanyOperationalReadinessResolver
{
    Task<CompanyOperationalReadinessResult> GetAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        CancellationToken cancellationToken = default
    );
}
