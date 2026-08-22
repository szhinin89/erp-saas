using System.Globalization;
using ERP.Application.Common;
using ERP.Application.Modules.Settings.Operations.DTOs;
using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Enums;
using ERP.Domain.Configuration.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Settings.Operations.UseCases.UpdateOperationalPreferences;

public sealed class UpdateOperationalPreferencesCommandHandler
    : IRequestHandler<UpdateOperationalPreferencesCommand, Result<OperationalPreferencesDto>>
{
    private readonly IOrgSettingsRepository _repo;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;
    private readonly ICurrentUser _currentUser;
    private readonly IOperationalPreferencesResolver _resolver;

    public UpdateOperationalPreferencesCommandHandler(
        IOrgSettingsRepository repo,
        ICurrentTenant currentTenant,
        ICurrentCompany currentCompany,
        ICurrentUser currentUser,
        IOperationalPreferencesResolver resolver
    )
    {
        _repo = repo;
        _currentTenant = currentTenant;
        _currentCompany = currentCompany;
        _currentUser = currentUser;
        _resolver = resolver;
    }

    public async Task<Result<OperationalPreferencesDto>> Handle(
        UpdateOperationalPreferencesCommand command,
        CancellationToken cancellationToken
    )
    {
        var tenantId = _currentTenant.TenantId;
        var companyId = _currentCompany.CompanyId;
        var userId = _currentUser.UserId;

        if (command.SalesPos is { } salesPos)
        {
            await UpsertAsync(tenantId, companyId, OrgSettingKeys.SalesPos.RequireOpenCashSession, Bool(salesPos.RequireOpenCashSession), SettingDataType.Bool, userId, cancellationToken);
            await UpsertAsync(tenantId, companyId, OrgSettingKeys.SalesPos.AllowManualPrice, Bool(salesPos.AllowManualPrice), SettingDataType.Bool, userId, cancellationToken);
            await UpsertAsync(tenantId, companyId, OrgSettingKeys.SalesPos.AllowManualDiscount, Bool(salesPos.AllowManualDiscount), SettingDataType.Bool, userId, cancellationToken);
            await UpsertAsync(tenantId, companyId, OrgSettingKeys.SalesPos.MaxDiscountPercent, Decimal(salesPos.MaxDiscountPercent), SettingDataType.Decimal, userId, cancellationToken);
            await UpsertAsync(tenantId, companyId, OrgSettingKeys.SalesPos.RequireCustomerAboveAmount, salesPos.RequireCustomerAboveAmount is { } rca ? Decimal(rca) : null, SettingDataType.Decimal, userId, cancellationToken);
            await UpsertAsync(tenantId, companyId, OrgSettingKeys.SalesPos.AllowSellWithoutStock, Bool(salesPos.AllowSellWithoutStock), SettingDataType.Bool, userId, cancellationToken);
            await UpsertAsync(tenantId, companyId, OrgSettingKeys.SalesPos.AskBeforeIssue, Bool(salesPos.AskBeforeIssue), SettingDataType.Bool, userId, cancellationToken);
            await UpsertAsync(tenantId, companyId, OrgSettingKeys.SalesPos.DefaultPriceListId, salesPos.DefaultPriceListId?.ToString(), SettingDataType.Guid, userId, cancellationToken);
            await UpsertAsync(tenantId, companyId, OrgSettingKeys.SalesPos.DefaultCustomerId, salesPos.DefaultCustomerId?.ToString(), SettingDataType.Guid, userId, cancellationToken);
        }

        if (command.Cash is { } cash)
        {
            await UpsertAsync(tenantId, companyId, OrgSettingKeys.Cash.RequireOpeningAmount, Bool(cash.RequireOpeningAmount), SettingDataType.Bool, userId, cancellationToken);
            await UpsertAsync(tenantId, companyId, OrgSettingKeys.Cash.AllowCloseWithDifference, Bool(cash.AllowCloseWithDifference), SettingDataType.Bool, userId, cancellationToken);
            await UpsertAsync(tenantId, companyId, OrgSettingKeys.Cash.MaxAllowedDifference, Decimal(cash.MaxAllowedDifference), SettingDataType.Decimal, userId, cancellationToken);
            await UpsertAsync(tenantId, companyId, OrgSettingKeys.Cash.RequireReasonForDifference, Bool(cash.RequireReasonForDifference), SettingDataType.Bool, userId, cancellationToken);
            await UpsertAsync(tenantId, companyId, OrgSettingKeys.Cash.AllowManualInOutMovements, Bool(cash.AllowManualInOutMovements), SettingDataType.Bool, userId, cancellationToken);
            await UpsertAsync(tenantId, companyId, OrgSettingKeys.Cash.RequireReasonForMovements, Bool(cash.RequireReasonForMovements), SettingDataType.Bool, userId, cancellationToken);
        }

        if (command.Purchases is { } purchases)
        {
            await UpsertAsync(tenantId, companyId, OrgSettingKeys.Purchases.DefaultWarehouseId, purchases.DefaultWarehouseId?.ToString(), SettingDataType.Guid, userId, cancellationToken);
            await UpsertAsync(tenantId, companyId, OrgSettingKeys.Purchases.AllowConfirmWithoutReceptionXml, Bool(purchases.AllowConfirmWithoutReceptionXml), SettingDataType.Bool, userId, cancellationToken);
            await UpsertAsync(tenantId, companyId, OrgSettingKeys.Purchases.UpdateCostOnConfirm, Bool(purchases.UpdateCostOnConfirm), SettingDataType.Bool, userId, cancellationToken);
            await UpsertAsync(tenantId, companyId, OrgSettingKeys.Purchases.AllowManualCostChange, Bool(purchases.AllowManualCostChange), SettingDataType.Bool, userId, cancellationToken);
            await UpsertAsync(tenantId, companyId, OrgSettingKeys.Purchases.RequireReasonForCostChange, Bool(purchases.RequireReasonForCostChange), SettingDataType.Bool, userId, cancellationToken);
        }

        if (command.Inventory is { } inventory)
        {
            await UpsertAsync(tenantId, companyId, OrgSettingKeys.Inventory.AllowNegativeStock, Bool(inventory.AllowNegativeStock), SettingDataType.Bool, userId, cancellationToken);
            await UpsertAsync(tenantId, companyId, OrgSettingKeys.Inventory.RequireReasonForAdjustment, Bool(inventory.RequireReasonForAdjustment), SettingDataType.Bool, userId, cancellationToken);
            await UpsertAsync(tenantId, companyId, OrgSettingKeys.Inventory.RequireApprovalForLargeAdjustment, Bool(inventory.RequireApprovalForLargeAdjustment), SettingDataType.Bool, userId, cancellationToken);
            await UpsertAsync(tenantId, companyId, OrgSettingKeys.Inventory.LargeAdjustmentThresholdAmount, Decimal(inventory.LargeAdjustmentThresholdAmount), SettingDataType.Decimal, userId, cancellationToken);
        }

        if (command.Printing is { } printing)
        {
            await UpsertAsync(tenantId, companyId, OrgSettingKeys.Printing.SalesReceiptMode, printing.SalesReceiptMode, SettingDataType.String, userId, cancellationToken);
            await UpsertAsync(tenantId, companyId, OrgSettingKeys.Printing.SalesReceiptCopies, printing.SalesReceiptCopies.ToString(CultureInfo.InvariantCulture), SettingDataType.Int, userId, cancellationToken);
            await UpsertAsync(tenantId, companyId, OrgSettingKeys.Printing.SalesReceiptPaperWidth, printing.SalesReceiptPaperWidth, SettingDataType.String, userId, cancellationToken);
            await UpsertAsync(tenantId, companyId, OrgSettingKeys.Printing.SalesReceiptIncludeLogo, Bool(printing.SalesReceiptIncludeLogo), SettingDataType.Bool, userId, cancellationToken);
            await UpsertAsync(tenantId, companyId, OrgSettingKeys.Printing.SalesReceiptIncludeAccessKey, Bool(printing.SalesReceiptIncludeAccessKey), SettingDataType.Bool, userId, cancellationToken);
            await UpsertAsync(tenantId, companyId, OrgSettingKeys.Printing.SalesReceiptIncludeCashier, Bool(printing.SalesReceiptIncludeCashier), SettingDataType.Bool, userId, cancellationToken);
            await UpsertAsync(tenantId, companyId, OrgSettingKeys.Printing.SalesReceiptOpenCashDrawer, Bool(printing.SalesReceiptOpenCashDrawer), SettingDataType.Bool, userId, cancellationToken);
        }

        if (command.ElectronicDocuments is { } ed)
        {
            await UpsertAsync(tenantId, companyId, OrgSettingKeys.ElectronicDocuments.AutoRetryEnabled, Bool(ed.AutoRetryEnabled), SettingDataType.Bool, userId, cancellationToken);
            await UpsertAsync(tenantId, companyId, OrgSettingKeys.ElectronicDocuments.MaxRetryAttempts, ed.MaxRetryAttempts.ToString(CultureInfo.InvariantCulture), SettingDataType.Int, userId, cancellationToken);
            await UpsertAsync(tenantId, companyId, OrgSettingKeys.ElectronicDocuments.GenerateRideOnAuthorization, Bool(ed.GenerateRideOnAuthorization), SettingDataType.Bool, userId, cancellationToken);
            await UpsertAsync(tenantId, companyId, OrgSettingKeys.ElectronicDocuments.EmailOnAuthorization, Bool(ed.EmailOnAuthorization), SettingDataType.Bool, userId, cancellationToken);
        }

        if (command.Notifications is { } notifications)
        {
            await UpsertAsync(tenantId, companyId, OrgSettingKeys.Communications.SalesInvoiceAuthorizedEnabled, Bool(notifications.SalesInvoiceAuthorizedEnabled), SettingDataType.Bool, userId, cancellationToken);
            await UpsertAsync(tenantId, companyId, OrgSettingKeys.Communications.SendCopyToCompanyEmail, Bool(notifications.SendCopyToCompanyEmail), SettingDataType.Bool, userId, cancellationToken);
            await UpsertAsync(tenantId, companyId, OrgSettingKeys.Communications.DefaultLanguage, notifications.DefaultLanguage, SettingDataType.String, userId, cancellationToken);
        }

        await _repo.SaveChangesAsync(cancellationToken);

        var p = await _resolver.ResolveAsync(cancellationToken);
        return Result<OperationalPreferencesDto>.Success(
            new OperationalPreferencesDto(
                SalesPos: new SalesPosPreferencesDto(
                    p.SalesPos.RequireOpenCashSession,
                    p.SalesPos.AllowManualPrice,
                    p.SalesPos.AllowManualDiscount,
                    p.SalesPos.MaxDiscountPercent,
                    p.SalesPos.RequireCustomerAboveAmount,
                    p.SalesPos.AllowSellWithoutStock,
                    p.SalesPos.AskBeforeIssue,
                    p.SalesPos.DefaultPriceListId,
                    p.SalesPos.DefaultCustomerId
                ),
                Cash: new CashPreferencesDto(
                    p.Cash.RequireOpeningAmount,
                    p.Cash.AllowCloseWithDifference,
                    p.Cash.MaxAllowedDifference,
                    p.Cash.RequireReasonForDifference,
                    p.Cash.AllowManualInOutMovements,
                    p.Cash.RequireReasonForMovements
                ),
                Purchases: new PurchasesPreferencesDto(
                    p.Purchases.DefaultWarehouseId,
                    p.Purchases.AllowConfirmWithoutReceptionXml,
                    p.Purchases.UpdateCostOnConfirm,
                    p.Purchases.AllowManualCostChange,
                    p.Purchases.RequireReasonForCostChange
                ),
                Inventory: new InventoryPreferencesDto(
                    p.Inventory.AllowNegativeStock,
                    p.Inventory.RequireReasonForAdjustment,
                    p.Inventory.RequireApprovalForLargeAdjustment,
                    p.Inventory.LargeAdjustmentThresholdAmount
                ),
                Printing: new PrintingPreferencesDto(
                    p.Printing.SalesReceiptMode,
                    p.Printing.SalesReceiptCopies,
                    p.Printing.SalesReceiptPaperWidth,
                    p.Printing.SalesReceiptIncludeLogo,
                    p.Printing.SalesReceiptIncludeAccessKey,
                    p.Printing.SalesReceiptIncludeCashier,
                    p.Printing.SalesReceiptOpenCashDrawer
                ),
                ElectronicDocuments: new ElectronicDocumentsPreferencesDto(
                    p.ElectronicDocuments.AutoRetryEnabled,
                    p.ElectronicDocuments.MaxRetryAttempts,
                    p.ElectronicDocuments.GenerateRideOnAuthorization,
                    p.ElectronicDocuments.EmailOnAuthorization
                ),
                Notifications: new NotificationsPreferencesDto(
                    p.Notifications.SalesInvoiceAuthorizedEnabled,
                    p.Notifications.SendCopyToCompanyEmail,
                    p.Notifications.DefaultLanguage
                )
            )
        );
    }

    private static string Bool(bool value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Decimal(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    private async Task UpsertAsync(
        Guid tenantId,
        Guid companyId,
        string key,
        string? value,
        SettingDataType dataType,
        Guid userId,
        CancellationToken ct
    )
    {
        var setting = OrgSetting.Create(
            tenantId,
            companyId,
            OrgScope.Company,
            companyId,
            key,
            value,
            dataType,
            userId
        );

        await _repo.UpsertAsync(setting, ct);
    }
}
