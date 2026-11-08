using ERP.Application.Common;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.MasterData.ValueObjects;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;

namespace ERP.Application.Modules.Purchases.PurchaseReception.Services;

/// <summary>
/// Proveedor de una operación NUEVA desde recepción (borrador de compra o de gasto). PURCHASES-
/// RECEPTION-SUPPLIER-RULE-01 (H4): exige socio activo y rol Proveedor activo del tenant antes de
/// cualquier escritura; el reconocimiento de documentos históricos no pasa por aquí (verificador).
/// </summary>
internal static class ReceptionSupplierResolver
{
    internal const string InactiveSupplierRoleMessage =
        "El tercero no tiene un rol de proveedor activo.";

    public static async Task<Result<BusinessPartner>> ResolveAsync(
        PurchaseReceptionDocument document,
        IBusinessPartnerRepository partners,
        IBusinessPartnerRoleRepository roles,
        IPurchaseReceptionDocumentRepository documents,
        Guid tenantId,
        Guid userId,
        CancellationToken ct
    )
    {
        var supplier = document.SupplierId is { } id
            ? await partners.GetByIdAsync(id, ct)
            : await partners.GetByIdentificationAsync(
                TaxIdentification.SriRuc,
                document.SupplierRuc.Trim(),
                ct
            );
        if (supplier is null || supplier.TenantId != tenantId)
            return Result<BusinessPartner>.ValidationFailure(
                "Cree primero el proveedor antes de generar el borrador."
            );
        if (!supplier.IsActive)
            return Result<BusinessPartner>.ValidationFailure(
                $"El proveedor '{supplier.Name.LegalName}' se encuentra inactivo."
            );

        var role = await roles.GetByTypeAsync(supplier.Id, RoleType.Supplier, ct);
        if (role is null || !role.IsActive || role.TenantId != tenantId)
            return Result<BusinessPartner>.ValidationFailure(InactiveSupplierRoleMessage);

        if (document.SupplierId is null)
        {
            document.AssignSupplier(supplier.Id, userId);
            await documents.SaveChangesAsync(ct);
        }
        return Result<BusinessPartner>.Success(supplier);
    }
}
