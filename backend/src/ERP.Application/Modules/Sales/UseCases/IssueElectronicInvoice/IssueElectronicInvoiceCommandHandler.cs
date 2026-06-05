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
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.Fiscal.Entities;
using ERP.Domain.Modules.Fiscal.Interfaces;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Sales.UseCases.IssueElectronicInvoice;

public sealed class IssueElectronicInvoiceCommandHandler
    : IRequestHandler<IssueElectronicInvoiceCommand, Result<Guid>>
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly ISriSettingsRepository _configSriRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly ISriElectronicInvoiceService _sriService;
    private readonly IFileStorage _fileStorage;
    private readonly IAccountingService _accounting;
    private readonly IProductRepository _productRepository;
    private readonly IUserActivityRepository _activity;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISecretProtector _secretProtector;
    private readonly ICurrentSubscriber _currentSubscriber;
    private readonly ICurrentCompany    _currentCompany;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<IssueElectronicInvoiceCommandHandler> _logger;

    public IssueElectronicInvoiceCommandHandler(
        IInvoiceRepository invoiceRepository,
        ISriSettingsRepository configSriRepository,
        ICompanyRepository companyRepository,
        ISriElectronicInvoiceService sriService,
        IFileStorage fileStorage,
        IAccountingService accounting,
        IProductRepository productRepository,
        IUserActivityRepository activity,
        IUnitOfWork unitOfWork,
        ISecretProtector secretProtector,
        ICurrentSubscriber currentSubscriber,
        ICurrentCompany currentCompany,
        ICurrentUser currentUser,
        ILogger<IssueElectronicInvoiceCommandHandler> logger)
    {
        _invoiceRepository   = invoiceRepository;
        _configSriRepository = configSriRepository;
        _companyRepository   = companyRepository;
        _sriService          = sriService;
        _fileStorage         = fileStorage;
        _accounting          = accounting;
        _productRepository   = productRepository;
        _activity            = activity;
        _unitOfWork          = unitOfWork;
        _secretProtector     = secretProtector;
        _currentSubscriber   = currentSubscriber;
        _currentCompany      = currentCompany;
        _currentUser         = currentUser;
        _logger              = logger;
    }

    public async Task<Result<Guid>> Handle(IssueElectronicInvoiceCommand command, CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        var userId = _currentUser.UserId;

        var loadResult = await LoadInvoiceForIssueAsync(subscriberId, command.SaleId, ct);
        if (!loadResult.IsSuccess)
            return Result<Guid>.Failure(loadResult.Error!);

        var (invoice, configSri, company) = loadResult.Value;
        var legacyBill = FiscalInvoiceSriBridge.ToLegacyBill(invoice);
        var legacyLines = FiscalInvoiceSriBridge.ToLegacyLines(invoice, userId);

        var xmlResult = await GenerateAndSignXmlAsync(invoice, legacyBill, legacyLines, configSri, company, userId, ct);
        if (!xmlResult.IsSuccess)
            return Result<Guid>.Failure(xmlResult.Error!);

        var sendResult = await SendToSriAsync(invoice, legacyBill, xmlResult.Value.signedXml, configSri, userId, ct);
        if (!sendResult.IsSuccess)
            return Result<Guid>.Failure(sendResult.Error!);

        var response = sendResult.Value.Response;
        if (!response.IsAuthorized)
        {
            var mensajeError = response.ErrorMessage ?? "SRI rechazó la salesBill sin indicar motivo.";
            _logger.LogWarning("SRI rechazó salesBill {FacturaId}: {Error}", invoice.PublicId, mensajeError);
            invoice.Reject(userId, mensajeError);
            invoice.Electronic?.MarkRejected(mensajeError);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result<Guid>.Failure($"El SRI rechazó la salesBill: {mensajeError}");
        }

        var (generatedXmlPath, authorizationXmlPath) = await SaveXmlFilesAsync(
            subscriberId, invoice.PublicId, xmlResult.Value.signedXml, response.AuthorizedXml, ct);

        return await AuthorizeInvoiceInTransactionAsync(
            invoice, subscriberId, userId, response, generatedXmlPath, authorizationXmlPath, ct);
    }

    private async Task<Result<(Invoice Invoice, SriSettings Config, Company Company)>> LoadInvoiceForIssueAsync(
        Guid subscriberId,
        Guid publicId,
        CancellationToken ct)
    {
        var invoice = await _invoiceRepository.GetByPublicIdAsync(publicId, ct);
        if (invoice is null)
            return Result<(Invoice, SriSettings, Company)>.Failure("salesBill de venta no encontrada.");

        if (invoice.Status != Invoice.Statuses.Validated)
            return Result<(Invoice, SriSettings, Company)>.Failure(
                $"Solo se puede emitir una salesBill Validada (estado actual: {invoice.Status}).");

        var companyId = _currentCompany.CompanyId;

        var configSri = await _configSriRepository.GetByCompanyIdAsync(companyId, ct);
        if (configSri is null)
            return Result<(Invoice, SriSettings, Company)>.Failure(
                "La configuración SRI no está configurada para esta empresa.");

        var company = await _companyRepository.GetByIdAsync(companyId, ct);
        if (company is null)
            return Result<(Invoice, SriSettings, Company)>.Failure("Empresa no encontrada.");

        return Result<(Invoice, SriSettings, Company)>.Success((invoice, configSri, company));
    }

    private async Task<Result<(byte[] signedXml, string XmlContent)>> GenerateAndSignXmlAsync(
        Invoice invoice,
        ERP.Domain.Modules.Sales.Entities.SalesBill legacyBill,
        List<ERP.Domain.Modules.Sales.Entities.SalesBillLine> legacyLines,
        SriSettings configSri,
        Company company,
        Guid userId,
        CancellationToken ct)
    {
        try
        {
            _logger.LogDebug("Generando y firmando XML para salesBill {FacturaId}", invoice.PublicId);
            var xmlContent = await _sriService.GenerateInvoiceXmlAsync(legacyBill, legacyLines, configSri, company);
            var signedXml = await _sriService.SignXmlAsync(
                xmlContent, configSri.CertP12Path, _secretProtector.UnprotectOrPlaintext(configSri.CertPassword));
            return Result<(byte[] signedXml, string XmlContent)>.Success((signedXml, xmlContent));
        }
        catch (SriCommunicationException ex)
        {
            _logger.LogError(ex, "Error SRI al generar/firmar XML de salesBill {FacturaId}", invoice.PublicId);
            invoice.Reject(userId, ex.Message);
            invoice.Electronic?.MarkRejected(ex.Message);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result<(byte[] signedXml, string XmlContent)>.Failure($"Error SRI: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al generar/firmar XML de salesBill {FacturaId}", invoice.PublicId);
            invoice.Reject(userId, $"Error al generar XML: {ex.Message}");
            invoice.Electronic?.MarkRejected(ex.Message);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result<(byte[] signedXml, string XmlContent)>.Failure($"Error al generar XML: {ex.Message}");
        }
    }

    private async Task<Result<(SriAuthorizationResponse Response, byte[] signedXml)>> SendToSriAsync(
        Invoice invoice,
        ERP.Domain.Modules.Sales.Entities.SalesBill legacyBill,
        byte[] signedXml,
        SriSettings configSri,
        Guid userId,
        CancellationToken ct)
    {
        try
        {
            _logger.LogDebug("Enviando XML al SRI para salesBill {FacturaId} (url={Url})", invoice.PublicId, configSri.WsdlUrl);
            invoice.Electronic?.MarkSent();
            var response = await _sriService.SendToSriAsync(signedXml, configSri.WsdlUrl);
            return Result<(SriAuthorizationResponse Response, byte[] signedXml)>.Success((response, signedXml));
        }
        catch (SriCommunicationException ex)
        {
            _logger.LogError(ex, "Error de comunicación SRI para salesBill {FacturaId}", invoice.PublicId);
            invoice.MarkSendError(userId, ex.Message);
            invoice.Electronic?.MarkSendError(ex.Message);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result<(SriAuthorizationResponse Response, byte[] signedXml)>.Failure($"Error SRI: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error de red al enviar salesBill {FacturaId} al SRI", invoice.PublicId);
            invoice.MarkSendError(userId, $"Error de comunicación con SRI: {ex.Message}");
            invoice.Electronic?.MarkSendError(ex.Message);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result<(SriAuthorizationResponse Response, byte[] signedXml)>.Failure(
                $"Error de red al comunicarse con el SRI: {ex.Message}");
        }
    }

    private async Task<(string? generatedXmlPath, string? authorizationXmlPath)> SaveXmlFilesAsync(
        Guid subscriberId,
        Guid publicId,
        byte[] signedXml,
        string authorizedXml,
        CancellationToken ct)
    {
        var generatedXmlPath = $"ventas/{subscriberId}/{publicId}/generado.xml";
        var authorizationXmlPath = $"ventas/{subscriberId}/{publicId}/autorizado.xml";
        try
        {
            await _fileStorage.SaveAsync(generatedXmlPath, new MemoryStream(signedXml), ct);
            await _fileStorage.SaveAsync(authorizationXmlPath, new MemoryStream(Encoding.UTF8.GetBytes(authorizedXml)), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo guardar el XML de la salesBill {FacturaId}; se continúa.", publicId);
            generatedXmlPath = null;
            authorizationXmlPath = null;
        }
        return (generatedXmlPath, authorizationXmlPath);
    }

    private async Task<Result<Guid>> AuthorizeInvoiceInTransactionAsync(
        Invoice invoice,
        Guid subscriberId,
        Guid userId,
        SriAuthorizationResponse response,
        string? generatedXmlPath,
        string? authorizationXmlPath,
        CancellationToken ct)
    {
        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var journalEntryResult = await _accounting.CreateSalesJournalEntryAsync(
                salesBillId: invoice.PublicId,
                reference: invoice.InvoiceNumber,
                date: invoice.IssueDate,
                subtotal: invoice.Subtotal,
                vatTotal: invoice.TaxTotal,
                total: invoice.Total,
                description: $"Venta {invoice.InvoiceNumber} — cliente {invoice.BusinessPartnerId}",
                ct);

            if (!journalEntryResult.IsSuccess)
            {
                await _unitOfWork.RollbackAsync(ct);
                invoice.Reject(userId, $"Autorizado por SRI pero falló el asiento contable: {journalEntryResult.Error}");
                invoice.Electronic?.MarkRejected(journalEntryResult.Error);
                await _unitOfWork.SaveChangesAsync(ct);
                return Result<Guid>.Failure(journalEntryResult.Error ?? "Error al crear asiento contable.");
            }

            var stockLines = await BuildStockLinesAsync(invoice, subscriberId, ct);

            invoice.Authorize(
                userId,
                response.AuthNumber,
                response.AuthDate,
                journalEntryResult.Value,
                stockLines);

            invoice.Electronic?.MarkAuthorized(response.AuthNumber, response.AuthDate, authorizationXmlPath);
            if (invoice.Electronic is not null && generatedXmlPath is not null)
                invoice.Electronic.MarkSigned(generatedXmlPath);

            await _activity.AddAsync(UserActivity.Create(
                subscriberId, userId, _currentUser.Email, _currentUser.FullName,
                module: "ventas", action: "venta.emitir",
                entityType: "Invoice", entityId: invoice.PublicId,
                description: $"{invoice.InvoiceNumber} — auth: {response.AuthNumber}"), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation(
                "salesBill emitida: id {FacturaId}, tenant {SubscriberId}, autorizacion {NumAuth}.",
                invoice.PublicId, subscriberId, response.AuthNumber);

            return Result<Guid>.Success(invoice.PublicId);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            _logger.LogError(ex, "Error al procesar emisión de salesBill {FacturaId}", invoice.PublicId);
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
        foreach (var line in invoice.Lines)
        {
            var producto = await _productRepository.GetByIdAsync(line.ProductId, subscriberId, ct);
            if (producto is null || producto.IsService || !producto.TracksStock)
                continue;
            stockLines.Add(new InvoiceAuthorizedStockLine(line.ProductId, line.Quantity));
        }
        return stockLines;
    }
}
