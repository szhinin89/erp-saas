using ERP.Domain.Audit;
using ERP.Domain.Common;
using ERP.Domain.Modules.Purchases.Enums;

namespace ERP.Domain.Modules.Purchases.Entities;

/// <summary>
/// Auditoría de dominio de <see cref="SupplierCredit"/> (ADR-022, Entity Audit) — cubre creación,
/// aplicación/reversa de aplicación, reembolso/reversa de reembolso y <c>SourceReturnCancelled</c>
/// (diseño §20.1, §20.1bis). Append-only — nunca se edita ni se borra.
///
/// Campos poblados según la etapa/acción (§20.1, párrafo bajo la tabla y columna "Campos nullable
/// según etapa"): <see cref="TargetPurchasePayableId"/> solo en <c>Applied</c>/
/// <c>ApplicationReversed</c>; el grupo de campos del destino financiero (<see cref="FinancialDestinationId"/>,
/// <see cref="FinancialDestinationCodeSnapshot"/>, <see cref="DestinationTypeCodeSnapshot"/>,
/// <see cref="AccountingAccountId"/>, <see cref="CashRegisterId"/>, <see cref="CashSessionId"/>,
/// <see cref="CashMovementId"/>, <see cref="PaymentMethodCode"/>, <see cref="ExternalReference"/>,
/// <see cref="EffectiveDate"/>) solo en <c>Refunded</c>/<c>RefundReversed</c>; en
/// <c>SourceReturnCancelled</c> ambos grupos quedan <c>null</c> (§20.1bis).
///
/// Nota de alcance (remediación MINOR-01): <c>ClientRequestId</c>/<c>RequestPayloadHash</c>,
/// mencionados en la tabla de §20.1bis, no se agregan como columnas propias de esta entidad — el
/// mecanismo de idempotencia/correlación de una operación aún no existe en ningún punto de
/// <c>ERP.Domain</c> (ni siquiera en <see cref="PurchaseReturn"/>, que es quien origina el
/// movimiento de sistema); es infraestructura de Application/Fase 2 (§16.2). <see cref="AuditRecordBase.CorrelationId"/>/
/// <see cref="AuditRecordBase.RequestId"/>, ya heredados de la infraestructura FROZEN de auditoría,
/// cumplen el mismo propósito de correlación sin duplicar columnas ni tocar una clase FROZEN desde
/// este dominio.
/// </summary>
public sealed class SupplierCreditAudit : AuditRecordBase, ICompanyOperationalEntity
{
    public Guid CompanyId { get; private set; }

    /// <summary>
    /// <c>BranchId</c> del agregado auditado (diseño §20.1, párrafo posterior a la tabla) —
    /// mismo valor persistido y congelado de <see cref="SupplierCredit.BranchId"/> en el momento
    /// del evento (Branch Ownership Rule, §5.2). Histórico, nunca resuelto desde la sucursal
    /// activa del operador en tiempo de ejecución; no es una columna de identidad del actor (esa
    /// vive exclusivamente en <see cref="AuditActor"/>).
    /// </summary>
    public Guid BranchId { get; private set; }

    public Guid SupplierId { get; private set; }

    /// <summary>
    /// Tipo del movimiento auditado (<see cref="SupplierCreditMovementType"/>) — <c>null</c>
    /// únicamente para la acción <c>Created</c> (creación del crédito, que no es un movimiento de
    /// la colección <see cref="SupplierCredit.Movements"/>).
    /// </summary>
    public SupplierCreditMovementType? MovementType { get; private set; }

    public decimal? Amount { get; private set; }

    /// <summary><c>SupplierCredit.AvailableAmount</c> inmediatamente antes del movimiento (§20.1bis).</summary>
    public decimal? BalanceBefore { get; private set; }

    /// <summary><c>SupplierCredit.AvailableAmount</c> inmediatamente después del movimiento (§20.1bis).</summary>
    public decimal? BalanceAfter { get; private set; }

    /// <summary>Estado derivado (<c>SupplierCredit.IsOpen</c>) antes del movimiento — <c>"Open"</c>/<c>"Closed"</c> (§20.1bis).</summary>
    public string? StatusBefore { get; private set; }

    /// <summary>Estado derivado (<c>SupplierCredit.IsOpen</c>) después del movimiento — <c>"Open"</c>/<c>"Closed"</c> (§20.1bis).</summary>
    public string? StatusAfter { get; private set; }

    /// <summary><c>PurchasePayable</c> destino — solo en <c>Applied</c>/<c>ApplicationReversed</c> (§20.1).</summary>
    public Guid? TargetPurchasePayableId { get; private set; }

    /// <summary>
    /// <c>PurchaseReturn</c> de origen del crédito, mismo valor que <see cref="SupplierCredit.SourcePurchaseReturnId"/>
    /// — exigido explícitamente para <c>SourceReturnCancelled</c> (§20.1bis).
    /// </summary>
    public Guid? SourcePurchaseReturnId { get; private set; }

    // ── Grupo "destino financiero del reembolso" (§6.4, §20.1) — solo Refunded/RefundReversed ──
    public Guid? FinancialDestinationId { get; private set; }
    public string? FinancialDestinationCodeSnapshot { get; private set; }
    public string? DestinationTypeCodeSnapshot { get; private set; }
    public Guid? AccountingAccountId { get; private set; }
    public Guid? CashRegisterId { get; private set; }
    public Guid? CashSessionId { get; private set; }
    public Guid? CashMovementId { get; private set; }
    public string? PaymentMethodCode { get; private set; }
    public string? ExternalReference { get; private set; }
    public DateOnly? EffectiveDate { get; private set; }

    private SupplierCreditAudit() { }

    public static SupplierCreditAudit Create(
        AuditActor actor,
        Guid companyId,
        Guid branchId,
        Guid supplierCreditId,
        Guid supplierId,
        string action,
        SupplierCreditMovementType? movementType = null,
        decimal? amount = null,
        decimal? balanceBefore = null,
        decimal? balanceAfter = null,
        string? statusBefore = null,
        string? statusAfter = null,
        Guid? targetPurchasePayableId = null,
        Guid? sourcePurchaseReturnId = null,
        Guid? financialDestinationId = null,
        string? financialDestinationCodeSnapshot = null,
        string? destinationTypeCodeSnapshot = null,
        Guid? accountingAccountId = null,
        Guid? cashRegisterId = null,
        Guid? cashSessionId = null,
        Guid? cashMovementId = null,
        string? paymentMethodCode = null,
        string? externalReference = null,
        DateOnly? effectiveDate = null,
        string? reason = null
    )
    {
        if (companyId == Guid.Empty)
            throw new ArgumentException("companyId requerido.", nameof(companyId));
        if (branchId == Guid.Empty)
            throw new ArgumentException("branchId requerido.", nameof(branchId));

        var audit = new SupplierCreditAudit
        {
            CompanyId = companyId,
            BranchId = branchId,
            SupplierId = supplierId,
            MovementType = movementType,
            Amount = amount,
            BalanceBefore = balanceBefore,
            BalanceAfter = balanceAfter,
            StatusBefore = statusBefore,
            StatusAfter = statusAfter,
            TargetPurchasePayableId = targetPurchasePayableId,
            SourcePurchaseReturnId = sourcePurchaseReturnId,
            FinancialDestinationId = financialDestinationId,
            FinancialDestinationCodeSnapshot = financialDestinationCodeSnapshot,
            DestinationTypeCodeSnapshot = destinationTypeCodeSnapshot,
            AccountingAccountId = accountingAccountId,
            CashRegisterId = cashRegisterId,
            CashSessionId = cashSessionId,
            CashMovementId = cashMovementId,
            PaymentMethodCode = paymentMethodCode,
            ExternalReference = externalReference,
            EffectiveDate = effectiveDate,
        };
        audit.SetCommon(actor, supplierCreditId, action, reason);
        return audit;
    }
}
