using ERP.Application.Modules.Accounting.Continuity;
using ERP.Application.Modules.Companies;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Sales.Interfaces;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// ACCOUNTING-CONTINUITY-05 — registra la obligación de costo de ventas (reconocimiento, cobertura o
/// devolución) en la transacción operativa. La aplicabilidad se ancla a la fecha de emisión de la
/// factura (BusinessDate): si la venta no estaba sujeta a Accounting, sus ajustes posteriores
/// tampoco; si lo estaba, siguen siendo obligación. La fecha contable es la del hecho de costo.
/// Todas las obligaciones de costo de una factura comparten <c>SourceDocumentId</c> = factura.
/// Importes redondeados con la precisión contable de la empresa (SSOT de precisión).
/// </summary>
public sealed class SaleCostPostingRecorder(
    AccountingPostingRecorder recorder,
    ISalesInvoiceRepository invoices,
    ICompanyPrecisionPolicyProvider precision)
{
    public const string SourceModule = "Sales";
    public const string CostFactType = "CostOfGoodsSold";
    public const string CostReversedFactType = "CostOfGoodsSoldReversed";

    public static bool IsCostFact(string sourceModule, string factType) =>
        sourceModule == SourceModule && factType is CostFactType or CostReversedFactType;

    /// <param name="amount">Delta con signo: positivo aumenta el costo de ventas.</param>
    public async Task RecordAsync(Guid tenantId, Guid companyId, Guid invoiceId, string? invoiceNumber,
        DateOnly issueDate, Guid sourceEventId, decimal amount, DateOnly entryDate, CancellationToken ct,
        decimal? recognizedCostTarget = null)
    {
        var decimals = (await precision.GetEffectiveAsync(ct)).AccountingDecimals;
        var rounded = Math.Round(Math.Abs(amount), decimals, MidpointRounding.AwayFromZero);
        if (rounded == 0m)
            return; // Sin efecto económico: no hay asiento que exigir.

        await recorder.RecordAsync(
            new AccountingPostingRequest(
                tenantId,
                companyId,
                SourceModule,
                amount < 0m ? CostReversedFactType : CostFactType,
                sourceEventId,
                invoiceNumber,
                issueDate,
                entryDate,
                new AccountingPostingPayload(0m, 0m, 0m, 0m, 0m,
                    HistoricalCostTotal: rounded,
                    References: new Dictionary<string, Guid> { ["SalesInvoice"] = invoiceId },
                    RecognizedCostTarget: recognizedCostTarget),
                invoiceId
            ),
            ct
        );
    }

    /// <summary>Igual que <see cref="RecordAsync"/>, resolviendo número y emisión de la factura.</summary>
    public async Task RecordForInvoiceAsync(Guid tenantId, Guid companyId, Guid invoiceId,
        Guid sourceEventId, decimal amount, DateOnly entryDate, CancellationToken ct,
        decimal? recognizedCostTarget = null)
    {
        var decimals = (await precision.GetEffectiveAsync(ct)).AccountingDecimals;
        if (Math.Round(amount, decimals, MidpointRounding.AwayFromZero) == 0m)
            return;
        var invoice = await invoices.GetByIdAsync(tenantId, invoiceId, ct);
        if (invoice is null || invoice.CompanyId != companyId)
            throw new DomainRuleViolationException("No se encontró la factura de venta del costo reconocido.");
        await RecordAsync(tenantId, companyId, invoiceId, invoice.InvoiceNumber, invoice.IssueDate,
            sourceEventId, amount, entryDate, ct, recognizedCostTarget);
    }
}
