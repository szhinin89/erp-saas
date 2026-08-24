namespace ERP.Domain.Modules.InitialLoad.Enums;

/// <summary>
/// Máquina de estados de un <c>ImportBatch</c>:
/// <c>Draft → Uploaded → Validating → Validated → Confirming → Completed | PartiallyCompleted</c>.
/// <see cref="Failed"/> solo se alcanza desde <see cref="Validating"/>/<see cref="Confirming"/> ante
/// una excepción inesperada — filas con errores de negocio no fallan el batch, quedan reflejadas
/// en los contadores de <c>Validated</c>. <see cref="Cancelled"/> solo es alcanzable desde
/// <see cref="Draft"/>/<see cref="Uploaded"/>/<see cref="Validated"/> — un batch en confirmación o
/// ya confirmado no se cancela.
/// </summary>
public enum ImportStatus
{
    Draft = 1,
    Uploaded = 2,
    Validating = 3,
    Validated = 4,
    Confirming = 5,
    Completed = 6,
    PartiallyCompleted = 7,
    Failed = 8,
    Cancelled = 9,
}
