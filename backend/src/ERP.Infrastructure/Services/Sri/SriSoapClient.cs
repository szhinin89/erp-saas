using System.Text;
using System.Xml;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Services.Sri;

/// <summary>
/// Cliente SOAP para los servicios SRI Ecuador de recepción y autorización.
/// Usa HttpClient puro (sin WCF) — sin dependencias adicionales.
/// Reutilizable desde cualquier contexto: API, Hangfire, CLI.
/// </summary>
public sealed class SriSoapClient
{
    private const string SoapAction  = "\"\"";
    private const string ContentType = "text/xml; charset=utf-8";

    // Namespaces WSDL SRI
    private const string NsRecepcion    = "http://ec.gob.sri.ws.recepcion";
    private const string NsAutorizacion = "http://ec.gob.sri.ws.autorizacion";
    private const string NsSoap        = "http://schemas.xmlsoap.org/soap/envelope/";

    private readonly IHttpClientFactory                _factory;
    private readonly ILogger<SriSoapClient>            _logger;

    public SriSoapClient(IHttpClientFactory factory, ILogger<SriSoapClient> logger)
    {
        _factory = factory;
        _logger  = logger;
    }

    // ── API pública ───────────────────────────────────────────────────────────

    /// <summary>
    /// Envía el XML firmado a RecepcionComprobantesOffline del SRI.
    /// Retorna estado RECIBIDA o DEVUELTA + lista de errores si aplica.
    /// </summary>
    public async Task<SriRecepcionResult> SendAsync(
        byte[]          signedXmlBytes,
        string          wsdlUrl,
        CancellationToken ct = default)
    {
        var endpointUrl = RecepcionEndpoint(wsdlUrl);
        var b64Xml      = Convert.ToBase64String(signedXmlBytes);
        var envelope    = BuildRecepcionEnvelope(b64Xml);

        _logger.LogDebug("[SRI] Enviando a recepción: {Url}", endpointUrl);

        var response = await PostSoapAsync(endpointUrl, envelope, ct);

        _logger.LogDebug("[SRI] Respuesta recepción ({Chars} chars)", response.Length);

        return ParseRecepcionResponse(response);
    }

    /// <summary>
    /// Consulta la autorización del comprobante en AutorizacionComprobantesOffline.
    /// Implementa un loop de polling con espera exponencial.
    /// </summary>
    public async Task<SriAutorizacionResult> CheckAuthorizationAsync(
        string          accessKey,
        string          wsdlUrl,
        int             maxAttempts  = 5,
        CancellationToken ct = default)
    {
        var endpointUrl = AutorizacionEndpoint(wsdlUrl);
        var envelope    = BuildAutorizacionEnvelope(accessKey);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            _logger.LogDebug("[SRI] Consultando autorización intento {N}/{Max}: {Key}", attempt, maxAttempts, accessKey);

            var response = await PostSoapAsync(endpointUrl, envelope, ct);
            var result   = ParseAutorizacionResponse(response);

            if (result.Estado is "AUTORIZADO" or "NO_AUTORIZADO")
                return result;

            // PENDIENTE o EN_PROCESO — esperar antes del siguiente intento
            if (attempt < maxAttempts)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt)); // 2s, 4s, 8s, 16s
                _logger.LogDebug("[SRI] Estado: {Estado} — esperando {Delay}s", result.Estado, delay.TotalSeconds);
                await Task.Delay(delay, ct);
            }
        }

        return new SriAutorizacionResult { Estado = "TIMEOUT", ErrorMessage = "El SRI no respondió tras varios reintentos." };
    }

    // ── Envelopes SOAP ────────────────────────────────────────────────────────

    private static string BuildRecepcionEnvelope(string base64Xml) =>
        $"""
         <soapenv:Envelope xmlns:soapenv="{NsSoap}" xmlns:ec="{NsRecepcion}">
           <soapenv:Header/>
           <soapenv:Body>
             <ec:validarComprobante>
               <xml>{base64Xml}</xml>
             </ec:validarComprobante>
           </soapenv:Body>
         </soapenv:Envelope>
         """;

    private static string BuildAutorizacionEnvelope(string accessKey) =>
        $"""
         <soapenv:Envelope xmlns:soapenv="{NsSoap}" xmlns:ec="{NsAutorizacion}">
           <soapenv:Header/>
           <soapenv:Body>
             <ec:autorizacionComprobante>
               <claveAccesoComprobante>{accessKey}</claveAccesoComprobante>
             </ec:autorizacionComprobante>
           </soapenv:Body>
         </soapenv:Envelope>
         """;

    // ── HTTP ──────────────────────────────────────────────────────────────────

    private async Task<string> PostSoapAsync(string url, string envelope, CancellationToken ct)
    {
        using var client  = _factory.CreateClient("sri");
        using var content = new StringContent(envelope, Encoding.UTF8, ContentType);
        content.Headers.TryAddWithoutValidation("SOAPAction", SoapAction);

        var response = await client.PostAsync(url, content, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }

    // ── Parsers de respuesta ──────────────────────────────────────────────────

    private static SriRecepcionResult ParseRecepcionResponse(string soap)
    {
        var doc = LoadXml(soap);
        var estado = NodeText(doc, "estado") ?? "DESCONOCIDO";

        var errores = doc.GetElementsByTagName("mensaje")
            .OfType<XmlElement>()
            .Select(m => $"[{NodeText(m, "identificador")}] {NodeText(m, "mensaje")}: {NodeText(m, "informacionAdicional")}")
            .ToList();

        return new SriRecepcionResult
        {
            Estado  = estado,
            Errores = errores,
        };
    }

    private static SriAutorizacionResult ParseAutorizacionResponse(string soap)
    {
        var doc = LoadXml(soap);

        var autorizacion = doc.GetElementsByTagName("autorizacion")
            .OfType<XmlElement>()
            .FirstOrDefault();

        if (autorizacion is null)
            return new SriAutorizacionResult { Estado = "SIN_RESPUESTA", ErrorMessage = "No se encontró el nodo <autorizacion>." };

        var estado   = NodeText(autorizacion, "estado")               ?? "DESCONOCIDO";
        var numero   = NodeText(autorizacion, "numeroAutorizacion")   ?? "";
        var fechaStr = NodeText(autorizacion, "fechaAutorizacion")    ?? "";
        var xmlComp  = NodeText(autorizacion, "comprobante")          ?? "";

        DateTime.TryParse(fechaStr, out var fecha);

        var mensajes = autorizacion.GetElementsByTagName("mensaje")
            .OfType<XmlElement>()
            .Select(m => $"[{NodeText(m, "identificador")}] {NodeText(m, "mensaje")}: {NodeText(m, "informacionAdicional")}")
            .ToList();

        return new SriAutorizacionResult
        {
            Estado              = estado,
            NumeroAutorizacion  = numero,
            FechaAutorizacion   = fecha,
            ComprobanteXml      = xmlComp,
            Mensajes            = mensajes,
            ErrorMessage        = estado != "AUTORIZADO" ? string.Join("; ", mensajes) : null,
        };
    }

    // ── URL helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// El WsdlUrl almacenado en SriSettings apunta a RecepcionComprobantesOffline.
    /// Quita el sufijo ?wsdl para obtener el endpoint SOAP real.
    /// </summary>
    private static string RecepcionEndpoint(string wsdlUrl)
        => wsdlUrl.TrimEnd('/').Replace("?wsdl", "", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Deriva la URL de autorización reemplazando "Recepcion" por "Autorizacion".
    /// Funciona tanto para pruebas (celcer) como producción (cel).
    /// </summary>
    private static string AutorizacionEndpoint(string wsdlUrl)
        => RecepcionEndpoint(wsdlUrl)
            .Replace("RecepcionComprobantesOffline", "AutorizacionComprobantesOffline",
                     StringComparison.OrdinalIgnoreCase);

    // ── XML utils ─────────────────────────────────────────────────────────────

    private static XmlDocument LoadXml(string xml)
    {
        var doc = new XmlDocument();
        doc.LoadXml(xml);
        return doc;
    }

    private static string? NodeText(XmlNode parent, string localName)
        => parent.SelectSingleNode($".//*[local-name()='{localName}']")?.InnerText?.Trim();
}

// ── DTOs de resultado ─────────────────────────────────────────────────────────

public sealed class SriRecepcionResult
{
    public string       Estado  { get; init; } = "";
    public List<string> Errores { get; init; } = new();
    public bool         Recibida => Estado.Equals("RECIBIDA", StringComparison.OrdinalIgnoreCase);
}

public sealed class SriAutorizacionResult
{
    public string       Estado             { get; init; } = "";
    public string       NumeroAutorizacion { get; init; } = "";
    public DateTime     FechaAutorizacion  { get; init; }
    public string       ComprobanteXml     { get; init; } = "";
    public List<string> Mensajes           { get; init; } = new();
    public string?      ErrorMessage       { get; set;  }
    public bool         Autorizado => Estado.Equals("AUTORIZADO", StringComparison.OrdinalIgnoreCase);
}
