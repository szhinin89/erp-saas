using ERP.Domain.Audit;
using ERP.Domain.Common;

namespace ERP.Domain.MasterData.Entities;

/// <summary>
/// Auditoría de dominio (ADR-022) de <see cref="SupplierExpenseSubcategory"/>: alta, retiro
/// (baja lógica) y reactivación de una asignación proveedor↔subcategoría de gasto. Append-only;
/// sin campos de otros dominios.
/// </summary>
public sealed class SupplierExpenseSubcategoryAudit : AuditRecordBase, ICompanyOperationalEntity
{
    public Guid CompanyId { get; private set; }
    public Guid BusinessPartnerId { get; private set; }
    public Guid ExpenseSubcategoryId { get; private set; }

    private SupplierExpenseSubcategoryAudit() { }

    public static SupplierExpenseSubcategoryAudit Create(
        AuditActor actor,
        Guid companyId,
        Guid assignmentId,
        Guid businessPartnerId,
        Guid expenseSubcategoryId,
        string action,
        string? reason = null
    )
    {
        if (companyId == Guid.Empty)
            throw new ArgumentException("companyId requerido.", nameof(companyId));

        var audit = new SupplierExpenseSubcategoryAudit
        {
            CompanyId = companyId,
            BusinessPartnerId = businessPartnerId,
            ExpenseSubcategoryId = expenseSubcategoryId,
        };
        audit.SetCommon(actor, assignmentId, action, reason);
        return audit;
    }
}
