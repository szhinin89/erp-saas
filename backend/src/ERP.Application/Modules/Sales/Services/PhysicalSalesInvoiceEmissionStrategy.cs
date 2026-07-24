using Microsoft.Extensions.Logging;
using ERP.Domain.Modules.Company.Enums;

namespace ERP.Application.Modules.Sales.Services;

/// <summary>
/// Punto de Emisión Físico: no ejecuta ningún componente de ElectronicDocuments — sin XML, sin
/// firma, sin clave de acceso, sin autorización/consulta SRI, sin RIDE electrónico. Por eso esta
/// clase deliberadamente no depende de <c>IElectronicDocumentIssuer</c> ni de
/// <c>IElectronicDocumentRepository</c>: la ausencia de esas dependencias es la garantía
/// estructural de que la factura física nunca toca esa infraestructura.
/// </summary>
public sealed class PhysicalSalesInvoiceEmissionStrategy : ISalesInvoiceEmissionStrategy
{
    private readonly ILogger<PhysicalSalesInvoiceEmissionStrategy> _logger;

    public PhysicalSalesInvoiceEmissionStrategy(ILogger<PhysicalSalesInvoiceEmissionStrategy> logger)
        => _logger = logger;

    public EmissionType SupportedType => EmissionType.Physical;

    public Task<string?> ExecuteAsync(SalesInvoiceEmissionContext context, CancellationToken ct)
    {
        var inv = context.Invoice;
        _logger.LogInformation(
            "Sales invoice {InvoiceNumber} ({InvoiceId}) authorized on a Physical emission point — no electronic document generated.",
            inv.InvoiceNumber, inv.Id);
        return Task.FromResult<string?>(null);
    }
}
