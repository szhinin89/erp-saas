namespace ERP.Domain.Common;

/// <summary>
/// Agregado que acumula eventos de dominio hasta el commit del contexto.
/// </summary>
public interface IHasDomainEvents
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    void ClearDomainEvents();

    void AddDomainEvent(IDomainEvent eventItem);
}
