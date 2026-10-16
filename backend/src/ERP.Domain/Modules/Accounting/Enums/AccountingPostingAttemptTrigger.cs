namespace ERP.Domain.Modules.Accounting.Enums;

/// <summary>
/// ACCOUNTING-CONTINUITY-01 — origen de un intento de contabilización. <see cref="Initial"/> es el
/// único intento del sistema tras el commit operativo (no es un reintento); <see cref="Manual"/>
/// es el reproceso solicitado por un usuario autorizado. No existe reintento automático.
/// </summary>
public enum AccountingPostingAttemptTrigger
{
    Initial = 1,
    Manual = 2,
}
