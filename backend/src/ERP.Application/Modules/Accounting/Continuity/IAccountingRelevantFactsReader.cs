namespace ERP.Application.Modules.Accounting.Continuity;

/// <summary>
/// ACCOUNTING-CONTINUITY-05 — hechos operativos contablemente relevantes ya registrados. Una vigencia
/// Enabled pasada solo es válida si ninguno quedaría sujeto retroactivamente (accounting.md §20).
/// </summary>
public interface IAccountingRelevantFactsReader
{
    /// <summary>Existe algún hecho no borrador con fecha de negocio igual o posterior a <paramref name="date"/>.</summary>
    Task<bool> ExistsOnOrAfterAsync(Guid tenantId, Guid companyId, DateOnly date, CancellationToken ct);

    /// <summary>
    /// ACCOUNTING-CONTINUITY-06 — orígenes forward que deben registrar obligación (venta y su devolución,
    /// compra, devolución y NC Discount de compra, aplicación y reembolso de saldo a favor, gasto, retención
    /// emitida, pago a proveedor y cobro), en los estados que la generan y con fecha contable en
    /// [<paramref name="from"/>, <paramref name="to"/>], sin <c>AccountingPosting</c> ni <c>JournalEntry</c>
    /// de su identidad (módulo + origen). El llamador acota el rango a las ventanas sujetas a Accounting.
    /// </summary>
    Task<int> CountWithoutAccountingAsync(Guid tenantId, Guid companyId, DateOnly from, DateOnly to, CancellationToken ct);
}
