namespace ERP.Domain.Audit;

/// <summary>
/// Quién/cuándo/desde-dónde de una acción auditable. Único lugar donde vive la información
/// del actor — value object reutilizado por todas las entidades de auditoría de dominio para
/// evitar repetir la misma lista de parámetros primitivos en cada factory <c>Create(...)</c>.
///
/// <c>UserName</c> es el snapshot obligatorio ya resuelto (nunca vacío para un actor
/// autenticado — ver <c>AuditRecordBase.SetCommon</c>, que además aplica un fallback
/// defensivo). <c>FullName</c>/<c>Email</c>/<c>RoleName</c> son detalle opcional adicional:
/// hoy ya se pueblan desde <c>HttpAuditContext</c> porque el dato está disponible sin costo
/// extra, pero ningún consumidor debe asumir que están presentes — están pensados para que
/// auditorías futuras (ej. mostrar "Juan Pérez (Administrador)" o filtrar por rol) no
/// requieran modificar de nuevo este contrato.
/// </summary>
public readonly record struct AuditActor(
    Guid TenantId,
    Guid UserId,
    string UserName,
    string? FullName,
    string? Email,
    string? RoleName,
    string? CorrelationId,
    string? RequestId,
    AuditSource Source
);
