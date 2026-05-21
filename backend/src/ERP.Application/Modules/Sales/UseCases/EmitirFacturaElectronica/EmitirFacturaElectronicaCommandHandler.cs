using System.Text;
using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Sales.Events;
using ERP.Domain.Modules.Sales.Interfaces;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Sales.UseCases.EmitirFacturaElectronica;

public sealed class IssueElectronicInvoiceCommandHandler
    : IRequestHandler<IssueElectronicInvoiceCommand, Result<Guid>>
{
    private readonly ISalesRepository               _ventasRepository;
    private readonly ISriSettingsRepository     _configSriRepository;
    private readonly ISriFacturaElectronicaService   _sriService;
    private readonly IFileStorage                    _fileStorage;
    private readonly IAccountingService              _accounting;
    private readonly IProductRepository              _productRepository;
    private readonly IUserActivityRepository         _activity;
    private readonly IUnitOfWork                     _unitOfWork;
    private readonly ISecretProtector                _secretProtector;
    private readonly ICurrentSubscriber                  _currentSubscriber;
    private readonly ICurrentUser                    _currentUser;
    private readonly ILogger<IssueElectronicInvoiceCommandHandler> _logger;

    public IssueElectronicInvoiceCommandHandler(
        ISalesRepository ventasRepository,
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
        _ventasRepository    = ventasRepository;
        _configSriRepository = configSriRepository;
        _sriService          = sriService;
        _fileStorage         = fileStorage;
        _accounting          = accounting;
        _productRepository   = productRepository;
        _activity            = activity;
        _unitOfWork          = unitOfWork;
        _secretProtector     = secretProtector;
        _currentSubscriber       = currentSubscriber;
        _currentUser         = currentUser;
        _logger              = logger;
    }

    public async Task<Result<Guid>> Handle(
        IssueElectronicInvoiceCommand command,
        CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        var userId   = _currentUser.UserId;

        var loadResult = await LoadBillForIssueAsync(subscriberId, command.VentaId, ct);
        if (!loadResult.IsSuccess)
            return Result<Guid>.Failure(loadResult.Error!);

        var (factura, configSri, detalles) = loadResult.Value;

        var xmlResult = await GenerateAndSignXmlAsync(factura, detalles, configSri, userId, ct);
        if (!xmlResult.IsSuccess)
            return Result<Guid>.Failure(xmlResult.Error!);

        var sendResult = await SendToSriAsync(factura, xmlResult.Value.XmlFirmado, configSri, userId, ct);
        if (!sendResult.IsSuccess)
            return Result<Guid>.Failure(sendResult.Error!);

        var response = sendResult.Value.Response;
        if (!response.IsAuthorized)
        {
            var mensajeError = response.ErrorMessage ?? "SRI rechazó la factura sin indicar motivo.";
            _logger.LogWarning("SRI rechazó factura {FacturaId}: {Error}", factura.Id, mensajeError);
            factura.Reject(userId, mensajeError);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result<Guid>.Failure($"El SRI rechazó la factura: {mensajeError}");
        }

        var (xmlGeneradoPath, xmlAutorizacionPath) = await SaveXmlFilesAsync(
            subscriberId, factura.Id, xmlResult.Value.XmlFirmado, response.AuthorizedXml, ct);

        return await AuthorizeBillInTransactionAsync(
            factura, detalles, subscriberId, userId, response, xmlGeneradoPath, xmlAutorizacionPath, ct);
    }

    private async Task<Result<(ERP.Domain.Modules.Sales.Entities.SalesBill Factura, ERP.Domain.Configuration.Entities.SriSettings Config, List<ERP.Domain.Modules.Sales.Entities.SalesBillLine> Detalles)>>
        LoadBillForIssueAsync(Guid subscriberId, Guid ventaId, CancellationToken ct)
    {
        var factura = await _ventasRepository.GetBillByIdAsync(subscriberId, ventaId, ct);
        if (factura is null)
            return Result<(ERP.Domain.Modules.Sales.Entities.SalesBill, ERP.Domain.Configuration.Entities.SriSettings, List<ERP.Domain.Modules.Sales.Entities.SalesBillLine>)>.Failure(
                "Factura de venta no encontrada.");

        if (factura.Status != "Validado")
            return Result<(ERP.Domain.Modules.Sales.Entities.SalesBill, ERP.Domain.Configuration.Entities.SriSettings, List<ERP.Domain.Modules.Sales.Entities.SalesBillLine>)>.Failure(
                $"Solo se puede emitir una factura Validada (estado actual: {factura.Status}).");

        var configSri = await _configSriRepository.GetBySubscriberIdAsync(subscriberId, ct);
        if (configSri is null)
            return Result<(ERP.Domain.Modules.Sales.Entities.SalesBill, ERP.Domain.Configuration.Entities.SriSettings, List<ERP.Domain.Modules.Sales.Entities.SalesBillLine>)>.Failure(
                "La configuración SRI no está configurada para este tenant.");

        return Result<(ERP.Domain.Modules.Sales.Entities.SalesBill, ERP.Domain.Configuration.Entities.SriSettings, List<ERP.Domain.Modules.Sales.Entities.SalesBillLine>)>.Success(
            (factura, configSri, factura.Lines.ToList()));
    }

    private async Task<Result<(byte[] XmlFirmado, string XmlContent)>> GenerateAndSignXmlAsync(
        ERP.Domain.Modules.Sales.Entities.SalesBill factura,
        List<ERP.Domain.Modules.Sales.Entities.SalesBillLine> detalles,
        ERP.Domain.Configuration.Entities.SriSettings configSri,
        Guid userId,
        CancellationToken ct)
    {
        try
        {
            _logger.LogDebug("Generando y firmando XML para factura {FacturaId}", factura.Id);
            var xmlContent = await _sriService.GenerarXmlFacturaAsync(factura, detalles, configSri);
            var xmlFirmado = await _sriService.FirmarXmlAsync(
                xmlContent, configSri.CertP12Path, _secretProtector.UnprotectOrPlaintext(configSri.CertPassword));
            return Result<(byte[] XmlFirmado, string XmlContent)>.Success((xmlFirmado, xmlContent));
        }
        catch (SriCommunicationException ex)
        {
            _logger.LogError(ex, "Error SRI al generar/firmar XML de factura {FacturaId}", factura.Id);
            factura.Reject(userId, ex.Message);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result<(byte[] XmlFirmado, string XmlContent)>.Failure($"Error SRI: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al generar/firmar XML de factura {FacturaId}", factura.Id);
            factura.Reject(userId, $"Error al generar XML: {ex.Message}");
            await _unitOfWork.SaveChangesAsync(ct);
            return Result<(byte[] XmlFirmado, string XmlContent)>.Failure($"Error al generar XML: {ex.Message}");
        }
    }

    private async Task<Result<(SriAutorizacionResponse Response, byte[] XmlFirmado)>> SendToSriAsync(
        ERP.Domain.Modules.Sales.Entities.SalesBill factura,
        byte[] xmlFirmado,
        ERP.Domain.Configuration.Entities.SriSettings configSri,
        Guid userId,
        CancellationToken ct)
    {
        try
        {
            _logger.LogDebug("Enviando XML al SRI para factura {FacturaId} (url={Url})", factura.Id, configSri.WsdlUrl);
            var response = await _sriService.EnviarAlSriAsync(xmlFirmado, configSri.WsdlUrl);
            return Result<(SriAutorizacionResponse Response, byte[] XmlFirmado)>.Success((response, xmlFirmado));
        }
        catch (SriCommunicationException ex)
        {
            _logger.LogError(ex, "Error de comunicación SRI para factura {FacturaId}", factura.Id);
            factura.Reject(userId, ex.Message);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result<(SriAutorizacionResponse Response, byte[] XmlFirmado)>.Failure($"Error SRI: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error de red al enviar factura {FacturaId} al SRI", factura.Id);
            factura.Reject(userId, $"Error de comunicación con SRI: {ex.Message}");
            await _unitOfWork.SaveChangesAsync(ct);
            return Result<(SriAutorizacionResponse Response, byte[] XmlFirmado)>.Failure(
                $"Error de red al comunicarse con el SRI: {ex.Message}");
        }
    }

    private async Task<(string? XmlGeneradoPath, string? XmlAutorizacionPath)> SaveXmlFilesAsync(
        Guid subscriberId,
        Guid facturaId,
        byte[] xmlFirmado,
        string authorizedXml,
        CancellationToken ct)
    {
        var xmlGeneradoPath     = $"ventas/{subscriberId}/{facturaId}/generado.xml";
        var xmlAutorizacionPath = $"ventas/{subscriberId}/{facturaId}/autorizado.xml";
        try
        {
            await _fileStorage.SaveAsync(xmlGeneradoPath,
                new MemoryStream(xmlFirmado), ct);
            await _fileStorage.SaveAsync(xmlAutorizacionPath,
                new MemoryStream(Encoding.UTF8.GetBytes(authorizedXml)), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo guardar el XML de la factura {FacturaId}; se continúa.", facturaId);
            xmlGeneradoPath     = null;
            xmlAutorizacionPath = null;
        }
        return (xmlGeneradoPath, xmlAutorizacionPath);
    }

    private async Task<Result<Guid>> AuthorizeBillInTransactionAsync(
        ERP.Domain.Modules.Sales.Entities.SalesBill factura,
        List<ERP.Domain.Modules.Sales.Entities.SalesBillLine> detalles,
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
            var numeroFactura = $"{factura.EstabCode}-{factura.EmPointCode}-{factura.Sequential}";

            var asientoResult = await _accounting.CrearAsientoVentaAsync(
                salesBillId:     factura.Id,
                reference:  numeroFactura,
                date:       factura.IssueDate,
                subtotal:    factura.Subtotal,
                vatTotal:         factura.VatTotal,
                total:       factura.Total,
                description: $"Venta {numeroFactura} — cliente {factura.CustomerId}",
                ct);

            if (!asientoResult.IsSuccess)
            {
                await _unitOfWork.RollbackAsync(ct);
                factura.Reject(userId,
                    $"Autorizado por SRI pero falló el asiento contable: {asientoResult.Error}");
                await _unitOfWork.SaveChangesAsync(ct);
                return Result<Guid>.Failure(asientoResult.Error ?? "Error al crear asiento contable.");
            }

            var stockLines = await BuildStockLinesAsync(detalles, subscriberId, ct);

            factura.Authorize(
                userId,
                response.AuthNumber,
                response.AuthDate,
                xmlGeneradoPath,
                xmlAutorizacionPath,
                asientoResult.Value,
                stockLines);

            await _activity.AddAsync(UserActivity.Create(
                subscriberId, userId, _currentUser.Email, _currentUser.FullName,
                module: "ventas", action: "venta.emitir",
                entityType: "SalesBill", entityId: factura.Id,
                description: $"{numeroFactura} — auth: {response.AuthNumber}"), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation(
                "Factura emitida: id {FacturaId}, tenant {SubscriberId}, autorizacion {NumAuth}.",
                factura.Id, subscriberId, response.AuthNumber);

            return Result<Guid>.Success(factura.Id);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            _logger.LogError(ex, "Error al procesar emisión de factura {FacturaId}", factura.Id);
            factura.Reject(userId,
                $"Autorizado por SRI pero falló el procesamiento interno: {ex.Message}");
            await _unitOfWork.SaveChangesAsync(ct);
            return Result<Guid>.Failure($"Error al procesar la emisión: {ex.Message}");
        }
    }

    private async Task<List<SalesBillAuthorizedStockLine>> BuildStockLinesAsync(
        List<ERP.Domain.Modules.Sales.Entities.SalesBillLine> detalles,
        Guid subscriberId,
        CancellationToken ct)
    {
        var stockLines = new List<SalesBillAuthorizedStockLine>();
        foreach (var detalle in detalles)
        {
            var producto = await _productRepository.GetByIdAsync(detalle.ProductId, subscriberId, ct);
            if (producto is null || producto.IsService || !producto.TracksStock)
                continue;
            stockLines.Add(new SalesBillAuthorizedStockLine(detalle.ProductId, detalle.Quantity));
        }
        return stockLines;
    }
}
