namespace ERP.Domain.Audit;

/// <summary>Origen de un registro de auditoría — común a todos los dominios.</summary>
public enum AuditSource
{
    UserAction = 0,
    System = 1,
    Integration = 2,
    Migration = 3,
}
