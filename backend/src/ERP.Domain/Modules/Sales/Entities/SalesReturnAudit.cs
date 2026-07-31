using ERP.Domain.Audit;
using ERP.Domain.Common;

namespace ERP.Domain.Modules.Sales.Entities;

/// <summary>
/// Auditoría de dominio de <see cref="SalesReturn"/>: cubre las transiciones Created/Authorized/
/// Cancelled. <see cref="GrandTotal"/> solo se conoce al autorizar (los totales se congelan recién
/// en <c>SalesReturn.Authorize()</c>) — queda <c>null</c> para Created/Cancelled, mismo criterio
/// que las columnas nullable de <c>SalesReturn.Authorized*</c>. <see cref="CustomerId"/> viene de
/// <see cref="ERP.Domain.Modules.Sales.Events.SalesReturnDraftCreatedEvent"/>/<see cref="ERP.Domain.Modules.Sales.Events.SalesReturnCancelledEvent"/>
/// (lo tienen); <see cref="ERP.Domain.Modules.Sales.Events.SalesReturnAuthorizedEvent"/> no lo
/// expone (evento ya congelado, documentado explícitamente como "listo para auditar sin
/// modificarlo") — queda <c>null</c> en ese caso, recuperable vía <see cref="SalesInvoiceId"/>.
/// Append-only — nunca se edita ni se borra.
/// </summary>
public sealed class SalesReturnAudit : AuditRecordBase, ICompanyOperationalEntity
{
    public Guid CompanyId { get; private set; }
    public Guid SalesInvoiceId { get; private set; }
    public Guid? CustomerId { get; private set; }
    public string ReturnNumber { get; private set; } = null!;
    public decimal? GrandTotal { get; private set; }

    private SalesReturnAudit() { }

    public static SalesReturnAudit Create(
        AuditActor actor,
        Guid companyId,
        Guid salesReturnId,
        Guid salesInvoiceId,
        string returnNumber,
        string action,
        Guid? customerId = null,
        decimal? grandTotal = null,
        string? reason = null
    )
    {
        if (companyId == Guid.Empty)
            throw new ArgumentException("companyId requerido.", nameof(companyId));

        var audit = new SalesReturnAudit
        {
            CompanyId = companyId,
            SalesInvoiceId = salesInvoiceId,
            CustomerId = customerId,
            ReturnNumber = returnNumber,
            GrandTotal = grandTotal,
        };
        audit.SetCommon(actor, salesReturnId, action, reason);
        return audit;
    }
}
