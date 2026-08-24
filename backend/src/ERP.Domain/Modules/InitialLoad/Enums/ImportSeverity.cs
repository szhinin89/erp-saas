namespace ERP.Domain.Modules.InitialLoad.Enums;

/// <summary>
/// Severidad de un <c>ImportBatchIssue</c>. <see cref="Error"/> bloquea la fila (nunca se
/// confirma); <see cref="Warning"/> no bloquea — la fila se confirma igual y el warning queda
/// como advertencia informativa para revisión posterior.
/// </summary>
public enum ImportSeverity
{
    Error = 1,
    Warning = 2,
}
