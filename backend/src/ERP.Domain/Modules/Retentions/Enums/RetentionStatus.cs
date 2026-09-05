namespace ERP.Domain.Modules.Retentions.Enums;

/// <summary>
/// Estado del ciclo de vida de un <c>RetentionDocument</c>. Sigue el mismo patrón de 3 estados
/// (Draft → Issued → Cancelled) ya usado por <c>ExpenseStatus</c> — <c>Cancelled</c> es terminal,
/// no existe transición de regreso.
/// </summary>
public enum RetentionStatus
{
    Draft = 0,
    Issued = 1,
    Cancelled = 2,
}
