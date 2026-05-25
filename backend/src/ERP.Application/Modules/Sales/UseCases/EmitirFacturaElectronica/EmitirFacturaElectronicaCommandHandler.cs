using System.Text;
using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.Fiscal.Integration;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Fiscal.Entities;
using ERP.Domain.Modules.Fiscal.Interfaces;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Sales.UseCases.EmitirFacturaElectronica;

public sealed class IssueElectronicInvoiceCommandHandler
    : IRequestHandler<IssueElectronicInvoiceCommand, Result<Guid>>
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly ISriSettingsRepository _configSriRepository;
    private readonly ISriFacturaElectronicaService _sriService;
    private readonly IFileStorage _fileStorage;
    private readonly IAccountingService _accounting;
    private readonly IProductRepository _productRepository;
    private readonly IUserActivityRepository _activity;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISecretProtector _secretProtector;
    private readonly ICurrentSubscriber _currentSubscriber;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<IssueElectronicInvoiceCommandHandler> _logger;

    public IssueElectronicInvoiceCommandHandler(
        IInvoiceRepository invoiceRepository,
        ISriSettingsRepository configSriRepository,
        ISriFacturaElectronicaService sriService,
        IFileStorage fileStorage,
        IAccountingService accounting,
        IProductRepository productRepository,
        IUserActivityRepository activity,
        IUnitOfWork unitOfWork,
        ISecretProtector secretProtector,
        ICurrentSubscriber currentSubscriber,
        ICurrentUser currentUser,
        ILogger<IssueElectronicInvoiceCommandHandler> logger)
    {
        _invoiceRepository = invoiceRepository;
        _configSriRepository = configSriRepository;
        _sriService = sriService;
        _fileStorage = fileStorage;
        _accounting = accounting;
        _productRepository = productRepository;
        _activity = activity;
        _unitOfWork = unitOfWork;
        _secretProtector = secretProtector;
        _currentSubscriber = currentSubscriber;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(IssueElectronicInvoiceCommand command, CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        var userId = _currentUser.UserId;

        var loadResult = await LoadInvoiceForIssueAsync(subscriberId, command.VentaId, ct);
        if (!loadResult.IsSuccess)
            return Result<Guid>.Failure(loadResult.Error!);

        var (invoice, configSri) = loadResult.Value;
        var legacyBill = FiscalInvoiceSriBridge.ToLegacyBill(invoice);
        var legacyLines = FiscalInvoiceSriBridge.ToLegacyLines(invoice, userId);

        var xmlResult = await GenerateAndSignXmlAsync(invoice, legacyBill, legacyLines, configSri, userId, ct);
        if (!xmlResult.IsSuccess)
            return Result<Guid>.Failure(xmlResult.Error!);

        var sendResult = await SendToSriAsync(invoice, legacyBill, xmlResult.Value.XmlFirmado, configSri, userId, ct);
        if (!sendResult.IsSuccess)
            return Result<Guid>.Failure(sendResult.Error!);

        var response = sendResult.Value.Response;
        if (!response.IsAuthorized)
        {
            var mensajeError = response.ErrorMessage ?? "SRI rechazó la factura sin indicar motivo.";
            _logger.LogWarning("SRI rechazó factura {FacturaId}: {Error}", invoice.PublicId, mensajeError);
            invoice.Reject(userId, mensajeError);
            invoice.Electronic?.MarkRejected(mensajeError);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result<Guid>.Failure($"El SRI rechazó la factura: {mensajeError}");
        }

        var (xmlGeneradoPath, xmlAutorizacionPath) = await SaveXmlFilesAsync(
            subscriberId, invoice.PublicId, xmlResult.Value.XmlFirmado, response.AuthorizedXml, ct);

        return await AuthorizeInvoiceInTransactionAsync(
            invoice, subscriberId, userId, response, xmlGeneradoPath, xmlAutorizacionPath, ct);
    }

    private async Task<Result<(Invoice Invoice, SriSettings Config)>> LoadInvoiceForIssueAsync(
        Guid subscriberId,
        Guid publicId,
        CancellationToken ct)
    {
        var invoice = await _invoiceRepository.GetByPublicIdAsync(publicId, ct);
        if (invoice is null)
            return Result<(Invoice, SriSettings)>.Failure("Factura de venta no encontrada.");

        if (invoice.Status != Invoice.Statuses.Validated)
            return Result<(Invoice, SriSettings)>.Failure(
                $"Solo se puede emitir una factura Validada (estado actual: {invoice.Status}).");

        var configSri = await _configSriRepository.GetBySubscriberIdAsync(subscriberId, ct);
        if (configSri is null)
            return Result<(Invoice, SriSettings)>.Failure(
                "La configuración SRI no está configurada para este tenant.");

        return Result<(Invoice, SriSettings)>.Success((invoice, configSri));
    }

    private async Task<Result<(byte[] XmlFirmado, string XmlContent)>> GenerateAndSignXmlAsync(
        Invoice invoice,
        ERP.Domain.Modules.Sales.Entities.SalesBill legacyBill,
        List<ERP.Domain.Modules.Sales.Entities.SalesBillLine> legacyLines,
        SriSettings configSri,
        Guid userId,
        CancellationToken ct)
    {
        try
        {
            _logger.LogDebug("Generando y firmando XML para factura {FacturaId}", invoice.PublicId);
            var xmlContent = await _sriService.GenerarXmlFacturaAsync(legacyBill, legacyLines, configSri);
            var xmlFirmado = await _sriService.FirmarXmlAsync(
                xmlContent, configSri.CertP12Path, _secretProtector.UnprotectOrPlaintext(configSri.CertPassword));
            return Result<(byte[] XmlFirmado, string XmlContent)>.Success((xmlFirmado, xmlContent));
        }
        catch (SriCommunicationException ex)
        {
            _logger.LogError(ex, "Error SRI al generar/firmar XML de factura {FacturaId}", invoice.PublicId);
            invoice.Reject(userId, ex.Message);
            invoice.Electronic?.MarkRejected(ex.Message);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result<(byte[] XmlFirmado, string XmlContent)>.Failure($"Error SRI: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al generar/firmar XML de factura {FacturaId}", invoice.PublicId);
            invoice.Reject(userId, $"Error al generar XML: {ex.Message}");
            invoice.Electronic?.MarkRejected(ex.Message);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result<(byte[] XmlFirmado, string XmlContent)>.Failure($"Error al generar XML: {ex.Message}");
        }
    }

    private async Task<Result<(SriAutorizacionResponse Response, byte[] XmlFirmado)>> SendToSriAsync(
        Invoice invoice,
        ERP.Domain.Modules.Sales.Entities.SalesBill legacyBill,
        byte[] xmlFirmado,
        SriSettings configSri,
        Guid userId,
        CancellationToken ct)
    {
        try
        {
            _logger.LogDebug("Enviando XML al SRI para factura {FacturaId} (url={Url})", invoice.PublicId, configSri.WsdlUrl);
            invoice.Electronic?.MarkSent();
            var response = await _sriService.EnviarAlSriAsync(xmlFirmado, configSri.WsdlUrl);
            return Result<(SriAutorizacionResponse Response, byte[] XmlFirmado)>.Success((response, xmlFirmado));
        }
        catch (SriCommunicationException ex)
        {
            _logger.LogError(ex, "Error de comunicación SRI para factura {FacturaId}", invoice.PublicId);
            invoice.MarkSendError(userId, ex.Message);
            invoice.Electronic?.MarkSendError(ex.Message);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result<(SriAutorizacionResponse Response, byte[] XmlFirmado)>.Failure($"Error SRI: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error de red al enviar factura {FacturaId} al SRI", invoice.PublicId);
            invoice.MarkSendError(userId, $"Error de comunicación con SRI: {ex.Message}");
            invoice.Electronic?.MarkSendError(ex.Message);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result<(SriAutorizacionResponse Response, byte[] XmlFirmado)>.Failure(
                $"Error de red al comunicarse con el SRI: {ex.Message}");
        }
    }

    private async Task<(string? XmlGeneradoPath, string? XmlAutorizacionPath)> SaveXmlFilesAsync(
        Guid subscriberId,
        Guid publicId,
        byte[] xmlFirmado,
        string authorizedXml,
        CancellationToken ct)
    {
        var xmlGeneradoPath = $"ventas/{subscriberId}/{publicId}/generado.xml";
        var xmlAutorizacionPath = $"ventas/{subscriberId}/{publicId}/autorizado.xml";
        try
        {
            await _fileStorage.SaveAsync(xmlGeneradoPath, new MemoryStream(xmlFirmado), ct);
            await _fileStorage.SaveAsync(xmlAutorizacionPath, new MemoryStream(Encoding.UTF8.GetBytes(authorizedXml)), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo guardar el XML de la factura {FacturaId}; se continúa.", publicId);
            xmlGeneradoPath = null;
            xmlAutorizacionPath = null;
        }
        return (xmlGeneradoPath, xmlAutorizacionPath);
    }

    private async Task<Result<Guid>> AuthorizeInvoiceInTransactionAsync(
        Invoice invoice,
        Guid subscriberId,
        Guid userId,
        SriAutorizacionResponse response,
        string? xmlGeneradoPath,
        string? xmlAutorizacionPath,
        CancellationToken ct)
    {
        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var asientoResult = await _accounting.CrearAsientoVentaAsync(
                salesBillId: invoice.PublicId,
                reference: invoice.InvoiceNumber,
                date: invoice.IssueDate,
                subtotal: invoice.Subtotal,
                vatTotal: invoice.TaxTotal,
                total: invoice.Total,
                description: $"Venta {invoice.InvoiceNumber} — cliente {invoice.BusinessPartnerId}",
                ct);

            if (!asientoResult.IsSuccess)
            {
                await _unitOfWork.RollbackAsync(ct);
                invoice.Reject(userId, $"Autorizado por SRI pero falló el asiento contable: {asientoResult.Error}");
                invoice.Electronic?.MarkRejected(asientoResult.Error);
                await _unitOfWork.SaveChangesAsync(ct);
                return Result<Guid>.Failure(asientoResult.Error ?? "Error al crear asiento contable.");
            }

            var stockLines = await BuildStockLinesAsync(invoice, subscriberId, ct);

            invoice.Authorize(
                userId,
                response.AuthNumber,
                response.AuthDate,
                asientoResult.Value,
                stockLines);

            invoice.Electronic?.MarkAuthorized(response.AuthNumber, response.AuthDate, xmlAutorizacionPath);
            if (invoice.Electronic is not null && xmlGeneradoPath is not null)
                invoice.Electronic.MarkSigned(xmlGeneradoPath);

            await _activity.AddAsync(UserActivity.Create(
                subscriberId, userId, _currentUser.Email, _currentUser.FullName,
                module: "ventas", action: "venta.emitir",
                entityType: "Invoice", entityId: invoice.PublicId,
                description: $"{invoice.InvoiceNumber} — auth: {response.AuthNumber}"), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation(
                "Factura emitida: id {FacturaId}, tenant {SubscriberId}, autorizacion {NumAuth}.",
                invoice.PublicId, subscriberId, response.AuthNumber);

            return Result<Guid>.Success(invoice.PublicId);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            _logger.LogError(ex, "Error al procesar emisión de factura {FacturaId}", invoice.PublicId);
            invoice.Reject(userId, $"Autorizado por SRI pero falló el procesamiento interno: {ex.Message}");
            invoice.Electronic?.MarkRejected(ex.Message);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result<Guid>.Failure($"Error al procesar la emisión: {ex.Message}");
        }
    }

    private async Task<List<InvoiceAuthorizedStockLine>> BuildStockLinesAsync(
        Invoice invoice,
        Guid subscriberId,
        CancellationToken ct)
    {
        var stockLines = new List<InvoiceAuthorizedStockLine>();
        foreach (var detalle in invoice.Lines)
        {
            var producto = await _productRepository.GetByIdAsync(detalle.ProductId, subscriberId, ct);
            if (producto is null || producto.IsService || !producto.TracksStock)
                continue;
            stockLines.Add(new InvoiceAuthorizedStockLine(detalle.ProductId, detalle.Quantity));
        }
        return stockLines;
    }
}
