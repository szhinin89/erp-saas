using ERP.Domain.Common;

namespace ERP.Domain.Access.Entities;

/// <summary>
/// Autoriza a una <see cref="CompanyUserMembership"/> a operar en una <c>Branch</c> específica.
/// Fuente única de "sucursal autorizada" — no almacena rol ni permisos (eso vive exclusivamente
/// en <see cref="CompanyUserMembership.Role"/>), ni datos descriptivos de la sucursal (eso vive en
/// <c>Branch</c>).
/// Invariante cross-entity (BranchId perteneciente a la misma CompanyId de la membresía) se valida
/// aquí solo entre los dos Guid primitivos recibidos en <see cref="Create"/>; la verificación contra
/// el registro real de <c>Branch</c> requiere acceso a datos y es responsabilidad del handler de
/// aplicación que orquesta esta creación (fuera de alcance de Domain).
/// </summary>
public sealed class CompanyUserBranch : AuditableEntity, ITenantScopedEntity, ICompanyOperationalEntity
{
    public Guid CompanyId { get; private set; }
    public Guid CompanyUserMembershipId { get; private set; }
    public Guid BranchId { get; private set; }
    public bool IsActive { get; private set; }

    private CompanyUserBranch() { }

    public static CompanyUserBranch Create(
        Guid tenantId,
        Guid companyId,
        Guid companyUserMembershipId,
        Guid branchId,
        Guid createdBy)
    {
        if (companyId == Guid.Empty)
            throw new ArgumentException("La empresa es obligatoria.", nameof(companyId));
        if (companyUserMembershipId == Guid.Empty)
            throw new ArgumentException("La membresía es obligatoria.", nameof(companyUserMembershipId));
        if (branchId == Guid.Empty)
            throw new ArgumentException("La sucursal es obligatoria.", nameof(branchId));

        var entity = new CompanyUserBranch
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            CompanyUserMembershipId = companyUserMembershipId,
            BranchId = branchId,
            IsActive = true,
        };
        entity.SetCreated(createdBy);

        return entity;
    }

    /// <summary>Revoca la autorización sin eliminar el registro. Idempotente.</summary>
    public void Deactivate(Guid updatedBy)
    {
        if (!IsActive)
            return;

        IsActive = false;
        SetUpdated(updatedBy);
    }

    /// <summary>Restaura una autorización previamente revocada. Idempotente.</summary>
    public void Activate(Guid updatedBy)
    {
        if (IsActive)
            return;

        IsActive = true;
        SetUpdated(updatedBy);
    }
}
