using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Modules.Sales.Entities;

namespace ERP.Infrastructure.Services;

public sealed class SriFacturaElectronicaSimuladoService : ISriFacturaElectronicaService
{
    private readonly IFileStorage                               _fileStorage;
    private readonly ILogger<SriFacturaElectronicaSimuladoService> _logger;

    public SriFacturaElectronicaSimuladoService(
        IFileStorage fileStorage,
        ILogger<SriFacturaElectronicaSimuladoService> logger)
    {
        _fileStorage = fileStorage;
        _logger      = logger;
    }

    public async Task<string> GenerarXmlFacturaAsync(VentasFactura factura, List<VentasDetalle> detalles, ConfiguracionSRI config)
    {
        _logger.LogDebug(
            "[SRI-SIM] Generando XML para factura {FacturaId} (clave={ClaveAcceso}, tenant={TenantId})",
            factura.Id, factura.ClaveAcceso, factura.TenantId);

        // Construir XDocument según XSD del SRI (versión simplificada para simulación)
        var xmlDoc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement("factura",
                new XAttribute("id", "comprobante"),
                new XAttribute("version", "1.0.0"),
                new XElement("infoTributaria",
                    new XElement("ambiente", config.Ambiente),
                    new XElement("tipoEmision", config.TipoEmision),
                    new XElement("razonSocial", config.RazonSocial),
                    new XElement("nombreComercial", config.NombreComercial ?? config.RazonSocial),
                    new XElement("ruc", config.RucEmpresa),
                    new XElement("claveAcceso", factura.ClaveAcceso),
                    new XElement("codDoc", factura.TipoDocumento),
                    new XElement("estab", factura.Establecimiento),
                    new XElement("ptoEmi", factura.PuntoEmision),
                    new XElement("secuencial", factura.Secuencial),
                    new XElement("dirMatriz", config.DireccionMatriz)
                ),
                new XElement("infoFactura",
                    new XElement("fechaEmision", factura.FechaEmision.ToString("dd/MM/yyyy")),
                    new XElement("dirEstablecimiento", config.DireccionMatriz),
                    new XElement("obligadoContabilidad", config.ObligadoContabilidad ? "SI" : "NO"),
                    new XElement("tipoIdentificacionComprador", "04"), // RUC por defecto
                    new XElement("razonSocialComprador", factura.Cliente.LegalName),
                    new XElement("identificacionComprador", factura.Cliente.IdentificationNumber),
                    new XElement("direccionComprador", factura.Cliente.AddressLine ?? ""),
                    new XElement("totalSinImpuestos", factura.Subtotal.ToString("F2")),
                    new XElement("totalDescuento", "0.00"),
                    new XElement("totalConImpuestos",
                        new XElement("totalImpuesto",
                            new XElement("codigo", "2"), // IVA
                            new XElement("codigoPorcentaje", "2"), // 12%
                            new XElement("baseImponible", factura.Subtotal.ToString("F2")),
                            new XElement("valor", factura.Impuesto.ToString("F2"))
                        )
                    ),
                    new XElement("propina", "0.00"),
                    new XElement("importeTotal", factura.Total.ToString("F2")),
                    new XElement("moneda", "DOLAR")
                ),
                new XElement("detalles",
                    detalles.Select(d => new XElement("detalle",
                        new XElement("codigoPrincipal", d.ProductoId.ToString()),
                        new XElement("descripcion", d.Descripcion),
                        new XElement("cantidad", d.Cantidad.ToString("F4")),
                        new XElement("precioUnitario", d.PrecioUnitario.ToString("F6")),
                        new XElement("descuento", "0.00"),
                        new XElement("precioTotalSinImpuesto", d.Subtotal.ToString("F2")),
                        new XElement("impuestos",
                            new XElement("impuesto",
                                new XElement("codigo", "2"), // IVA
                                new XElement("codigoPorcentaje", "2"), // 12%
                                new XElement("tarifa", "12.00"),
                                new XElement("baseImponible", d.Subtotal.ToString("F2")),
                                new XElement("valor", d.Impuesto.ToString("F2"))
                            )
                        )
                    ))
                ),
                new XElement("infoAdicional",
                    new XElement("campoAdicional",
                        new XAttribute("nombre", "Email"),
                        factura.Cliente.Email ?? ""
                    )
                )
            )
        );

        var xmlString = xmlDoc.ToString();

        _logger.LogDebug("[SRI-SIM] XML generado ({Bytes} bytes) para factura {FacturaId}",
            xmlString.Length, factura.Id);

        // Guardar archivo temporal para referencia
        var path = $"facturas/ventas/{factura.ClaveAcceso}.xml";
        await _fileStorage.SaveAsync(path, new MemoryStream(Encoding.UTF8.GetBytes(xmlString)));
        return xmlString; // Retornar contenido XML, no la ruta
    }

    public async Task<string> GenerarXmlNotaCreditoDebitoAsync(
        VentasFactura facturaOriginal,
        VentasNotaCreditoDebito nota,
        List<VentasNotaDetalle> detalles,
        ConfiguracionSRI config)
    {
        var esCredito = string.Equals(nota.TipoNota, "CREDITO", StringComparison.OrdinalIgnoreCase);
        var rootName  = esCredito ? "notaCredito" : "notaDebito";
        var infoName  = esCredito ? "infoNotaCredito" : "infoNotaDebito";

        var numDocSustento =
            $"{facturaOriginal.Establecimiento}-{facturaOriginal.PuntoEmision}-{facturaOriginal.Secuencial}";

        var infoTributaria = new XElement("infoTributaria",
            new XElement("ambiente", config.Ambiente),
            new XElement("tipoEmision", config.TipoEmision),
            new XElement("razonSocial", config.RazonSocial),
            new XElement("nombreComercial", config.NombreComercial ?? config.RazonSocial),
            new XElement("ruc", config.RucEmpresa),
            new XElement("claveAcceso", nota.ClaveAcceso),
            new XElement("codDoc", nota.TipoDocumento),
            new XElement("estab", nota.Establecimiento),
            new XElement("ptoEmi", nota.PuntoEmision),
            new XElement("secuencial", nota.Secuencial),
            new XElement("dirMatriz", config.DireccionMatriz));

        var infoPrincipal = new XElement(infoName,
            new XElement("fechaEmision", nota.FechaEmision.ToString("dd/MM/yyyy")),
            new XElement("dirEstablecimiento", config.DireccionMatriz),
            new XElement("tipoIdentificacionComprador", "04"),
            new XElement("razonSocialComprador", facturaOriginal.Cliente.LegalName),
            new XElement("identificacionComprador", facturaOriginal.Cliente.IdentificationNumber),
            new XElement("contribuyenteEspecial", ""),
            new XElement("obligadoContabilidad", config.ObligadoContabilidad ? "SI" : "NO"),
            new XElement("tipoEmision", config.TipoEmision),
            new XElement("rise", ""),
            new XElement("codDocModificado", facturaOriginal.TipoDocumento.Trim()),
            new XElement("numDocModificado", numDocSustento),
            new XElement("fechaEmisionDocSustento", facturaOriginal.FechaEmision.ToString("dd/MM/yyyy")),
            new XElement("motivo", nota.Motivo),
            new XElement("numAutDocSustento", facturaOriginal.NumeroAutorizacion ?? "0000000000"),
            new XElement("totalSinImpuestos", nota.Subtotal.ToString("F2")),
            new XElement("valorModificacion", nota.Total.ToString("F2")),
            new XElement("totalConImpuestos",
                new XElement("totalImpuesto",
                    new XElement("codigo", "2"),
                    new XElement("codigoPorcentaje", "2"),
                    new XElement("baseImponible", nota.Subtotal.ToString("F2")),
                    new XElement("valor", nota.Impuesto.ToString("F2")))),
            new XElement("moneda", "DOLAR"));

        var xmlDoc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(rootName,
                new XAttribute("id", "comprobante"),
                new XAttribute("version", "1.0.0"),
                infoTributaria,
                infoPrincipal,
                new XElement("detalles",
                    detalles.Select(d => new XElement("detalle",
                        new XElement("codigoInterno", d.ProductoId.ToString()),
                        new XElement("descripcion", d.Descripcion),
                        new XElement("cantidad", d.Cantidad.ToString("F4")),
                        new XElement("precioUnitario", d.PrecioUnitario.ToString("F6")),
                        new XElement("descuento", "0.00"),
                        new XElement("precioTotalSinImpuesto", d.Subtotal.ToString("F2")),
                        new XElement("impuestos",
                            new XElement("impuesto",
                                new XElement("codigo", "2"),
                                new XElement("codigoPorcentaje", "2"),
                                new XElement("tarifa", "12.00"),
                                new XElement("baseImponible", d.Subtotal.ToString("F2")),
                                new XElement("valor", d.Impuesto.ToString("F2")))))))
            ));

        var xmlString = xmlDoc.ToString();
        _logger.LogDebug("[SRI-SIM] XML {Root} generado para nota {NotaId}", rootName, nota.Id);

        var path = $"notas/ventas/{nota.ClaveAcceso}.xml";
        await _fileStorage.SaveAsync(path, new MemoryStream(Encoding.UTF8.GetBytes(xmlString)));
        return xmlString;
    }

    public Task<byte[]> FirmarXmlAsync(string xmlContent, string p12Path, string password)
    {
        _logger.LogDebug("[SRI-SIM] Firma digital simulada (P12 no aplicado)");
        // Simulación: retornar el mismo XML como bytes (sin firma real)
        return Task.FromResult(Encoding.UTF8.GetBytes(xmlContent));
    }

    public Task<SriAutorizacionResponse> EnviarAlSriAsync(byte[] xmlFirmado, string urlWsdl)
    {
        var numeroAuth = Guid.NewGuid().ToString("N")[..10].ToUpper();

        _logger.LogInformation(
            "[SRI-SIM] Autorización simulada generada: {NumeroAutorizacion}", numeroAuth);

        return Task.FromResult(new SriAutorizacionResponse
        {
            Autorizada         = true,
            NumeroAutorizacion = numeroAuth,
            FechaAutorizacion  = DateTime.UtcNow,
            XmlAutorizado      = Encoding.UTF8.GetString(xmlFirmado),
            MensajeError       = null,
        });
    }
}