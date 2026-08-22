using ERP.Application.Common;
using ERP.Application.Modules.Settings.Operations.DTOs;
using MediatR;

namespace ERP.Application.Modules.Settings.Operations.UseCases.UpdateOperationalPreferences;

/// <summary>
/// Cada grupo es opcional: el frontend guarda una sección del hub a la vez (mismo patrón que
/// CompanySettingsHubPage), así que solo el grupo presente en el request se escribe — los demás
/// quedan intactos. Dentro de un grupo presente, todos sus campos se envían siempre (igual que
/// UpdateCompanyEmailSettingsCommand).
/// </summary>
public sealed record UpdateOperationalPreferencesCommand(
    SalesPosPreferencesInput? SalesPos,
    CashPreferencesInput? Cash,
    PurchasesPreferencesInput? Purchases,
    InventoryPreferencesInput? Inventory,
    PrintingPreferencesInput? Printing,
    ElectronicDocumentsPreferencesInput? ElectronicDocuments,
    NotificationsPreferencesInput? Notifications
) : IRequest<Result<OperationalPreferencesDto>>;

public sealed record SalesPosPreferencesInput(
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

public sealed record CashPreferencesInput(
    bool RequireOpeningAmount,
    bool AllowCloseWithDifference,
    decimal MaxAllowedDifference,
    bool RequireReasonForDifference,
    bool AllowManualInOutMovements,
    bool RequireReasonForMovements
);

public sealed record PurchasesPreferencesInput(
    Guid? DefaultWarehouseId,
    bool AllowConfirmWithoutReceptionXml,
    bool UpdateCostOnConfirm,
    bool AllowManualCostChange,
    bool RequireReasonForCostChange
);

public sealed record InventoryPreferencesInput(
    bool AllowNegativeStock,
    bool RequireReasonForAdjustment,
    bool RequireApprovalForLargeAdjustment,
    decimal LargeAdjustmentThresholdAmount
);

public sealed record PrintingPreferencesInput(
    string SalesReceiptMode,
    int SalesReceiptCopies,
    string SalesReceiptPaperWidth,
    bool SalesReceiptIncludeLogo,
    bool SalesReceiptIncludeAccessKey,
    bool SalesReceiptIncludeCashier,
    bool SalesReceiptOpenCashDrawer
);

public sealed record ElectronicDocumentsPreferencesInput(
    bool AutoRetryEnabled,
    int MaxRetryAttempts,
    bool GenerateRideOnAuthorization,
    bool EmailOnAuthorization
);

public sealed record NotificationsPreferencesInput(
    bool SalesInvoiceAuthorizedEnabled,
    bool SendCopyToCompanyEmail,
    string DefaultLanguage
);
