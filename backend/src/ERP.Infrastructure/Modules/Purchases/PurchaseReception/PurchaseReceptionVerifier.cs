using ERP.Application.Common;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.MasterData.ValueObjects;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Models;

namespace ERP.Infrastructure.Modules.Purchases.PurchaseReception;

/// <summary>
/// Cruza registros parseados del TXT SRI contra proveedores (BusinessPartner + rol Supplier) y
/// compras (PurchaseInvoice.AccessKey) ya existentes en el ERP — mismo criterio de resolución de
/// proveedor que ya usa <c>CreatePurchaseDraftHandler</c>, sin duplicar la lógica.
/// </summary>
public sealed class PurchaseReceptionVerifier : IPurchaseReceptionVerifier
{
    private readonly IBusinessPartnerRepository _bpRepo;
    private readonly IBusinessPartnerRoleRepository _roleRepo;
    private readonly IPurchaseInvoiceRepository _purchaseRepo;
    private readonly ICurrentTenant _tenant;

    public PurchaseReceptionVerifier(
        IBusinessPartnerRepository bpRepo,
        IBusinessPartnerRoleRepository roleRepo,
        IPurchaseInvoiceRepository purchaseRepo,
        ICurrentTenant tenant
    )
    {
        _bpRepo = bpRepo;
        _roleRepo = roleRepo;
        _purchaseRepo = purchaseRepo;
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

            var businessPartner = await _bpRepo.GetByIdentificationAsync(
                TaxIdentification.SriRuc,
                record.SupplierRuc,
                cancellationToken
            );
            if (businessPartner is not null)
            {
                var supplierRole = await _roleRepo.GetByTypeAsync(
                    businessPartner.Id,
                    RoleType.Supplier,
                    cancellationToken
                );
                supplierExists = supplierRole is not null;
            }

            var purchaseExists = false;
            Guid? purchaseId = null;
            if (supplierExists)
            {
                var purchase = await _purchaseRepo.GetByAccessKeyAsync(
                    _tenant.TenantId,
                    record.AccessKey,
                    cancellationToken
                );
                purchaseExists = purchase is not null;
                purchaseId = purchase?.Id;
            }

            var status =
                purchaseExists ? PurchaseReceptionStatus.Imported
                : supplierExists ? PurchaseReceptionStatus.Pending
                : PurchaseReceptionStatus.NewSupplier;

            var supplierId = supplierExists ? businessPartner?.Id : null;

            items.Add(
                new PurchaseReceptionVerifiedItem(
                    record,
                    supplierExists,
                    purchaseExists,
                    status,
                    supplierId,
                    purchaseId
                )
            );
        }

        return items;
    }
}
