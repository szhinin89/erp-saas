namespace ERP.Domain.Configuration.Interfaces;

/// <summary>
/// CONFIG-DYNAMIC-OPERATIONS-01: resolver tipado de las preferencias operativas de
/// /settings/operations (Sales/POS, Cash, Purchases, Inventory, Printing, ElectronicDocuments,
/// Notifications). Solo scope Company está implementado — Branch/CashRegister quedan
/// documentados como extensión futura (ver ConfigurationDefinition.AllowedScopes de cada key,
/// hoy limitado a [Company]), no como parámetros sin uso real.
/// </summary>
public interface IOperationalPreferencesResolver
{
    /// <summary>Resuelve para el tenant/company del contexto autenticado actual (ICurrentTenant/ICurrentCompany) — uso normal desde handlers de request HTTP.</summary>
    Task<OperationalPreferences> ResolveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Resuelve para un tenant/company explícitos — uso desde event handlers/jobs en background
    /// donde ICurrentTenant/ICurrentCompany del request NO reflejan de forma confiable la empresa
    /// dueña del documento que disparó el evento (ej. SalesInvoiceAuthorizedCommunicationHandler,
    /// que ya recibe TenantId/CompanyId explícitos del documento, no del contexto ambiente).
    /// </summary>
    Task<OperationalPreferences> ResolveAsync(
        Guid tenantId,
        Guid companyId,
        CancellationToken cancellationToken = default
    );
}

public sealed record OperationalPreferences(
    SalesPosPreferences SalesPos,
    CashPreferences Cash,
    PurchasesPreferences Purchases,
    InventoryPreferences Inventory,
    PrintingPreferences Printing,
    ElectronicDocumentsPreferences ElectronicDocuments,
    NotificationsPreferences Notifications
);

public sealed record SalesPosPreferences(
    bool RequireOpenCashSession,
    bool AllowManualPrice,
    bool AllowManualDiscount,
    decimal MaxDiscountPercent,
    decimal? RequireCustomerAboveAmount,
    bool AllowSellWithoutStock,
    bool AskBeforeIssue,
    Guid? DefaultPriceListId,
    Guid? DefaultCustomerId
);

public sealed record CashPreferences(
    bool RequireOpeningAmount,
    bool AllowCloseWithDifference,
    decimal MaxAllowedDifference,
    bool RequireReasonForDifference,
    bool AllowManualInOutMovements,
    bool RequireReasonForMovements
);

public sealed record PurchasesPreferences(
    Guid? DefaultWarehouseId,
    bool AllowConfirmWithoutReceptionXml,
    bool UpdateCostOnConfirm,
    bool AllowManualCostChange,
    bool RequireReasonForCostChange
);

public sealed record InventoryPreferences(
    bool AllowNegativeStock,
    bool RequireReasonForAdjustment,
    bool RequireApprovalForLargeAdjustment,
    decimal LargeAdjustmentThresholdAmount
);

/// <summary>
/// <see cref="SalesReceiptPaperWidth"/> se resuelve y se expone (para que la pantalla lo
/// muestre/guarde) pero deliberadamente ningún consumidor lo aplica — ver doc comment en
/// <c>OrgSettingKeys.Printing</c>.
/// </summary>
public sealed record PrintingPreferences(
    string SalesReceiptMode,
    int SalesReceiptCopies,
    string SalesReceiptPaperWidth,
    bool SalesReceiptIncludeLogo,
    bool SalesReceiptIncludeAccessKey,
    bool SalesReceiptIncludeCashier,
    bool SalesReceiptOpenCashDrawer
);

public sealed record ElectronicDocumentsPreferences(
    bool AutoRetryEnabled,
    int MaxRetryAttempts,
    bool GenerateRideOnAuthorization,
    bool EmailOnAuthorization
);

/// <summary>DefaultLanguage reutiliza OrgSettingKeys.Communications.DefaultLanguage (no una key nueva) — ver plan CONFIG-DYNAMIC-OPERATIONS-01.</summary>
public sealed record NotificationsPreferences(
    bool SalesInvoiceAuthorizedEnabled,
    bool SendCopyToCompanyEmail,
    string DefaultLanguage
);
