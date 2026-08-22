namespace ERP.Application.Modules.Settings.Operations.DTOs;

public sealed record OperationalPreferencesDto(
    SalesPosPreferencesDto SalesPos,
    CashPreferencesDto Cash,
    PurchasesPreferencesDto Purchases,
    InventoryPreferencesDto Inventory,
    PrintingPreferencesDto Printing,
    ElectronicDocumentsPreferencesDto ElectronicDocuments,
    NotificationsPreferencesDto Notifications
);

public sealed record SalesPosPreferencesDto(
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

public sealed record CashPreferencesDto(
    bool RequireOpeningAmount,
    bool AllowCloseWithDifference,
    decimal MaxAllowedDifference,
    bool RequireReasonForDifference,
    bool AllowManualInOutMovements,
    bool RequireReasonForMovements
);

public sealed record PurchasesPreferencesDto(
    Guid? DefaultWarehouseId,
    bool AllowConfirmWithoutReceptionXml,
    bool UpdateCostOnConfirm,
    bool AllowManualCostChange,
    bool RequireReasonForCostChange
);

public sealed record InventoryPreferencesDto(
    bool AllowNegativeStock,
    bool RequireReasonForAdjustment,
    bool RequireApprovalForLargeAdjustment,
    decimal LargeAdjustmentThresholdAmount
);

public sealed record PrintingPreferencesDto(
    string SalesReceiptMode,
    int SalesReceiptCopies,
    string SalesReceiptPaperWidth,
    bool SalesReceiptIncludeLogo,
    bool SalesReceiptIncludeAccessKey,
    bool SalesReceiptIncludeCashier,
    bool SalesReceiptOpenCashDrawer
);

public sealed record ElectronicDocumentsPreferencesDto(
    bool AutoRetryEnabled,
    int MaxRetryAttempts,
    bool GenerateRideOnAuthorization,
    bool EmailOnAuthorization
);

public sealed record NotificationsPreferencesDto(
    bool SalesInvoiceAuthorizedEnabled,
    bool SendCopyToCompanyEmail,
    string DefaultLanguage
);
