using System.Globalization;
using ERP.Application.Common;
using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Enums;
using ERP.Domain.Configuration.Interfaces;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Services;

/// <summary>
/// CONFIG-DYNAMIC-OPERATIONS-01: única implementación de <see cref="IOperationalPreferencesResolver"/>.
/// Company-scope únicamente (ver AllowedScopes de cada ConfigurationDefinition en
/// Definitions/Modules/{SalesPos,Cash,Purchases,Inventory,Printing,ElectronicDocuments}
/// ConfigurationDefinitions.cs).
///
/// Manejo de valor faltante/corrupto: cae a un default seguro documentado (ConfigurationDefinition.
/// DefaultValue) con warning de log — NO fail-closed, a diferencia de la regla general de la
/// arquitectura objetivo para keys RequiresAudit=true (docs/architecture/
/// configuration-engine-target-architecture.md Fase 5). Desviación deliberada y acotada a este
/// resolver: estas son preferencias operativas de bajo riesgo con default conservador
/// documentado (no bodega/certificado/fiscal), y OrgSettingsRepository.UpsertAsync ya impide que
/// se escriba un valor inválido en el flujo normal — un valor corrupto solo puede llegar aquí por
/// manipulación directa de BD, fuera del camino normal de escritura.
/// </summary>
public sealed class OperationalPreferencesResolver : IOperationalPreferencesResolver
{
    private readonly IOrgSettingsRepository _orgRepo;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;
    private readonly ILogger<OperationalPreferencesResolver> _logger;

    public OperationalPreferencesResolver(
        IOrgSettingsRepository orgRepo,
        ICurrentTenant currentTenant,
        ICurrentCompany currentCompany,
        ILogger<OperationalPreferencesResolver> logger
    )
    {
        _orgRepo = orgRepo;
        _currentTenant = currentTenant;
        _currentCompany = currentCompany;
        _logger = logger;
    }

    public Task<OperationalPreferences> ResolveAsync(CancellationToken cancellationToken = default) =>
        ResolveAsync(_currentTenant.TenantId, _currentCompany.CompanyId, cancellationToken);

    public async Task<OperationalPreferences> ResolveAsync(
        Guid tenantId,
        Guid companyId,
        CancellationToken cancellationToken = default
    )
    {
        var settings = await _orgRepo.GetAllForScopeAsync(
            tenantId,
            companyId,
            OrgScope.Company,
            companyId,
            cancellationToken
        );
        var lookup = settings.ToDictionary(s => s.Key, s => s.Value);

        bool Bool(string key, bool fallback)
        {
            if (!lookup.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
                return fallback;
            if (bool.TryParse(raw, out var parsed))
                return parsed;
            LogCorrupt(key, raw);
            return fallback;
        }

        int Int(string key, int fallback)
        {
            if (!lookup.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
                return fallback;
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                return parsed;
            LogCorrupt(key, raw);
            return fallback;
        }

        decimal Decimal(string key, decimal fallback)
        {
            if (!lookup.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
                return fallback;
            if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
                return parsed;
            LogCorrupt(key, raw);
            return fallback;
        }

        decimal? DecimalNullable(string key)
        {
            if (!lookup.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
                return null;
            if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
                return parsed;
            LogCorrupt(key, raw);
            return null;
        }

        Guid? GuidNullable(string key)
        {
            if (!lookup.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
                return null;
            if (Guid.TryParse(raw, out var parsed))
                return parsed;
            LogCorrupt(key, raw);
            return null;
        }

        string String(string key, string fallback) =>
            lookup.TryGetValue(key, out var raw) && !string.IsNullOrWhiteSpace(raw) ? raw : fallback;

        var salesPos = new SalesPosPreferences(
            RequireOpenCashSession: Bool(OrgSettingKeys.SalesPos.RequireOpenCashSession, true),
            AllowManualPrice: Bool(OrgSettingKeys.SalesPos.AllowManualPrice, false),
            AllowManualDiscount: Bool(OrgSettingKeys.SalesPos.AllowManualDiscount, true),
            MaxDiscountPercent: Decimal(OrgSettingKeys.SalesPos.MaxDiscountPercent, 0m),
            RequireCustomerAboveAmount: DecimalNullable(
                OrgSettingKeys.SalesPos.RequireCustomerAboveAmount
            ),
            AllowSellWithoutStock: Bool(OrgSettingKeys.SalesPos.AllowSellWithoutStock, false),
            AskBeforeIssue: Bool(OrgSettingKeys.SalesPos.AskBeforeIssue, false),
            DefaultPriceListId: GuidNullable(OrgSettingKeys.SalesPos.DefaultPriceListId),
            DefaultCustomerId: GuidNullable(OrgSettingKeys.SalesPos.DefaultCustomerId)
        );

        var cash = new CashPreferences(
            RequireOpeningAmount: Bool(OrgSettingKeys.Cash.RequireOpeningAmount, true),
            AllowCloseWithDifference: Bool(OrgSettingKeys.Cash.AllowCloseWithDifference, true),
            MaxAllowedDifference: Decimal(OrgSettingKeys.Cash.MaxAllowedDifference, 0m),
            RequireReasonForDifference: Bool(OrgSettingKeys.Cash.RequireReasonForDifference, true),
            AllowManualInOutMovements: Bool(OrgSettingKeys.Cash.AllowManualInOutMovements, true),
            RequireReasonForMovements: Bool(OrgSettingKeys.Cash.RequireReasonForMovements, true)
        );

        var purchases = new PurchasesPreferences(
            DefaultWarehouseId: GuidNullable(OrgSettingKeys.Purchases.DefaultWarehouseId),
            AllowConfirmWithoutReceptionXml: Bool(
                OrgSettingKeys.Purchases.AllowConfirmWithoutReceptionXml,
                true
            ),
            UpdateCostOnConfirm: Bool(OrgSettingKeys.Purchases.UpdateCostOnConfirm, true),
            AllowManualCostChange: Bool(OrgSettingKeys.Purchases.AllowManualCostChange, true),
            RequireReasonForCostChange: Bool(
                OrgSettingKeys.Purchases.RequireReasonForCostChange,
                false
            )
        );

        var inventory = new InventoryPreferences(
            AllowNegativeStock: Bool(OrgSettingKeys.Inventory.AllowNegativeStock, false),
            RequireReasonForAdjustment: Bool(
                OrgSettingKeys.Inventory.RequireReasonForAdjustment,
                true
            ),
            RequireApprovalForLargeAdjustment: Bool(
                OrgSettingKeys.Inventory.RequireApprovalForLargeAdjustment,
                false
            ),
            LargeAdjustmentThresholdAmount: Decimal(
                OrgSettingKeys.Inventory.LargeAdjustmentThresholdAmount,
                0m
            )
        );

        var printing = new PrintingPreferences(
            SalesReceiptMode: String(OrgSettingKeys.Printing.SalesReceiptMode, "AskBeforePrint"),
            SalesReceiptCopies: Int(OrgSettingKeys.Printing.SalesReceiptCopies, 1),
            SalesReceiptPaperWidth: String(OrgSettingKeys.Printing.SalesReceiptPaperWidth, "80mm"),
            SalesReceiptIncludeLogo: Bool(OrgSettingKeys.Printing.SalesReceiptIncludeLogo, false),
            SalesReceiptIncludeAccessKey: Bool(
                OrgSettingKeys.Printing.SalesReceiptIncludeAccessKey,
                true
            ),
            SalesReceiptIncludeCashier: Bool(
                OrgSettingKeys.Printing.SalesReceiptIncludeCashier,
                true
            ),
            SalesReceiptOpenCashDrawer: Bool(
                OrgSettingKeys.Printing.SalesReceiptOpenCashDrawer,
                false
            )
        );

        var electronicDocuments = new ElectronicDocumentsPreferences(
            AutoRetryEnabled: Bool(OrgSettingKeys.ElectronicDocuments.AutoRetryEnabled, true),
            MaxRetryAttempts: Int(OrgSettingKeys.ElectronicDocuments.MaxRetryAttempts, 3),
            GenerateRideOnAuthorization: Bool(
                OrgSettingKeys.ElectronicDocuments.GenerateRideOnAuthorization,
                true
            ),
            EmailOnAuthorization: Bool(
                OrgSettingKeys.ElectronicDocuments.EmailOnAuthorization,
                true
            )
        );

        var notifications = new NotificationsPreferences(
            SalesInvoiceAuthorizedEnabled: Bool(
                OrgSettingKeys.Communications.SalesInvoiceAuthorizedEnabled,
                true
            ),
            SendCopyToCompanyEmail: Bool(
                OrgSettingKeys.Communications.SendCopyToCompanyEmail,
                false
            ),
            DefaultLanguage: String(OrgSettingKeys.Communications.DefaultLanguage, "es")
        );

        return new OperationalPreferences(
            salesPos,
            cash,
            purchases,
            inventory,
            printing,
            electronicDocuments,
            notifications
        );
    }

    private void LogCorrupt(string key, string rawValue) =>
        _logger.LogWarning(
            "OperationalPreferences: valor no parseable para la key '{Key}' (valor crudo: '{RawValue}') — se usó el default seguro documentado.",
            key,
            rawValue
        );
}
