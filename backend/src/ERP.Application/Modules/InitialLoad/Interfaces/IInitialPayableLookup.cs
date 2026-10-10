namespace ERP.Application.Modules.InitialLoad.Interfaces;

/// <summary>
/// IL-6A — lectura de solo consulta para la Carga Inicial de CxP. Company scope por filtros
/// globales fail-closed.
/// </summary>
public interface IInitialPayableLookup
{
    /// <summary>
    /// Números de documento de todas las CxP existentes del proveedor en la empresa, de cualquier
    /// origen (Compra, Gasto, saldo inicial) y estado. El processor normaliza todos antes de comparar.
    /// </summary>
    Task<IReadOnlyList<string>> GetDocumentNumbersAsync(Guid supplierId, CancellationToken ct);
}
