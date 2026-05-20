namespace ERP.Domain.Common;

/// <summary>
/// Clase base para entidades maestras de administración.
/// Ejemplos: Account, Product, Customer, User, etc.
///
/// Hereda: BaseEntity (Id, SubscriberId)
///       → AggregateRoot (DomainEvents)
///       → AuditableEntity (CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
///
/// REGLA: Nunca se eliminan físicamente.
/// Solo se deshabilitan con Disable() o se reactivan con Enable().
/// </summary>
public abstract class MasterEntity : AuditableEntity
{
    public bool IsActive { get; protected set; } = true;

    /// <summary>
    /// Deshabilita la entidad. No la elimina.
    /// </summary>
    public void Disable(Guid updatedBy)
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
