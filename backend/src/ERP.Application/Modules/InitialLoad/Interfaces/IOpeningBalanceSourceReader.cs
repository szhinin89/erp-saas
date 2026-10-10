using ERP.Domain.Modules.InitialLoad.Enums;

namespace ERP.Application.Modules.InitialLoad.Interfaces;

/// <summary>
/// IL-7A — lo que un lote de saldos dejó CONFIRMADO en el dominio, base única del asiento de
/// apertura (nunca staging ni el Excel). <see cref="RawAmount"/> sin redondear (el costo de
/// inventario trae más de 2 decimales); <see cref="CutoffDates"/> son las fechas efectivas
/// registradas (Kardex <c>EffectiveDate</c>, CxP <c>AccountingDate</c>; la CxC no guarda corte);
/// <see cref="MissingCostCount"/> cuenta movimientos de inventario sin costo total.
/// </summary>
public sealed record OpeningBalanceSource(
    int DocumentCount,
    decimal RawAmount,
    IReadOnlyCollection<DateOnly> CutoffDates,
    int MissingCostCount
);

public interface IOpeningBalanceSourceReader
{
    /// <summary>
    /// Inventario: Σ <c>StockMovement.TotalCost</c> <c>InitialBalance</c> de los documentos de
    /// apertura creados por el lote. CxC: Σ <c>SalesReceivable.OriginalAmount</c> de origen
    /// <c>InitialBalance</c> del lote. CxP: Σ cuotas originales de las <c>AccountsPayable</c>
    /// <c>InitialBalance</c> del lote. Acotado a la empresa activa.
    /// </summary>
    Task<OpeningBalanceSource> GetConfirmedSourceAsync(
        Guid importBatchId,
        ImportType importType,
        CancellationToken ct
    );
}
