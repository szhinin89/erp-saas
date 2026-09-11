using ERP.Application.Common;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.MasterData.ValueObjects;
using ERP.Domain.Modules.Expenses.Interfaces;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Models;

namespace ERP.Infrastructure.Modules.Purchases.PurchaseReception;

/// <summary>
/// Cruza registros parseados del TXT SRI contra proveedores (BusinessPartner + rol Supplier) y
/// compras (PurchaseInvoice.AccessKey) ya existentes en el ERP — mismo criterio de resolución de
/// proveedor que ya usa <c>CreatePurchaseDraftHandler</c>, sin duplicar la lógica. EXPENSES-FROM-
/// RECEPTION-01 agrega el mismo cruce contra Gastos (ExpenseDocument.AccessKey).
/// </summary>
public sealed class PurchaseReceptionVerifier : IPurchaseReceptionVerifier
{
    private readonly IBusinessPartnerRepository _bpRepo;
    private readonly IBusinessPartnerRoleRepository _roleRepo;
    private readonly IPurchaseInvoiceRepository _purchaseRepo;
    private readonly IExpenseDocumentRepository _expenseRepo;
    private readonly ICurrentTenant _tenant;

    public PurchaseReceptionVerifier(
        IBusinessPartnerRepository bpRepo,
        IBusinessPartnerRoleRepository roleRepo,
        IPurchaseInvoiceRepository purchaseRepo,
        IExpenseDocumentRepository expenseRepo,
        ICurrentTenant tenant
    )
    {
        _bpRepo = bpRepo;
        _roleRepo = roleRepo;
        _purchaseRepo = purchaseRepo;
        _expenseRepo = expenseRepo;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<PurchaseReceptionVerifiedItem>> VerifyAsync(
        IReadOnlyList<PurchaseReceptionRecord> records,
        CancellationToken cancellationToken = default
    )
    {
        var items = new List<PurchaseReceptionVerifiedItem>(records.Count);

        foreach (var record in records)
        {
            var supplierExists = false;
            bool? supplierIsActive = null;

            var businessPartner = await _bpRepo.GetByIdentificationAsync(
                TaxIdentification.SriRuc,
                record.SupplierRuc,
                cancellationToken
            );
            if (businessPartner is not null)
            {
                supplierIsActive = businessPartner.IsActive;
                var supplierRole = await _roleRepo.GetByTypeAsync(
                    businessPartner.Id,
                    RoleType.Supplier,
                    cancellationToken
                );
                supplierExists = supplierRole is not null;
                if (record.SourceDocType == PurchaseReceptionSourceDocType.Invoice)
                    supplierExists = businessPartner.TenantId == _tenant.TenantId
                        && supplierRole is { IsActive: true } && supplierRole.TenantId == _tenant.TenantId;
            }

            var purchaseExists = false;
            Guid? purchaseId = null;
            Guid? cancelledPurchaseId = null;
            var expenseExists = false;
            Guid? cancelledExpenseId = null;
            if (supplierExists)
            {
                // RECEPTION-REPROCESS-AFTER-CANCEL-STANDARD-01 — GetByAccessKeyAsync/
                // ExistsByAccessKeyAsync ya ignoran Cancelled (solo Draft/Confirmed cuenta como
                // "activa"); el historial de la más reciente Cancelled se resuelve aparte, solo
                // cuando no hay ninguna activa, para "Ver compra/gasto anulado" en la UI.
                var purchase = await _purchaseRepo.GetByAccessKeyAsync(
                    _tenant.TenantId,
                    record.AccessKey,
                    cancellationToken
                );
                purchaseExists = purchase is not null;
                purchaseId = purchase?.Id;
                if (!purchaseExists)
                    cancelledPurchaseId = await _purchaseRepo.GetLatestCancelledIdByAccessKeyAsync(
                        _tenant.TenantId,
                        record.AccessKey,
                        cancellationToken
                    );

                expenseExists = await _expenseRepo.ExistsByAccessKeyAsync(
                    _tenant.TenantId,
                    record.AccessKey,
                    cancellationToken
                );
                if (!expenseExists)
                    cancelledExpenseId = await _expenseRepo.GetLatestCancelledIdByAccessKeyAsync(
                        _tenant.TenantId,
                        record.AccessKey,
                        cancellationToken
                    );
            }

            var status =
                purchaseExists ? PurchaseReceptionStatus.Imported
                : supplierExists ? PurchaseReceptionStatus.Pending
                : PurchaseReceptionStatus.NewSupplier;

            var supplierId = supplierExists ? businessPartner?.Id : null;

            var affectedPurchaseExists = false;
            Guid? affectedPurchaseId = null;
            if (
                supplierExists
                && supplierId.HasValue
                && record.SourceDocType == PurchaseReceptionSourceDocType.CreditNote
                && !string.IsNullOrWhiteSpace(record.ModifiedDocumentNumber)
            )
            {
                var affectedPurchase = await _purchaseRepo.GetBySupplierAndInvoiceNumberAsync(
                    _tenant.TenantId,
                    supplierId.Value,
                    record.ModifiedDocumentNumber,
                    cancellationToken
                );
                affectedPurchaseExists = affectedPurchase is not null;
                affectedPurchaseId = affectedPurchase?.Id;
            }

            items.Add(
                new PurchaseReceptionVerifiedItem(
                    record,
                    supplierExists,
                    purchaseExists,
                    status,
                    supplierId,
                    purchaseId,
                    affectedPurchaseExists,
                    affectedPurchaseId,
                    supplierIsActive,
                    expenseExists,
                    cancelledPurchaseId,
                    cancelledExpenseId
                )
            );
        }

        return items;
    }
}
