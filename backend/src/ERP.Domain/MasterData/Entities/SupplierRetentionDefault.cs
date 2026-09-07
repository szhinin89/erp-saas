using ERP.Domain.Common;

namespace ERP.Domain.MasterData.Entities;

/// <summary>
/// Retención predeterminada de un proveedor (BusinessPartner) en una Company específica —
/// RETENTIONS-SUPPLIER-DEFAULTS-DYNAMIC-01. Reemplaza los antiguos
/// SupplierRoleConfig.DefaultRetentionVatCode/DefaultRetentionIncomeCode (un único código fijo
/// por impuesto, tenant-wide) por una lista dinámica: un proveedor puede tener N retenciones
/// activas por empresa, incluidas varias del mismo impuesto (ej. distintos códigos Renta según el
/// tipo de gasto).
///
/// Mismo patrón de scope que <see cref="CompanyBpPurchaseSettings"/> (ADR-033, Fase 3):
/// ITenantScopedEntity + ICompanyScopedEntity, query filter fail-closed en ambas dimensiones.
///
/// FK real a SriRetentionCode (nunca string suelto) — TaxType/Name/Percentage se leen siempre del
/// catálogo vigente vía SriRetentionCodeId, nunca se duplican aquí (SSOT dinámico).
///
/// IsActive permite desactivar una fila sin DELETE físico (soft-disable). DisplayOrder es solo de
/// presentación en UI — no participa en ninguna regla de elegibilidad/cálculo.
/// </summary>
public sealed class SupplierRetentionDefault
    : AuditableEntity,
        ITenantScopedEntity,
        ICompanyScopedEntity
{
    public Guid CompanyId { get; private set; }
    public Guid BusinessPartnerId { get; private set; }
    public Guid SriRetentionCodeId { get; private set; }
    public bool IsActive { get; private set; } = true;
    public int DisplayOrder { get; private set; }

    private SupplierRetentionDefault() { }

    public static SupplierRetentionDefault Create(
        Guid tenantId,
        Guid companyId,
        Guid businessPartnerId,
        Guid sriRetentionCodeId,
        int displayOrder,
        Guid createdBy
    )
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("tenantId es obligatorio.", nameof(tenantId));
        if (companyId == Guid.Empty)
            throw new ArgumentException("CompanyId es obligatorio.", nameof(companyId));
        if (businessPartnerId == Guid.Empty)
            throw new ArgumentException(
                "BusinessPartnerId es obligatorio.",
                nameof(businessPartnerId)
            );
        if (sriRetentionCodeId == Guid.Empty)
            throw new ArgumentException(
                "SriRetentionCodeId es obligatorio.",
                nameof(sriRetentionCodeId)
            );

        var entry = new SupplierRetentionDefault
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BusinessPartnerId = businessPartnerId,
            SriRetentionCodeId = sriRetentionCodeId,
            IsActive = true,
            DisplayOrder = displayOrder,
        };
        entry.SetCreated(createdBy);
        return entry;
    }

    public void Activate(Guid updatedBy)
    {
        IsActive = true;
        SetUpdated(updatedBy);
    }

    public void Deactivate(Guid updatedBy)
    {
        IsActive = false;
        SetUpdated(updatedBy);
    }

    public void SetDisplayOrder(int displayOrder, Guid updatedBy)
    {
        DisplayOrder = displayOrder;
        SetUpdated(updatedBy);
    }
}
