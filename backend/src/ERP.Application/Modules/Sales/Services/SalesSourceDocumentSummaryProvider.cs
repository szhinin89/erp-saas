using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Domain.Modules.Sales.Interfaces;

namespace ERP.Application.Modules.Sales.Services;

/// <summary>Resuelve número de factura y nombre del cliente para el Monitor de Documentos Electrónicos — nunca datos comerciales completos.</summary>
public sealed class SalesSourceDocumentSummaryProvider : ISourceDocumentSummaryProvider
{
    private readonly ISalesInvoiceRepository _invoiceRepository;

    public SalesSourceDocumentSummaryProvider(ISalesInvoiceRepository invoiceRepository)
    {
        _invoiceRepository = invoiceRepository;
    }

    public string SourceModule => "Sales";

    public async Task<IReadOnlyDictionary<Guid, SourceDocumentSummary>> GetSummariesAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> sourceEntityIds,
        CancellationToken ct = default
    )
    {
        var summaries = await _invoiceRepository.GetSummariesByIdsAsync(
            tenantId,
            sourceEntityIds,
            ct
        );
        return summaries.ToDictionary(
            kv => kv.Key,
            kv => new SourceDocumentSummary(kv.Value.InvoiceNumber, kv.Value.CustomerName)
        );
    }
}
