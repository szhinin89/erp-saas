using ERP.Domain.Common;

namespace ERP.Domain.MasterData.Entities;

/// <summary>
/// Aggregate Root: default de condición de pago de un proveedor (BusinessPartner) en una Company
/// específica — ADR-033, Fase 3.
///
/// SCOPE: ITenantScopedEntity + ICompanyScopedEntity.
/// Query filter fail-closed en ambas dimensiones. Sin company context → 0 filas.
///
/// CONTIENE: únicamente el default de condición de pago para compras/gastos por empresa.
/// NO CONTIENE: riesgo/crédito de proveedor (bloqueo, límite de crédito) — eso es un concepto de
///              Cliente y vive en CompanyBpTradingSettings, nunca se fusiona aquí.
///
/// Única entrada por (tenant_id, company_id, business_partner_id).
///
/// PaymentTermId es opcional: ausente significa "sin default configurado para esta empresa" — la
/// resolución de default (IPaymentTermDefaultResolver, Fase 3b) exige selección explícita en ese
/// caso, nunca infiere un valor. SupplierRoleConfig.PaymentTermId (tenant-wide) deja de ser la
/// fuente operativa una vez que existe esta entidad — solo se usa como semilla de backfill.
/// </summary>
public sealed class CompanyBpPurchaseSettings
    : AuditableEntity,
        ITenantScopedEntity,
        ICompanyScopedEntity
{
    public Guid CompanyId { get; private set; }
    public Guid BusinessPartnerId { get; private set; }
    public Guid? PaymentTermId { get; private set; }

    private CompanyBpPurchaseSettings() { }

    public static CompanyBpPurchaseSettings Create(
        Guid tenantId,
        Guid companyId,
        Guid businessPartnerId,
        Guid? paymentTermId,
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

        var settings = new CompanyBpPurchaseSettings
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BusinessPartnerId = businessPartnerId,
            PaymentTermId = paymentTermId,
        };
        settings.SetCreated(createdBy);
        return settings;
    }

    /// <summary>Actualiza el default. Usado en el flujo Upsert del handler (Fase 3d).</summary>
    public void SetPaymentTerm(Guid? paymentTermId, Guid updatedBy)
    {
        PaymentTermId = paymentTermId;
        SetUpdated(updatedBy);
    }
}
