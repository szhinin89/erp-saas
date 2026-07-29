using ERP.Domain.Audit;

namespace ERP.Application.Audit;

/// <summary>
/// Facade de auditoría: un único punto de inyección para los event handlers de dominio.
/// Delega en <see cref="IAuditWriter{TAudit}"/> resuelto por tipo — evita que cada handler
/// de cada módulo tenga que inyectar el writer genérico explícitamente.
/// </summary>
public interface IAuditService
{
    Task RecordAsync<TAudit>(TAudit record, CancellationToken ct = default)
        where TAudit : AuditRecordBase;
}
