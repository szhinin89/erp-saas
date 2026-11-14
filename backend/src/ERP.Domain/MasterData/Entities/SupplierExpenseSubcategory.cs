using ERP.Domain.Common;
using ERP.Domain.Exceptions;
using ERP.Domain.MasterData.Events;

namespace ERP.Domain.MasterData.Entities;

/// <summary>
/// Asignación de una subcategoría de gasto a un proveedor (BusinessPartner) en una Company
/// específica — EXPENSE-SUPPLIER-SUBCATEGORIES-01. El conjunto de asignaciones ACTIVAS restringe
/// qué subcategorías ofrece y acepta Gastos para ese proveedor; cero asignaciones activas significa
/// «sin configuración» (catálogo completo).
///
/// Mismo patrón de scope que <see cref="SupplierRetentionDefault"/>: ITenantScopedEntity +
/// ICompanyScopedEntity, query filter fail-closed en ambas dimensiones. FKs simples; la pertenencia
/// de la subcategoría a la misma empresa la valida el handler y el filtro de
/// <c>expense_category_nodes</c>.
///
/// Baja lógica: <see cref="Disable"/> / <see cref="Enable"/> reutilizan el mismo registro (nunca
/// DELETE físico ni segunda fila: índice único sin filtro por IsActive). Cada transición levanta un
/// evento auditable (ADR-022) desde este agregado.
/// </summary>
public sealed class SupplierExpenseSubcategory
    : AuditableEntity,
        ITenantScopedEntity,
        ICompanyScopedEntity
{
    public Guid CompanyId { get; private set; }
    public Guid BusinessPartnerId { get; private set; }
    public Guid ExpenseSubcategoryId { get; private set; }
    public bool IsActive { get; private set; } = true;

    private SupplierExpenseSubcategory() { }

    public static SupplierExpenseSubcategory Create(
        Guid tenantId,
        Guid companyId,
        Guid businessPartnerId,
        Guid expenseSubcategoryId,
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
        if (expenseSubcategoryId == Guid.Empty)
            throw new ArgumentException(
                "ExpenseSubcategoryId es obligatorio.",
                nameof(expenseSubcategoryId)
            );

        var assignment = new SupplierExpenseSubcategory
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BusinessPartnerId = businessPartnerId,
            ExpenseSubcategoryId = expenseSubcategoryId,
            IsActive = true,
        };
        assignment.SetCreated(createdBy);
        assignment.RaiseDomainEvent(
            new SupplierExpenseSubcategoryAssignedEvent
            {
                TenantId = tenantId,
                AssignmentId = assignment.Id,
                CompanyId = companyId,
                BusinessPartnerId = businessPartnerId,
                ExpenseSubcategoryId = expenseSubcategoryId,
                AssignedBy = createdBy,
            }
        );
        return assignment;
    }

    /// <summary>Reactiva una asignación retirada reutilizando este mismo registro.</summary>
    public void Enable(Guid updatedBy)
    {
        if (IsActive)
            throw new DomainRuleViolationException("La asignación ya está activa.");
        IsActive = true;
        SetUpdated(updatedBy);
        RaiseDomainEvent(
            new SupplierExpenseSubcategoryEnabledEvent
            {
                TenantId = TenantId,
                AssignmentId = Id,
                CompanyId = CompanyId,
                BusinessPartnerId = BusinessPartnerId,
                ExpenseSubcategoryId = ExpenseSubcategoryId,
                EnabledBy = updatedBy,
            }
        );
    }

    /// <summary>Retira la asignación (baja lógica; la fila se conserva).</summary>
    public void Disable(Guid updatedBy)
    {
        if (!IsActive)
            throw new DomainRuleViolationException("La asignación ya está deshabilitada.");
        IsActive = false;
        SetUpdated(updatedBy);
        RaiseDomainEvent(
            new SupplierExpenseSubcategoryDisabledEvent
            {
                TenantId = TenantId,
                AssignmentId = Id,
                CompanyId = CompanyId,
                BusinessPartnerId = BusinessPartnerId,
                ExpenseSubcategoryId = ExpenseSubcategoryId,
                DisabledBy = updatedBy,
            }
        );
    }
}
