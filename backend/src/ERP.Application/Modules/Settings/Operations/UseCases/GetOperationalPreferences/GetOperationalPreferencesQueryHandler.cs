using ERP.Application.Common;
using ERP.Application.Modules.Settings.Operations.DTOs;
using ERP.Domain.Configuration.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Settings.Operations.UseCases.GetOperationalPreferences;

public sealed class GetOperationalPreferencesQueryHandler
    : IRequestHandler<GetOperationalPreferencesQuery, Result<OperationalPreferencesDto>>
{
    private readonly IOperationalPreferencesResolver _resolver;

    public GetOperationalPreferencesQueryHandler(IOperationalPreferencesResolver resolver) =>
        _resolver = resolver;

    public async Task<Result<OperationalPreferencesDto>> Handle(
        GetOperationalPreferencesQuery request,
        CancellationToken cancellationToken
    )
    {
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
}
