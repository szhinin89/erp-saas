using System.Globalization;
using System.Net;
using System.Net.Mail;
using ERP.Application.Modules.Communications.DTOs;
using ERP.Application.Modules.Communications.Services;
using ERP.Application.Modules.Ride.DTOs;
using ERP.Application.Modules.Ride.UseCases.GetOrGenerateRide;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Communications.Constants;
using ERP.Domain.Modules.Communications.Enums;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Events;
using ERP.Domain.Modules.ElectronicDocuments.Interfaces;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Domain.Modules.Sales.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.Communications.EventHandlers;

/// <summary>
/// Encola el correo transaccional de factura electrónica autorizada. El handler reacciona al
/// estado AUTORIZADO de ElectronicDocuments y delega el envío real al outbox de Communications.
/// Cualquier fallo en RIDE o Communications se absorbe y registra: la autorización SRI ya ocurrió
/// y no puede revertirse por un problema de correo.
/// </summary>
public sealed partial class SalesInvoiceAuthorizedCommunicationHandler
    : INotificationHandler<ElectronicDocumentAuthorizedEvent>
{
    private const string SalesSourceModule = "Sales";
    private const string SalesInvoiceCorrelationType = "SalesInvoice";

    private readonly IElectronicDocumentRepository _electronicDocuments;
    private readonly ISalesInvoiceRepository _salesInvoices;
    private readonly ICompanyRepository _companies;
    private readonly ICommunicationQueue _communicationQueue;
    private readonly ISender _sender;
    private readonly ILogger<SalesInvoiceAuthorizedCommunicationHandler> _logger;
    private readonly IOperationalPreferencesResolver _preferences;

    public SalesInvoiceAuthorizedCommunicationHandler(
        IElectronicDocumentRepository electronicDocuments,
        ISalesInvoiceRepository salesInvoices,
        ICompanyRepository companies,
        ICommunicationQueue communicationQueue,
        ISender sender,
        ILogger<SalesInvoiceAuthorizedCommunicationHandler> logger,
        IOperationalPreferencesResolver preferences
    )
    {
        _electronicDocuments = electronicDocuments;
        _salesInvoices = salesInvoices;
        _companies = companies;
        _communicationQueue = communicationQueue;
        _sender = sender;
        _logger = logger;
        _preferences = preferences;
    }

    public async Task Handle(ElectronicDocumentAuthorizedEvent e, CancellationToken ct)
    {
        if (e.DocumentType != ElectronicDocumentType.Invoice)
            return;

        try
        {
            await TryQueueAsync(e, ct);
        }
        catch (Exception ex)
        {
            LogQueueUnexpectedFailure(e.ElectronicDocumentId, ex);
        }
    }

    private async Task TryQueueAsync(ElectronicDocumentAuthorizedEvent e, CancellationToken ct)
    {
        var document = await _electronicDocuments.GetByIdAsync(
            e.TenantId!.Value,
            e.ElectronicDocumentId,
            ct
        );
        if (document is null)
        {
            LogElectronicDocumentMissing(e.ElectronicDocumentId);
            return;
        }

        if (
            document.CurrentState != ElectronicDocumentState.Authorized
            || document.DocumentType != ElectronicDocumentType.Invoice
            || !string.Equals(document.SourceModule, SalesSourceModule, StringComparison.Ordinal)
        )
            return;

        var invoice = await _salesInvoices.GetByIdAsync(
            document.TenantId,
            document.SourceEntityId,
            ct
        );
        if (invoice is null)
        {
            LogSalesInvoiceMissing(document.SourceEntityId, document.Id);
            return;
        }

        if (invoice.Status != SalesInvoiceStatus.Authorized)
            return;

        // CONFIG-DYNAMIC-OPERATIONS-01 (electronic_documents.email_on_authorization): resuelto con
        // el tenant/company EXPLÍCITOS del documento, no con ICurrentTenant/ICurrentCompany
        // ambiente — este handler reacciona a un evento de dominio, no a un request HTTP
        // autenticado, así que el contexto ambiente no es una fuente confiable de la empresa dueña
        // del documento.
        var preferences = await _preferences.ResolveAsync(document.TenantId, document.CompanyId, ct);
        if (!preferences.ElectronicDocuments.EmailOnAuthorization)
            return;

        var recipientEmail = NormalizeEmail(invoice.Customer.Email);
        if (recipientEmail is null)
        {
            LogSalesInvoiceWithoutCustomerEmail(invoice.Id, invoice.InvoiceNumber);
            return;
        }

        var company = await _companies.GetByIdAsync(document.CompanyId, ct);
        var issuerName = company?.TenantId == document.TenantId
            ? FirstNonEmpty(company.TradeName, company.LegalName) ?? "Empresa emisora"
            : "Empresa emisora";

        var attachments = new List<QueueCommunicationAttachmentDto>();
        AddAuthorizedXmlAttachment(attachments, invoice.InvoiceNumber, document.AuthorizedXmlPath);
        await AddRideAttachmentAsync(attachments, invoice.InvoiceNumber, invoice.Id, ct);

        var queued = await _communicationQueue.QueueEmailAsync(
            new QueueEmailRequest(
                Purpose: CommunicationPurposes.SalesInvoiceAuthorized,
                RecipientName: invoice.Customer.Name,
                RecipientEmail: recipientEmail,
                Subject: $"Factura autorizada {invoice.InvoiceNumber} - {issuerName}",
                BodyHtml: BuildBodyHtml(
                    invoice.InvoiceNumber,
                    document.AuthorizationNumber!.Value,
                    invoice.Customer.Name,
                    invoice.AuthorizedGrandTotal ?? invoice.GrandTotal,
                    issuerName
                ),
                BodyText: BuildBodyText(
                    invoice.InvoiceNumber,
                    document.AuthorizationNumber!.Value,
                    invoice.Customer.Name,
                    invoice.AuthorizedGrandTotal ?? invoice.GrandTotal,
                    issuerName
                ),
                Priority: CommunicationPriority.Normal,
                ScheduledAtUtc: DateTime.UtcNow,
                MaxRetries: null,
                CorrelationType: SalesInvoiceCorrelationType,
                CorrelationId: invoice.Id,
                IdempotencyKey: BuildIdempotencyKey(
                    document.TenantId,
                    document.CompanyId,
                    invoice.Id,
                    recipientEmail
                ),
                Attachments: attachments,
                BranchId: invoice.BranchId,
                SaveImmediately: false
            ),
            ct
        );

        LogSalesInvoiceEmailQueued(invoice.Id, invoice.InvoiceNumber, queued.Id, queued.WasAlreadyQueued);
    }

    private async Task AddRideAttachmentAsync(
        ICollection<QueueCommunicationAttachmentDto> attachments,
        string invoiceNumber,
        Guid invoiceId,
        CancellationToken ct
    )
    {
        try
        {
            var rideResult = await _sender.Send(
                new GetOrGenerateRideQuery(SalesSourceModule, invoiceId),
                ct
            );

            if (!rideResult.IsSuccess)
            {
                LogRideUnavailable(invoiceId, rideResult.Error);
                return;
            }

            var ride = rideResult.Value!;
            if (
                ride.Outcome is RideOutcome.Generated or RideOutcome.Cached
                && !string.IsNullOrWhiteSpace(ride.StoragePath)
            )
            {
                attachments.Add(
                    new QueueCommunicationAttachmentDto(
                        CommunicationAttachmentType.RidePdf,
                        $"{SafeFileToken(invoiceNumber)}-RIDE.pdf",
                        "application/pdf",
                        FileStoragePath: ride.StoragePath
                    )
                );
                return;
            }

            LogRideNotAttached(invoiceId, ride.Outcome, ride.ReasonCode);
        }
        catch (Exception ex)
        {
            LogRideGenerationThrew(invoiceId, ex);
        }
    }

    private void AddAuthorizedXmlAttachment(
        ICollection<QueueCommunicationAttachmentDto> attachments,
        string invoiceNumber,
        string? authorizedXmlPath
    )
    {
        if (string.IsNullOrWhiteSpace(authorizedXmlPath))
        {
            LogAuthorizedXmlMissing(invoiceNumber);
            return;
        }

        attachments.Add(
            new QueueCommunicationAttachmentDto(
                CommunicationAttachmentType.AuthorizedXml,
                $"{SafeFileToken(invoiceNumber)}-autorizado.xml",
                "application/xml",
                FileStoragePath: authorizedXmlPath
            )
        );
    }

    private static string BuildIdempotencyKey(
        Guid tenantId,
        Guid companyId,
        Guid invoiceId,
        string recipientEmail
    ) =>
        $"communications:{CommunicationPurposes.SalesInvoiceAuthorized}:{tenantId:N}:{companyId:N}:{invoiceId:N}:{recipientEmail.ToLowerInvariant()}";

    private static string BuildBodyHtml(
        string invoiceNumber,
        string accessKey,
        string customerName,
        decimal total,
        string issuerName
    ) =>
        "<p>Estimado/a "
        + Html(customerName)
        + ",</p>"
        + "<p>Su factura electronica fue autorizada por el SRI.</p>"
        + "<ul>"
        + "<li><strong>Factura:</strong> "
        + Html(invoiceNumber)
        + "</li>"
        + "<li><strong>Clave de acceso:</strong> "
        + Html(accessKey)
        + "</li>"
        + "<li><strong>Cliente:</strong> "
        + Html(customerName)
        + "</li>"
        + "<li><strong>Total:</strong> USD "
        + total.ToString("0.00", CultureInfo.InvariantCulture)
        + "</li>"
        + "<li><strong>Emisor:</strong> "
        + Html(issuerName)
        + "</li>"
        + "</ul>";

    private static string BuildBodyText(
        string invoiceNumber,
        string accessKey,
        string customerName,
        decimal total,
        string issuerName
    ) =>
        $"Estimado/a {customerName},\n\n"
        + "Su factura electronica fue autorizada por el SRI.\n"
        + $"Factura: {invoiceNumber}\n"
        + $"Clave de acceso: {accessKey}\n"
        + $"Cliente: {customerName}\n"
        + $"Total: USD {total.ToString("0.00", CultureInfo.InvariantCulture)}\n"
        + $"Emisor: {issuerName}\n";

    private static string? NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        var trimmed = email.Trim();
        try
        {
            var parsed = new MailAddress(trimmed);
            return string.Equals(parsed.Address, trimmed, StringComparison.OrdinalIgnoreCase)
                ? parsed.Address.ToLowerInvariant()
                : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static string Html(string value) => WebUtility.HtmlEncode(value);

    private static string SafeFileToken(string value) =>
        string.Join("-", value.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "No se encontro ElectronicDocument {ElectronicDocumentId} para encolar correo de factura autorizada."
    )]
    private partial void LogElectronicDocumentMissing(Guid electronicDocumentId);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "No se encontro SalesInvoice {InvoiceId} para ElectronicDocument autorizado {ElectronicDocumentId}."
    )]
    private partial void LogSalesInvoiceMissing(Guid invoiceId, Guid electronicDocumentId);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "SalesInvoice {InvoiceId} ({InvoiceNumber}) autorizada sin email de cliente; no se encola comunicacion."
    )]
    private partial void LogSalesInvoiceWithoutCustomerEmail(Guid invoiceId, string invoiceNumber);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Factura {InvoiceNumber}: XML autorizado no disponible; correo se encola sin adjunto XML."
    )]
    private partial void LogAuthorizedXmlMissing(string invoiceNumber);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "No se pudo obtener RIDE para SalesInvoice {InvoiceId}: {Reason}."
    )]
    private partial void LogRideUnavailable(Guid invoiceId, string? reason);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "RIDE no adjuntado para SalesInvoice {InvoiceId}; outcome {Outcome}, reason {ReasonCode}."
    )]
    private partial void LogRideNotAttached(Guid invoiceId, RideOutcome outcome, string? reasonCode);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Excepcion generando RIDE para SalesInvoice {InvoiceId}; se continua con el encolado de correo si aplica."
    )]
    private partial void LogRideGenerationThrew(Guid invoiceId, Exception ex);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Correo de factura autorizada encolado para SalesInvoice {InvoiceId} ({InvoiceNumber}) como CommunicationOutbox {CommunicationId}. Ya existia: {WasAlreadyQueued}."
    )]
    private partial void LogSalesInvoiceEmailQueued(
        Guid invoiceId,
        string invoiceNumber,
        Guid communicationId,
        bool wasAlreadyQueued
    );

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "No se pudo encolar correo para ElectronicDocument autorizado {ElectronicDocumentId}; la autorizacion SRI se conserva."
    )]
    private partial void LogQueueUnexpectedFailure(Guid electronicDocumentId, Exception ex);
}
