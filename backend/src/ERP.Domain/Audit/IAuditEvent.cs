namespace ERP.Domain.Audit;

/// <summary>
/// Marca opcional para domain events que representan una transición auditable.
/// Documenta qué entidad y qué acción de negocio ocurrió — la traducción a una entidad
/// de auditoría específica del dominio la hace el event handler de Application
/// correspondiente, nunca este contrato.
/// </summary>
public interface IAuditEvent
{
    Guid EntityId { get; }
    string Action { get; }
    string? Reason { get; }
}
