using ERP.Domain.Audit;

namespace ERP.Application.Audit;

/// <summary>
/// Persistencia de una entidad de auditoría de dominio. Una única implementación
/// genérica (Infrastructure) sirve a todos los módulos — ningún dominio implementa esto
/// por su cuenta.
/// </summary>
public interface IAuditWriter<TAudit>
    where TAudit : AuditRecordBase
{
    /// <summary>
    /// Registra el registro en el <c>ChangeTracker</c> del contexto de persistencia activo.
    /// No hace commit por sí mismo: se persiste con el <c>SaveChangesAsync</c> ambiente
    /// (típicamente el mismo que dispara el domain event que originó este registro), para
    /// que la auditoría quede en la misma transacción que el cambio de negocio.
    /// </summary>
    Task RecordAsync(TAudit record, CancellationToken ct = default);
}
