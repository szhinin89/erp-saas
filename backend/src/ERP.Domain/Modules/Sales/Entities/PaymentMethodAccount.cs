using ERP.Domain.Common;

namespace ERP.Domain.Modules.Sales.Entities;

/// <summary>
/// SALES-TRANSFER-ACCOUNTING-CASH-VS-BANK-01: vínculo Company-scoped entre un <see cref="PaymentMethod"/>
/// (catálogo tenant-wide, compartido entre todas las Companies del tenant — ver
/// <c>PaymentMethodConfiguration</c>, único índice <c>(TenantId, Code)</c>) y la cuenta contable real
/// que debe recibir el débito de "dinero real cobrado" (Caja/Bancos) cuando ese método se usa en una
/// venta de ESA Company específica.
///
/// Entidad separada — no un campo <c>AccountingAccountId</c> directo en <see cref="PaymentMethod"/> —
/// porque <see cref="Domain.Modules.Accounting.Entities.Account"/> es estrictamente CompanyId-scoped
/// (ADR-026 §2: "no existe cuenta compartida entre companies"), mientras que <c>PaymentMethod</c> es
/// tenant-wide (una fila de "Transferencia Bancaria" sirve a todas las Companies del tenant). Un
/// campo escalar en <c>PaymentMethod</c> sería incorrecto para cualquier tenant con más de una
/// Company: la misma fila necesitaría apuntar a la cuenta "Bancos" de la Company A y, simultáneamente,
/// a la cuenta "Bancos" de la Company B. Esta entidad resuelve eso con una fila por
/// (TenantId, CompanyId, PaymentMethodId) — mismo criterio de <see cref="Finance.Entities.CompanyFinancialDestination"/>
/// (también Company-scoped) para el problema análogo de Finance/Payment.
///
/// Sin fila para un (Company, PaymentMethod) dado → ese método no tiene cuenta configurada en esa
/// Company: la autorización de venta debe fallar con error claro (fail-closed), nunca caer en un
/// default oculto como "Caja general" — ver <c>AuthorizeSalesInvoiceHandler</c>.
/// </summary>
public sealed class PaymentMethodAccount : AuditableEntity, ITenantScopedEntity, ICompanyOperationalEntity
{
    public Guid CompanyId { get; private set; }
    public Guid PaymentMethodId { get; private set; }
    public Guid AccountingAccountId { get; private set; }

    private PaymentMethodAccount() { }

    public static PaymentMethodAccount Create(
        Guid tenantId,
        Guid companyId,
        Guid paymentMethodId,
        Guid accountingAccountId,
        Guid createdBy
    )
    {
        if (paymentMethodId == Guid.Empty)
            throw new ArgumentException("El método de pago es obligatorio.", nameof(paymentMethodId));
        if (accountingAccountId == Guid.Empty)
            throw new ArgumentException(
                "La cuenta contable es obligatoria.",
                nameof(accountingAccountId)
            );

        var link = new PaymentMethodAccount
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            PaymentMethodId = paymentMethodId,
            AccountingAccountId = accountingAccountId,
        };
        link.SetCreated(createdBy);
        return link;
    }

    public void ChangeAccount(Guid accountingAccountId, Guid updatedBy)
    {
        if (accountingAccountId == Guid.Empty)
            throw new ArgumentException(
                "La cuenta contable es obligatoria.",
                nameof(accountingAccountId)
            );
        AccountingAccountId = accountingAccountId;
        SetUpdated(updatedBy);
    }
}
