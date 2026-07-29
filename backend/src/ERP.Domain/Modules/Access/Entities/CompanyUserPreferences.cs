using ERP.Domain.Access.Enums;
using ERP.Domain.Common;

namespace ERP.Domain.Access.Entities;

/// <summary>
/// Preferencias operativas de un <see cref="CompanyUserMembership"/> dentro de una empresa.
/// Responde exclusivamente "¿cómo debe iniciar sesión ese usuario en esa empresa?" — nunca
/// "¿a qué sucursales puede ingresar?", que es responsabilidad exclusiva de
/// <see cref="CompanyUserBranch"/>. Esta entidad no autoriza acceso a ninguna sucursal; solo
/// describe una preferencia de arranque de sesión. La verificación de que
/// <see cref="DefaultBranchId"/> sea una sucursal real y autorizada para la membresía (vía
/// <see cref="CompanyUserBranch"/>) requiere acceso a datos y es responsabilidad del handler de
/// aplicación que orquesta la creación/actualización de esta entidad (fuera de alcance de Domain).
/// </summary>
public sealed class CompanyUserPreferences
    : AuditableEntity,
        ITenantScopedEntity,
        ICompanyOperationalEntity
{
    public Guid CompanyId { get; private set; }
    public Guid CompanyUserMembershipId { get; private set; }
    public Guid? DefaultBranchId { get; private set; }
    public CompanyUserLoginMode LoginMode { get; private set; }

    private CompanyUserPreferences() { }

    public static CompanyUserPreferences Create(
        Guid tenantId,
        Guid companyId,
        Guid companyUserMembershipId,
        CompanyUserLoginMode loginMode,
        Guid? defaultBranchId,
        Guid createdBy
    )
    {
        if (companyId == Guid.Empty)
            throw new ArgumentException("La empresa es obligatoria.", nameof(companyId));
        if (companyUserMembershipId == Guid.Empty)
            throw new ArgumentException(
                "La membresía es obligatoria.",
                nameof(companyUserMembershipId)
            );
        if (defaultBranchId == Guid.Empty)
            throw new ArgumentException(
                "La sucursal por defecto no puede ser un Guid vacío.",
                nameof(defaultBranchId)
            );
        if (loginMode == CompanyUserLoginMode.DirectToDefault && defaultBranchId is null)
            throw new ArgumentException(
                "LoginMode.DirectToDefault requiere una sucursal por defecto.",
                nameof(defaultBranchId)
            );

        var entity = new CompanyUserPreferences
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            CompanyUserMembershipId = companyUserMembershipId,
            LoginMode = loginMode,
            DefaultBranchId = defaultBranchId,
        };
        entity.SetCreated(createdBy);

        return entity;
    }

    /// <summary>Cambia la sucursal por defecto. Idempotente si ya era la misma.</summary>
    public void ChangeDefaultBranch(Guid? defaultBranchId, Guid updatedBy)
    {
        if (defaultBranchId == Guid.Empty)
            throw new ArgumentException(
                "La sucursal por defecto no puede ser un Guid vacío.",
                nameof(defaultBranchId)
            );
        if (LoginMode == CompanyUserLoginMode.DirectToDefault && defaultBranchId is null)
            throw new ArgumentException(
                "No se puede quitar la sucursal por defecto mientras LoginMode sea DirectToDefault.",
                nameof(defaultBranchId)
            );

        if (DefaultBranchId == defaultBranchId)
            return;

        DefaultBranchId = defaultBranchId;
        SetUpdated(updatedBy);
    }

    /// <summary>Cambia el modo de inicio de sesión. Idempotente si ya era el mismo.</summary>
    public void ChangeLoginMode(CompanyUserLoginMode loginMode, Guid updatedBy)
    {
        if (loginMode == CompanyUserLoginMode.DirectToDefault && DefaultBranchId is null)
            throw new ArgumentException(
                "No se puede activar DirectToDefault sin una sucursal por defecto ya asignada.",
                nameof(loginMode)
            );

        if (LoginMode == loginMode)
            return;

        LoginMode = loginMode;
        SetUpdated(updatedBy);
    }
}
