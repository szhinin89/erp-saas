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
    private readonly ICurrentTenant                  _currentTenant;
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
        ICurrentTenant currentTenant,
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
        _currentTenant       = currentTenant;
        _currentUser         = currentUser;
        _logger              = logger;
    }

    public async Task<Result<Guid>> Handle(
        IssueElectronicInvoiceCommand command,
        CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;
        var userId   = _currentUser.UserId;

        // 1. Cargar factura con detalles
        var factura = await _ventasRepository.GetBillByIdAsync(tenantId, command.VentaId, ct);
        if (factura is null)
            return Result<Guid>.Failure("Factura de venta no encontrada.");

        if (factura.Status != "Validado")
            return Result<Guid>.Failure(
                $"Solo se puede emitir una factura Validada (estado actual: {factura.Status}).");

        // 2. Cargar configuración SRI
        var configSri = await _configSriRepository.GetByTenantIdAsync(tenantId, ct);
        if (configSri is null)
            return Result<Guid>.Failure("La configuración SRI no está configurada para este tenant.");

        // 3. Generar y firmar XML
        var detalles = factura.Lines.ToList();
        string xmlContent;
        byte[] xmlFirmado;
        try
        {
            _logger.LogDebug("Generando y firmando XML para factura {FacturaId}", factura.Id);
            xmlContent = await _sriService.GenerarXmlFacturaAsync(factura, detalles, configSri);
            xmlFirmado = await _sriService.FirmarXmlAsync(
                xmlContent, configSri.CertP12Path, configSri.CertPassword);
        }
        catch (SriCommunicationException ex)
        {
            _logger.LogError(ex, "Error SRI al generar/firmar XML de factura {FacturaId}", factura.Id);
            factura.Reject(userId, ex.Message);
            await _ventasRepository.SaveChangesAsync(ct);
            return Result<Guid>.Failure($"Error SRI: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al generar/firmar XML de factura {FacturaId}", factura.Id);
            factura.Reject(userId, $"Error al generar XML: {ex.Message}");
            await _ventasRepository.SaveChangesAsync(ct);
            return Result<Guid>.Failure($"Error al generar XML: {ex.Message}");
        }

        // 4. Enviar al SRI
        SriAutorizacionResponse response;
        try
        {
            _logger.LogDebug("Enviando XML al SRI para factura {FacturaId} (url={Url})", factura.Id, configSri.WsdlUrl);
            response = await _sriService.EnviarAlSriAsync(xmlFirmado, configSri.WsdlUrl);
        }
        catch (SriCommunicationException ex)
        {
            _logger.LogError(ex, "Error de comunicación SRI para factura {FacturaId}", factura.Id);
            factura.Reject(userId, ex.Message);
            await _ventasRepository.SaveChangesAsync(ct);
            return Result<Guid>.Failure($"Error SRI: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error de red al enviar factura {FacturaId} al SRI", factura.Id);
            factura.Reject(userId, $"Error de comunicación con SRI: {ex.Message}");
            await _ventasRepository.SaveChangesAsync(ct);
            return Result<Guid>.Failure($"Error de red al comunicarse con el SRI: {ex.Message}");
        }

        // 5. Respuesta no autorizada
        if (!response.IsAuthorized)
        {
            var mensajeError = response.ErrorMessage ?? "SRI rechazó la factura sin indicar motivo.";
            _logger.LogWarning("SRI rechazó factura {FacturaId}: {Error}", factura.Id, mensajeError);
            factura.Reject(userId, mensajeError);
            await _ventasRepository.SaveChangesAsync(ct);
            return Result<Guid>.Failure($"El SRI rechazó la factura: {mensajeError}");
        }

        // 6. Guardar archivos XML fuera de la transacción DB
        var xmlGeneradoPath     = $"ventas/{tenantId}/{factura.Id}/generado.xml";
        var xmlAutorizacionPath = $"ventas/{tenantId}/{factura.Id}/autorizado.xml";
        try
        {
            await _fileStorage.SaveAsync(xmlGeneradoPath,
                new MemoryStream(xmlFirmado), ct);
            await _fileStorage.SaveAsync(xmlAutorizacionPath,
                new MemoryStream(Encoding.UTF8.GetBytes(response.AuthorizedXml)), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo guardar el XML de la factura {FacturaId}; se continúa.", factura.Id);
            // No se aborta el proceso: la autorización SRI ya ocurrió
            xmlGeneradoPath     = null;
            xmlAutorizacionPath = null;
        }

        // 7. Transacción: asiento + cambio de estado (inventario vía SalesBillAuthorizedEvent)
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
                await _ventasRepository.SaveChangesAsync(ct);
                return Result<Guid>.Failure(asientoResult.Error ?? "Error al crear asiento contable.");
            }

            var stockLines = new List<SalesBillAuthorizedStockLine>();
            foreach (var detalle in detalles)
            {
                var producto = await _productRepository.GetByIdAsync(detalle.ProductId, tenantId, ct);
                if (producto is null || producto.IsService || !producto.TracksStock)
                    continue;
                stockLines.Add(new SalesBillAuthorizedStockLine(detalle.ProductId, detalle.Quantity));
            }

            factura.Authorize(
                userId,
                response.AuthNumber,
                response.AuthDate,
                xmlGeneradoPath,
                xmlAutorizacionPath,
                asientoResult.Value,
                stockLines);

            await _activity.AddAsync(UserActivity.Create(
                tenantId, userId, _currentUser.Email, _currentUser.FullName,
                module: "ventas", action: "venta.emitir",
                entityType: "SalesBill", entityId: factura.Id,
                description: $"{numeroFactura} — auth: {response.AuthNumber}"), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation(
                "Factura emitida: id {FacturaId}, tenant {TenantId}, autorizacion {NumAuth}.",
                factura.Id, tenantId, response.AuthNumber);

            return Result<Guid>.Success(factura.Id);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            _logger.LogError(ex, "Error al procesar emisión de factura {FacturaId}", command.VentaId);
            factura.Reject(userId,
                $"Autorizado por SRI pero falló el procesamiento interno: {ex.Message}");
            await _ventasRepository.SaveChangesAsync(ct);
            return Result<Guid>.Failure($"Error al procesar la emisión: {ex.Message}");
        }
    }
}
