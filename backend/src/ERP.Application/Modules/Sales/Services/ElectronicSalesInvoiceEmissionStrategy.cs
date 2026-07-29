using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.Sales.Services;

/// <summary>
/// Punto de Emisión Electrónico: dispara el pipeline electrónico completo (Provider→XML→XSD→
/// Firma→Storage→Recepción→Autorización) vía la infraestructura FROZEN de ElectronicDocuments
/// (ADR-023) — nunca reimplementada aquí, solo invocada.
/// </summary>
public sealed class ElectronicSalesInvoiceEmissionStrategy : ISalesInvoiceEmissionStrategy
{
    private readonly IElectronicDocumentIssuer _edocIssuer;
    private readonly ILogger<ElectronicSalesInvoiceEmissionStrategy> _logger;

    public ElectronicSalesInvoiceEmissionStrategy(
        IElectronicDocumentIssuer edocIssuer, ILogger<ElectronicSalesInvoiceEmissionStrategy> logger)
    {
        _edocIssuer = edocIssuer;
        _logger = logger;
    }

    public EmissionType SupportedType => EmissionType.Electronic;

    public async Task<string?> ExecuteAsync(SalesInvoiceEmissionContext context, CancellationToken ct)
    {
        var inv = context.Invoice;
        try
        {
            var issueResult = await _edocIssuer.RegisterAsync(
                new RegisterElectronicDocumentRequest(
                    context.TenantId, context.CompanyId, ElectronicDocumentType.Invoice,
                    "Sales", inv.Id, context.UserId),
                ct);
            if (!issueResult.IsSuccess)
            {
                _logger.LogWarning(
                    "Electronic document issuance failed for sales invoice {InvoiceNumber} ({InvoiceId}): {Reason}",
                    inv.InvoiceNumber, inv.Id, issueResult.Error);
                return issueResult.Error;
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error issuing electronic document for sales invoice {InvoiceNumber} ({InvoiceId})",
                inv.InvoiceNumber, inv.Id);
            return "Error inesperado al generar el documento electrónico. Revise el Monitor de Documentos Electrónicos.";
        }
    }
}
