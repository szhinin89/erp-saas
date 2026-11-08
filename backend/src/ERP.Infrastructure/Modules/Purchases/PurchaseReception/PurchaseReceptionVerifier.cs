using ERP.Application.Common;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.MasterData.ValueObjects;
using ERP.Domain.Modules.Expenses.Interfaces;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Models;
using ERP.Domain.Modules.Purchases.PurchaseReception.Services;

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
            var (supplierExists, supplierIsActive, supplierId) = await ResolveSupplierAsync(
                record.SupplierRuc,
                cancellationToken
            );

            var purchase = PurchaseReceptionPurchaseInfo.None;
            var expenseExists = false;
            Guid? cancelledExpenseId = null;
            if (supplierExists)
            {
                // RECEPTION-REPROCESS-AFTER-CANCEL-STANDARD-01 — GetByAccessKeyAsync/
                // ExistsByAccessKeyAsync ya ignoran Cancelled (solo Draft/Confirmed cuenta como
                // "activa"); el historial de la más reciente Cancelled se resuelve aparte, solo
                // cuando no hay ninguna activa, para "Ver compra/gasto anulado" en la UI.
                // FASE 5 PURCHASE-UX — misma resolución que la descarga de XML individual/lote.
                purchase = await PurchaseReceptionPurchaseStateResolver.ResolveAsync(
                    _purchaseRepo,
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
                purchase.State is PurchaseReceptionPurchaseState.Draft or PurchaseReceptionPurchaseState.Confirmed
                    ? PurchaseReceptionStatus.Imported
                : supplierExists ? PurchaseReceptionStatus.Pending
                : PurchaseReceptionStatus.NewSupplier;

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
                    status,
                    supplierId,
                    purchase.PurchaseId,
                    affectedPurchaseExists,
                    affectedPurchaseId,
                    supplierIsActive,
                    expenseExists,
                    purchase.CancelledPurchaseId,
                    cancelledExpenseId,
                    purchase.State
                )
            );
        }

        return items;
    }

    /// <summary>
    /// PURCHASES-RECEPTION-SUPPLIER-RULE-01 (H4) — misma regla para factura y NC: el proveedor está
    /// identificado si el socio y su rol Proveedor son del tenant, activos o no, para reconocer los
    /// documentos históricos ya registrados. El estado activo (socio y rol) se informa aparte; las
    /// operaciones nuevas lo exigen en el servidor (<c>ReceptionSupplierResolver</c>), salvo la NC,
    /// que siempre se vincula a una compra existente del mismo proveedor
    /// (<c>CreateDraftPurchaseCreditNoteHandler</c>).
    /// </summary>
    private async Task<(bool Exists, bool? IsActive, Guid? SupplierId)> ResolveSupplierAsync(
        string supplierRuc,
        CancellationToken cancellationToken
    )
    {
        var businessPartner = await _bpRepo.GetByIdentificationAsync(
            TaxIdentification.SriRuc,
            supplierRuc,
            cancellationToken
        );
        if (businessPartner is null || businessPartner.TenantId != _tenant.TenantId)
            return (false, null, null);

        var supplierRole = await _roleRepo.GetByTypeAsync(
            businessPartner.Id,
            RoleType.Supplier,
            cancellationToken
        );
        if (supplierRole is null || supplierRole.TenantId != _tenant.TenantId)
            return (false, null, null);

        return (true, businessPartner.IsActive && supplierRole.IsActive, businessPartner.Id);
    }
}
