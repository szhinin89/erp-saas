namespace ERP.Domain.Common;

/// <summary>
/// Clase base para entidades maestras de administración.
/// Ejemplos: Account, Product, Customer, User, etc.
///
/// Hereda: BaseEntity (Id, tenantId)
///       → AggregateRoot (DomainEvents)
///       → AuditableEntity (CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
///
/// REGLA: Nunca se eliminan físicamente.
/// Solo se deshabilitan con Disable() o se reactivan con Enable().
/// </summary>
public abstract class MasterEntity : AuditableEntity, ISystemSeeded
{
    public bool IsActive { get; protected set; } = true;

    /// <summary>
    /// Ver <see cref="ISystemSeeded"/>. Solo se activa mediante <see cref="MarkAsSystemSeeded"/>,
    /// invocado exclusivamente por la fábrica <c>CreateSystemSeeded(...)</c> que cada entidad protegida
    /// expone además de su <c>Create(...)</c> normal — nunca por un flujo de usuario.
    /// </summary>
    public bool IsSystemSeeded { get; private set; }

    /// <summary>
    /// Marca la entidad como sembrada por el sistema. Solo debe invocarse desde la fábrica
    /// <c>CreateSystemSeeded(...)</c> de la entidad concreta, inmediatamente después de <c>Create(...)</c>.
    /// </summary>
    protected void MarkAsSystemSeeded() => IsSystemSeeded = true;

    /// <summary>
    /// Deshabilita la entidad. No la elimina. <c>virtual</c> para permitir que entidades con
    /// registros protegidos del sistema (Tipo A o Tipo C — ver política de Bootstrap en
    /// <c>CLAUDE.md</c>) rechacen la deshabilitación llamando a
    /// <see cref="SystemSeedGuard.EnsureEditable"/> antes de invocar <c>base.Disable(...)</c>.
    /// </summary>
    public virtual void Disable(Guid updatedBy)
    {
        if (!IsActive)
            throw new InvalidOperationException("El registro ya está deshabilitado.");

        IsActive = false;
        SetUpdated(updatedBy);
    }

    /// <summary>
    /// Reactiva una entidad previamente deshabilitada.
    /// </summary>
    public void Enable(Guid updatedBy)
    {
        if (IsActive)
            throw new InvalidOperationException("El registro ya está activo.");

        IsActive = true;
        SetUpdated(updatedBy);
    }
}
