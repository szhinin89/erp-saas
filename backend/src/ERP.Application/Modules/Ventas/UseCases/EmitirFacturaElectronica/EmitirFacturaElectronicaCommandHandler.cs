using System.Text;
using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Inventario.Entities;
using ERP.Domain.Inventario.Enums;
using ERP.Domain.Inventario.Interfaces;
using ERP.Domain.Products.Interfaces;
using ERP.Domain.Ventas.Interfaces;

namespace ERP.Application.Ventas.UseCases.EmitirFacturaElectronica;

public sealed class EmitirFacturaElectronicaCommandHandler
    : IRequestHandler<EmitirFacturaElectronicaCommand, Result<Guid>>
{
    private readonly IVentasRepository               _ventasRepository;
    private readonly IConfiguracionSRIRepository     _configSriRepository;
    private readonly ISriFacturaElectronicaService   _sriService;
    private readonly IFileStorage                    _fileStorage;
    private readonly IAccountingService              _accounting;
    private readonly IInventarioStockRepository      _inventario;
    private readonly IProductRepository              _productRepository;
    private readonly IUserActivityRepository         _activity;
    private readonly IUnitOfWork                     _unitOfWork;
    private readonly ICurrentTenant                  _currentTenant;
    private readonly ICurrentUser                    _currentUser;
    private readonly ILogger<EmitirFacturaElectronicaCommandHandler> _logger;

    public EmitirFacturaElectronicaCommandHandler(
        IVentasRepository ventasRepository,
        IConfiguracionSRIRepository configSriRepository,
        ISriFacturaElectronicaService sriService,
        IFileStorage fileStorage,
        IAccountingService accounting,
        IInventarioStockRepository inventario,
        IProductRepository productRepository,
        IUserActivityRepository activity,
        IUnitOfWork unitOfWork,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        ILogger<EmitirFacturaElectronicaCommandHandler> logger)
    {
        _ventasRepository    = ventasRepository;
        _configSriRepository = configSriRepository;
        _sriService          = sriService;
        _fileStorage         = fileStorage;
        _accounting          = accounting;
        _inventario          = inventario;
        _productRepository   = productRepository;
        _activity            = activity;
        _unitOfWork          = unitOfWork;
        _currentTenant       = currentTenant;
        _currentUser         = currentUser;
        _logger              = logger;
    }

    public async Task<Result<Guid>> Handle(
        EmitirFacturaElectronicaCommand command,
        CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;
        var userId   = _currentUser.UserId;

        // 1. Cargar factura con detalles
        var factura = await _ventasRepository.GetFacturaByIdAsync(tenantId, command.VentaId, ct);
        if (factura is null)
            return Result<Guid>.Failure("Factura de venta no encontrada.");

        if (factura.Estado != "Validado")
            return Result<Guid>.Failure(
                $"Solo se puede emitir una factura Validada (estado actual: {factura.Estado}).");

        // 2. Cargar configuración SRI
        var configSri = await _configSriRepository.GetByTenantIdAsync(tenantId, ct);
        if (configSri is null)
            return Result<Guid>.Failure("La configuración SRI no está configurada para este tenant.");

        // 3. Generar y firmar XML
        var detalles = factura.Detalles.ToList();
        string xmlContent;
        byte[] xmlFirmado;
        try
        {
            _logger.LogDebug("Generando y firmando XML para factura {FacturaId}", factura.Id);
            xmlContent = await _sriService.GenerarXmlFacturaAsync(factura, detalles, configSri);
            xmlFirmado = await _sriService.FirmarXmlAsync(
                xmlContent, configSri.CertificadoP12Path, configSri.CertificadoPassword);
        }
        catch (SriCommunicationException ex)
        {
            _logger.LogError(ex, "Error SRI al generar/firmar XML de factura {FacturaId}", factura.Id);
            factura.MarcarErrorEnvio(userId, ex.Message);
            await _ventasRepository.SaveChangesAsync(ct);
            return Result<Guid>.Failure($"Error SRI: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al generar/firmar XML de factura {FacturaId}", factura.Id);
            factura.MarcarErrorEnvio(userId, $"Error al generar XML: {ex.Message}");
            await _ventasRepository.SaveChangesAsync(ct);
            return Result<Guid>.Failure($"Error al generar XML: {ex.Message}");
        }

        // 4. Enviar al SRI
        SriAutorizacionResponse response;
        try
        {
            _logger.LogDebug("Enviando XML al SRI para factura {FacturaId} (url={Url})", factura.Id, configSri.UrlSriAutorizacion);
            response = await _sriService.EnviarAlSriAsync(xmlFirmado, configSri.UrlSriAutorizacion);
        }
        catch (SriCommunicationException ex)
        {
            _logger.LogError(ex, "Error de comunicación SRI para factura {FacturaId}", factura.Id);
            factura.MarcarErrorEnvio(userId, ex.Message);
            await _ventasRepository.SaveChangesAsync(ct);
            return Result<Guid>.Failure($"Error SRI: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error de red al enviar factura {FacturaId} al SRI", factura.Id);
            factura.MarcarErrorEnvio(userId, $"Error de comunicación con SRI: {ex.Message}");
            await _ventasRepository.SaveChangesAsync(ct);
            return Result<Guid>.Failure($"Error de red al comunicarse con el SRI: {ex.Message}");
        }

        // 5. Respuesta no autorizada
        if (!response.Autorizada)
        {
            var mensajeError = response.MensajeError ?? "SRI rechazó la factura sin indicar motivo.";
            _logger.LogWarning("SRI rechazó factura {FacturaId}: {Error}", factura.Id, mensajeError);
            factura.Rechazar(userId, mensajeError);
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
                new MemoryStream(Encoding.UTF8.GetBytes(response.XmlAutorizado)), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo guardar el XML de la factura {FacturaId}; se continúa.", factura.Id);
            // No se aborta el proceso: la autorización SRI ya ocurrió
            xmlGeneradoPath     = null;
            xmlAutorizacionPath = null;
        }

        // 7. Transacción: asiento + inventario + cambio de estado
        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var numeroFactura = $"{factura.Establecimiento}-{factura.PuntoEmision}-{factura.Secuencial}";

            var asientoResult = await _accounting.CrearAsientoVentaAsync(
                ventaId:     factura.Id,
                referencia:  numeroFactura,
                fecha:       factura.FechaEmision,
                subtotal:    factura.Subtotal,
                iva:         factura.Impuesto,
                total:       factura.Total,
                descripcion: $"Venta {numeroFactura} — cliente {factura.ClienteId}",
                ct);

            if (!asientoResult.IsSuccess)
            {
                await _unitOfWork.RollbackAsync(ct);
                factura.MarcarErrorEnvio(userId,
                    $"Autorizado por SRI pero falló el asiento contable: {asientoResult.Error}");
                await _ventasRepository.SaveChangesAsync(ct);
                return Result<Guid>.Failure(asientoResult.Error ?? "Error al crear asiento contable.");
            }

            // 8. Descontar inventario por cada detalle
            foreach (var detalle in detalles)
            {
                var producto = await _productRepository.GetByIdAsync(detalle.ProductoId, tenantId, ct);
                if (producto is null || producto.IsService || !producto.TracksStock) continue;

                var stock = await _inventario.GetStockByTenantBodegaProductAsync(
                    tenantId, factura.BodegaId, detalle.ProductoId, ct);

                if (stock is null)
                {
                    _logger.LogWarning(
                        "Factura {FacturaId}: sin stock registrado para producto {ProductoId} en bodega {BodegaId}; se omite descuento.",
                        factura.Id, detalle.ProductoId, factura.BodegaId);
                    continue;
                }

                var cantidadAnterior = stock.Cantidad;
                // Costo promedio ANTES de aplicar el movimiento (para valorizar la salida).
                var costoPromedioVenta = stock.CostoPromedioActual;
                stock.AplicarMovimiento(-detalle.Cantidad, userId, costoPromedioVenta);

                var movimiento = InventarioMovimiento.Create(
                    tenantId,
                    detalle.ProductoId,
                    factura.BodegaId,
                    TipoMovimientoInventario.SalidaVenta,
                    cantidad:            -detalle.Cantidad,
                    cantidadAnterior:    cantidadAnterior,
                    referencia:          numeroFactura,
                    documentoOrigenId:   factura.Id,
                    documentoOrigenTipo: "VentasFactura",
                    createdBy:           userId,
                    costoUnitario:       costoPromedioVenta);

                await _inventario.AddMovimientoAsync(movimiento, ct);
            }

            // 9. Actualizar estado de la factura
            factura.Autorizar(
                userId,
                response.NumeroAutorizacion,
                response.FechaAutorizacion,
                xmlGeneradoPath,
                xmlAutorizacionPath,
                asientoResult.Value);

            await _activity.AddAsync(UserActivity.Create(
                tenantId, userId, _currentUser.Email, _currentUser.FullName,
                module: "ventas", action: "venta.emitir",
                entityType: "VentasFactura", entityId: factura.Id,
                description: $"{numeroFactura} — auth: {response.NumeroAutorizacion}"), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation(
                "Factura emitida: id {FacturaId}, tenant {TenantId}, autorizacion {NumAuth}.",
                factura.Id, tenantId, response.NumeroAutorizacion);

            return Result<Guid>.Success(factura.Id);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            _logger.LogError(ex, "Error al procesar emisión de factura {FacturaId}", command.VentaId);
            factura.MarcarErrorEnvio(userId,
                $"Autorizado por SRI pero falló el procesamiento interno: {ex.Message}");
            await _ventasRepository.SaveChangesAsync(ct);
            return Result<Guid>.Failure($"Error al procesar la emisión: {ex.Message}");
        }
    }
}
