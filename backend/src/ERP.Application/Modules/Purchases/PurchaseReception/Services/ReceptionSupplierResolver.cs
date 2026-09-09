using ERP.Application.Common;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.MasterData.ValueObjects;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;

namespace ERP.Application.Modules.Purchases.PurchaseReception.Services;

internal static class ReceptionSupplierResolver
{
    public static async Task<Result<BusinessPartner>> ResolveAsync(
        PurchaseReceptionDocument document, IBusinessPartnerRepository partners,
        IPurchaseReceptionDocumentRepository documents, Guid tenantId, Guid userId,
        CancellationToken ct)
    {
        var supplier = document.SupplierId is { } id
            ? await partners.GetByIdAsync(id, ct)
            : await partners.GetByIdentificationAsync(TaxIdentification.SriRuc, document.SupplierRuc.Trim(), ct);
        if (supplier is null || supplier.TenantId != tenantId)
            return Result<BusinessPartner>.ValidationFailure("Cree primero el proveedor antes de generar el borrador.");
        if (!supplier.IsActive)
            return Result<BusinessPartner>.ValidationFailure($"El proveedor '{supplier.Name.LegalName}' se encuentra inactivo.");

        if (document.SupplierId is null)
        {
            document.AssignSupplier(supplier.Id, userId);
            await documents.SaveChangesAsync(ct);
        }
        return Result<BusinessPartner>.Success(supplier);
    }
}
