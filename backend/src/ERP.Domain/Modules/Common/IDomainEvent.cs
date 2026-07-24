using MediatR;

namespace ERP.Domain.Common;

/// <summary>
/// Evento de dominio publicado vía MediatR tras persistir el agregado en el DbContext.
/// </summary>
public interface IDomainEvent : INotification
{
    Guid Id { get; }
    DateTime OccurredOn { get; }
}
