namespace ERP.Domain.Common;

/// <summary>
/// Marca un IDomainEvent como candidato a exportación futura hacia
/// Platform (vía Outbox + bus de eventos, aún no implementado).
/// </summary>
public interface IIntegrationEvent : IDomainEvent { }
