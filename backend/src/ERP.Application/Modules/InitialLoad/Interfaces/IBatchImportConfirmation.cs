namespace ERP.Application.Modules.InitialLoad.Interfaces;

/// <summary>
/// Confirmación de un lote completo en una sola llamada (IL-4B Inventario Inicial): el processor
/// recibe todas las filas válidas y escribe documentos agrupados (p. ej. uno por bodega) dentro de
/// la transacción del handler. Si devuelve fallo — o lanza — el handler revierte todo.
/// </summary>
public interface IBatchImportConfirmation
{
    Task<BatchConfirmResult> ConfirmBatchAsync(
        IReadOnlyList<(int RowNumber, string ParsedDataJson)> rows,
        CancellationToken ct
    );
}

/// <summary>Id del documento creado por número de fila, o el motivo del rechazo.</summary>
public sealed record BatchConfirmResult(IReadOnlyDictionary<int, Guid>? CreatedIdsByRow, string? Error)
{
    public bool IsSuccess => Error is null;

    public static BatchConfirmResult Success(IReadOnlyDictionary<int, Guid> createdIdsByRow) =>
        new(createdIdsByRow, null);

    public static BatchConfirmResult Failed(string error) => new(null, error);
}
