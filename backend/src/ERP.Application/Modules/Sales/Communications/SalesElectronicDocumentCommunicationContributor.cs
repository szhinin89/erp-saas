using ERP.Application.Common;
using ERP.Application.Modules.Communications.ElectronicDocuments;
using ERP.Application.Modules.Communications.Templates;
using ERP.Domain.Modules.Communications.Constants;
using ERP.Domain.Modules.Communications.Enums;
using ERP.Domain.Modules.Communications.ValueObjects;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Domain.Modules.Sales.Interfaces;
using System.Globalization;

namespace ERP.Application.Modules.Sales.Communications;

/// <summary>
/// ZH-EDOC-COMMUNICATIONS-01 — Ventas aporta a la comunicación de SUS comprobantes autorizados:
/// <list type="bullet">
/// <item>Factura (<c>SalesInvoice</c>) → SALES_INVOICE_AUTHORIZED al cliente (snapshot de la factura),
/// mismos datos que el correo previo (golden).</item>
/// <item>Nota de crédito (<c>SalesReturn</c>) → SALES_CREDIT_NOTE_AUTHORIZED al cliente de la factura
/// modificada (el mismo snapshot que va en el XML de la nota de crédito).</item>
/// </list>
/// Solo datos: ni asunto, ni HTML, ni adjuntos, ni outbox.
/// </summary>
public sealed class SalesElectronicDocumentCommunicationContributor : IElectronicDocumentCommunicationContributor
{
    public const string SalesSourceModule = "Sales";
    public const string SalesInvoiceSourceType = "SalesInvoice";
    public const string SalesReturnSourceType = "SalesReturn";

    private static readonly IReadOnlyDictionary<ElectronicDocumentType, ElectronicDocumentCommunicationTarget> Routes =
        new Dictionary<ElectronicDocumentType, ElectronicDocumentCommunicationTarget>
        {
            [ElectronicDocumentType.Invoice] = new(CommunicationPurposes.SalesInvoiceAuthorized, SalesInvoiceSourceType),
            [ElectronicDocumentType.CreditNote] = new(CommunicationPurposes.SalesCreditNoteAuthorized, SalesReturnSourceType),
        };

    private readonly ISalesInvoiceRepository _invoices;
    private readonly ISalesReturnRepository _returns;

    public SalesElectronicDocumentCommunicationContributor(ISalesInvoiceRepository invoices, ISalesReturnRepository returns)
    {
        _invoices = invoices;
        _returns = returns;
    }

    public string SourceModule => SalesSourceModule;

    public IReadOnlyDictionary<ElectronicDocumentType, ElectronicDocumentCommunicationTarget> Targets => Routes;

    public Task<Result<ElectronicDocumentCommunicationContribution>> ContributeAsync(
        ElectronicDocumentCommunicationContext context,
        CancellationToken ct = default
    ) =>
        context.Document.DocumentType == ElectronicDocumentType.CreditNote
            ? ContributeCreditNoteAsync(context, ct)
            : ContributeInvoiceAsync(context, ct);

    private async Task<Result<ElectronicDocumentCommunicationContribution>> ContributeInvoiceAsync(
        ElectronicDocumentCommunicationContext context,
        CancellationToken ct
    )
    {
        var document = context.Document;
        var invoice = await _invoices.GetByIdAsync(document.TenantId, document.SourceEntityId, ct);
        if (invoice is null || invoice.CompanyId != document.CompanyId)
            return NotFound();
        if (invoice.Status != SalesInvoiceStatus.Authorized)
            return NotEligible();

        return Result<ElectronicDocumentCommunicationContribution>.Success(
            new ElectronicDocumentCommunicationContribution(
                CommunicationPurposes.SalesInvoiceAuthorized,
                new CommunicationSource(SalesSourceModule, SalesInvoiceSourceType, invoice.Id),
                CommunicationRecipientRole.Customer,
                invoice.Customer.Name,
                invoice.Customer.Email,
                invoice.BranchId,
                invoice.InvoiceNumber,
                new SalesInvoiceAuthorizedTemplateModel(
                    CustomerName: invoice.Customer.Name,
                    InvoiceNumber: invoice.InvoiceNumber,
                    AccessKey: document.AuthorizationNumber!.Value,
                    Total: Money(invoice.AuthorizedGrandTotal ?? invoice.GrandTotal),
                    IssuerName: context.IssuerName
                )
            )
        );
    }

    private async Task<Result<ElectronicDocumentCommunicationContribution>> ContributeCreditNoteAsync(
        ElectronicDocumentCommunicationContext context,
        CancellationToken ct
    )
    {
        var document = context.Document;
        var salesReturn = await _returns.GetByIdAsync(document.TenantId, document.SourceEntityId, ct);
        if (salesReturn is null || salesReturn.CompanyId != document.CompanyId)
            return NotFound();
        if (salesReturn.Status != SalesReturnStatus.Authorized || string.IsNullOrWhiteSpace(salesReturn.CreditNoteDocumentNumber))
            return NotEligible();

        // Destinatario = cliente de la factura modificada (mismo snapshot que el XML de la NC).
        var invoice = await _invoices.GetByIdAsync(document.TenantId, salesReturn.SalesInvoiceId, ct);
        if (invoice is null || invoice.CompanyId != document.CompanyId)
            return NotFound();

        return Result<ElectronicDocumentCommunicationContribution>.Success(
            new ElectronicDocumentCommunicationContribution(
                CommunicationPurposes.SalesCreditNoteAuthorized,
                new CommunicationSource(SalesSourceModule, SalesReturnSourceType, salesReturn.Id),
                CommunicationRecipientRole.Customer,
                invoice.Customer.Name,
                invoice.Customer.Email,
                invoice.BranchId,
                salesReturn.CreditNoteDocumentNumber,
                new SalesCreditNoteAuthorizedTemplateModel(
                    CustomerName: invoice.Customer.Name,
                    CreditNoteNumber: salesReturn.CreditNoteDocumentNumber,
                    ModifiedInvoiceNumber: invoice.InvoiceNumber,
                    AccessKey: document.AuthorizationNumber!.Value,
                    Total: Money(salesReturn.GrandTotal),
                    IssuerName: context.IssuerName
                )
            )
        );
    }

    private static string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    private static Result<ElectronicDocumentCommunicationContribution> NotFound() =>
        Result<ElectronicDocumentCommunicationContribution>.Failure(
            "El documento de origen del comprobante no existe en la empresa.",
            ApiResponseCodes.Communications.SourceNotFound
        );

    private static Result<ElectronicDocumentCommunicationContribution> NotEligible() =>
        Result<ElectronicDocumentCommunicationContribution>.Failure(
            "El documento de origen no está autorizado.",
            ApiResponseCodes.Communications.SourceNotEligible
        );
}
