using System.Text;
using Microsoft.Extensions.Logging;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Infrastructure.Services.Sri;

namespace ERP.Infrastructure.Services;

/// <summary>
/// Implementación real del servicio SRI Ecuador.
/// Genera XML v1.1.0, firma con XAdES-BES y envía via SOAP.
/// Independiente de cualquier frontend — consumible desde API, Hangfire, CLI o tests.
/// </summary>
public sealed class SriElectronicInvoiceRealService : ISriElectronicInvoiceService
{
    private readonly SriSoapClient                             _soap;
    private readonly IFileStorage                              _fileStorage;
    private readonly ILogger<SriElectronicInvoiceRealService> _logger;

    public SriElectronicInvoiceRealService(
        SriSoapClient                             soap,
        IFileStorage                              fileStorage,
        ILogger<SriElectronicInvoiceRealService> logger)
    {
        _soap        = soap;
        _fileStorage = fileStorage;
        _logger      = logger;
    }

    // ── Generación XML ────────────────────────────────────────────────────────

    public Task<string> GenerateInvoiceXmlAsync(
        SalesBill salesBill,
        List<SalesBillLine> lines,
        SriSettings         config,
        Company             company)
    {
        _logger.LogDebug("[SRI-REAL] Generando XML salesBill {Id} clave {Key}", salesBill.Id, salesBill.AccessKey);
        var xml = SriXmlInvoiceBuilder.BuildFactura(salesBill, lines, config, company);
        return Task.FromResult(xml);
    }

    public Task<string> GenerateCreditDebitNoteXmlAsync(
        SalesBill originalBill,
        SalesNote note,
        List<SalesNoteLine> lines,
        SriSettings         config,
        Company             company)
    {
        _logger.LogDebug("[SRI-REAL] Generando XML note {Tipo} {Id}", note.NoteType, note.Id);
        var xml = SriXmlInvoiceBuilder.BuildNotaCreditoDebito(originalBill, note, lines, config, company);
        return Task.FromResult(xml);
    }

    // ── Firma XAdES-BES ───────────────────────────────────────────────────────

    public Task<byte[]> SignXmlAsync(string xmlContent, string p12Path, string password)
    {
        _logger.LogDebug("[SRI-REAL] Firmando XML con certificado {P12}", p12Path);

        if (!File.Exists(p12Path))
            throw new FileNotFoundException($"Certificado P12 no encontrado: {p12Path}");

        var signer     = new XadesBesSigner();
        var signedBytes = signer.Sign(xmlContent, p12Path, password);

        _logger.LogDebug("[SRI-REAL] XML firmado ({Bytes} bytes)", signedBytes.Length);
        return Task.FromResult(signedBytes);
    }

    // ── Envío + polling autorización ──────────────────────────────────────────

    public async Task<SriAuthorizationResponse> SendToSriAsync(
        byte[] signedXml, string urlWsdl)
    {
        _logger.LogInformation("[SRI-REAL] Enviando a {Url}", urlWsdl);

        // Paso 1 — Recepción
        var recepcion = await _soap.SendAsync(signedXml, urlWsdl);

        if (!recepcion.Received)
        {
            var err = string.Join("; ", recepcion.Errors);
            _logger.LogWarning("[SRI-REAL] Recepción DEVUELTA: {Err}", err);
            return Failure(err);
        }

        _logger.LogDebug("[SRI-REAL] Recepción OK — consultando autorización");

        // Paso 2 — Autorización (polling)
        var accessKey    = ExtractAccessKey(Encoding.UTF8.GetString(signedXml));
        var autorizacion = await _soap.CheckAuthorizationAsync(accessKey, urlWsdl);

        if (!autorizacion.Authorized)
        {
            var err = autorizacion.ErrorMessage ?? "SRI no autorizó el comprobante.";
            _logger.LogWarning("[SRI-REAL] No autorizado: {Err}", err);
            return Failure(err);
        }

        _logger.LogInformation("[SRI-REAL] Autorizado: {Num} fecha {Fecha}",
            autorizacion.AuthorizationNumber, autorizacion.AuthorizationDate);

        return new SriAuthorizationResponse
        {
            IsAuthorized  = true,
            AuthNumber    = autorizacion.AuthorizationNumber,
            AuthDate      = autorizacion.AuthorizationDate,
            AuthorizedXml = autorizacion.DocumentXml,
            ErrorMessage  = null,
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static SriAuthorizationResponse Failure(string msg) =>
        new() { IsAuthorized = false, ErrorMessage = msg, AuthorizedXml = "" };

    /// <summary>
    /// Extrae la claveAcceso (49 dígitos) del XML firmado.
    /// El elemento puede aparecer como &lt;claveAcceso&gt; o &lt;accessKey&gt; (legado).
    /// </summary>
    private static string ExtractAccessKey(string xmlUtf8)
    {
        var doc = new System.Xml.XmlDocument();
        doc.LoadXml(xmlUtf8);
        var node = doc.SelectSingleNode("//*[local-name()='claveAcceso']")
                ?? doc.SelectSingleNode("//*[local-name()='accessKey']");
        return node?.InnerText?.Trim() ?? throw new InvalidOperationException("claveAcceso no encontrada en el XML.");
    }
}
