namespace ERP.Application.Modules.InitialLoad.Interfaces;

/// <summary>
/// IL-5A — lectura de solo consulta para la Carga Inicial de CxC. Company scope por filtros
/// globales fail-closed.
/// </summary>
public interface IInitialReceivableLookup
{
    /// <summary>
    /// Números de documento de todas las CxC existentes del cliente en la empresa, de cualquier
    /// origen y estado: número de la factura (Invoice) o número normalizado persistido
    /// (InitialBalance). El processor normaliza todos antes de comparar (idempotente).
    /// </summary>
    Task<IReadOnlyList<string>> GetDocumentNumbersAsync(Guid customerId, CancellationToken ct);

    /// <summary>
    /// <c>Company.OpeningBalanceDate</c> de la empresa operativa: único corte de apertura de saldos
    /// (SSOT). Null si la empresa aún no lo definió.
    /// </summary>
    Task<DateOnly?> GetOpeningBalanceDateAsync(CancellationToken ct);
}
