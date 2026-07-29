using System.Text;
using System.Xml;
using ERP.Application.Common.Config;
using ERP.Domain.Modules.ElectronicDocuments.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ERP.Infrastructure.Services.Sri;

/// <summary>
/// Cliente SOAP para los servicios SRI Ecuador de recepción y autorización.
/// Usa HttpClient puro (sin WCF) — sin dependencias adicionales.
/// Reutilizable desde cualquier contexto: API, Hangfire, CLI.
/// </summary>
public sealed partial class SriSoapClient
{
    private const string SoapAction = "\"\"";

    // Solo el media type: StringContent(string, Encoding, string) construye el header
    // "Content-Type: text/xml; charset=utf-8" a partir de Encoding — pasar aquí el charset ya
    // incluido produce un valor duplicado/mal formado y StringContent lanza FormatException en
    // cada llamada (defecto real confirmado por SriSoapClientTests — Fase 9, nunca antes
    // ejercitado porque SendAsync/CheckAuthorizationAsync no estaban conectados a ningún flujo).
    private const string ContentType = "text/xml";

    // Namespaces WSDL SRI
    private const string NsRecepcion = "http://ec.gob.sri.ws.recepcion";
    private const string NsAutorizacion = "http://ec.gob.sri.ws.autorizacion";
    private const string NsSoap = "http://schemas.xmlsoap.org/soap/envelope/";

    private readonly IHttpClientFactory _factory;
    private readonly ILogger<SriSoapClient> _logger;
    private readonly SriPollingOptions _pollingOptions;

    public SriSoapClient(
        IHttpClientFactory factory,
        ILogger<SriSoapClient> logger,
        IOptions<SriPollingOptions>? pollingOptions = null
    )
    {
        _factory = factory;
        _logger = logger;
        _pollingOptions = pollingOptions?.Value ?? new SriPollingOptions();
    }

    // ── API pública ───────────────────────────────────────────────────────────

    /// <summary>
    /// Envía el XML firmado a RecepcionComprobantesOffline del SRI.
    /// Retorna estado RECIBIDA o DEVUELTA + lista de errores si aplica.
    /// </summary>
    public async Task<SriRecepcionResult> SendAsync(
        byte[] signedXmlBytes,
        string wsdlUrl,
        CancellationToken cancellationToken = default
    )
    {
        var endpointUrl = RecepcionEndpoint(wsdlUrl);
        var b64Xml = Convert.ToBase64String(signedXmlBytes);
        var envelope = BuildRecepcionEnvelope(b64Xml);

        LogSriSending(endpointUrl);

        string? response;
        try
        {
            response = await PostSoapAsync(endpointUrl, envelope, cancellationToken);
        }
        catch (SriSoapFaultException ex)
        {
            return new SriRecepcionResult
            {
                Status = "ERROR_SOAP_FAULT",
                Errors = [ex.Fault.ToDisplayMessage()],
            };
        }

        if (response is null)
        {
            return new SriRecepcionResult
            {
                Status = "ERROR_CONEXION",
                Errors =
                [
                    "No se pudo contactar al servicio de recepción del SRI. Verifique la conectividad y la URL configurada.",
                ],
            };
        }

        LogSriRecepcionResponse(response.Length);

        try
        {
            return ParseRecepcionResponse(response);
        }
        catch (XmlException ex)
        {
            // Defecto real confirmado por auditoría (Fase 8): un cuerpo no-XML (p.ej. una
            // página de error de un proxy/WAF delante del WSDL) hacía que XmlDocument.LoadXml
            // lanzara sin que nada en el pipeline lo capturara — violaba la regla de nunca
            // propagar excepciones técnicas hacia los módulos consumidores.
            LogSriMalformedResponse(endpointUrl, ex.Message);
            return new SriRecepcionResult
            {
                Status = "ERROR_RESPUESTA_INVALIDA",
                Errors =
                [
                    "El SRI respondió con un contenido que no pudo interpretarse como XML válido.",
                ],
            };
        }
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "[SRI] Respuesta de recepción no interpretable como XML desde {Url}: {Reason}"
    )]
    private partial void LogSriMalformedResponse(string url, string reason);

    /// <summary>
    /// Consulta la autorización del comprobante en AutorizacionComprobantesOffline. Espera
    /// <see cref="SriPollingOptions.InitialAuthorizationDelaySeconds"/> (configurable, appsettings
    /// sección "SriPolling") antes del primer intento — el SRI recomienda dar tiempo antes de
    /// consultar tras el envío — y luego implementa un loop de polling con espera exponencial
    /// entre reintentos (2s, 4s, 8s, 16s).
    /// </summary>
    public async Task<SriAutorizacionResult> CheckAuthorizationAsync(
        string accessKey,
        string wsdlUrl,
        int maxAttempts = 5,
        CancellationToken cancellationToken = default
    )
    {
        var endpointUrl = AutorizacionEndpoint(wsdlUrl);
        var envelope = BuildAutorizacionEnvelope(accessKey);

        if (_pollingOptions.InitialAuthorizationDelaySeconds > 0)
        {
            LogSriInitialDelay(_pollingOptions.InitialAuthorizationDelaySeconds);
            await Task.Delay(
                TimeSpan.FromSeconds(_pollingOptions.InitialAuthorizationDelaySeconds),
                cancellationToken
            );
        }

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            LogSriCheckingAuthorization(attempt, maxAttempts, accessKey);

            string? response;
            try
            {
                response = await PostSoapAsync(endpointUrl, envelope, cancellationToken);
            }
            catch (SriSoapFaultException ex)
            {
                return new SriAutorizacionResult
                {
                    Status = "ERROR_SOAP_FAULT",
                    ErrorMessage = ex.Fault.ToDisplayMessage(),
                };
            }

            if (response is null)
            {
                return new SriAutorizacionResult
                {
                    Status = "ERROR_CONEXION",
                    ErrorMessage =
                        "No se pudo contactar al servicio de autorización del SRI. Verifique la conectividad y la URL configurada.",
                };
            }

            SriAutorizacionResult result;
            try
            {
                result = ParseAutorizacionResponse(response);
            }
            catch (XmlException ex)
            {
                // Mismo defecto real que en SendAsync (Fase 8), confirmado por auditoría (Fase 9):
                // ParseAutorizacionResponse no estaba protegido dentro del loop de polling.
                LogSriMalformedResponse(endpointUrl, ex.Message);
                return new SriAutorizacionResult
                {
                    Status = "ERROR_RESPUESTA_INVALIDA",
                    ErrorMessage =
                        "El SRI respondió con un contenido que no pudo interpretarse como XML válido.",
                };
            }

            // EST-01v2 (rechazo real del SRI, 2026-07-11): la suposición anterior ("el WS solo
            // devuelve AUTORIZADO o RECHAZADO") resultó falsa contra el ambiente de Pruebas real
            // — el literal que el SRI realmente envía es "NO AUTORIZADO" (con espacio, sin guion
            // bajo, sin la palabra "RECHAZADO"). Sin este fix, "NO AUTORIZADO" no coincidía con
            // ninguna de las dos ramas, se interpretaba como PENDIENTE/EN_PROCESO, se agotaban
            // los 5 intentos de polling y el documento terminaba en TIMEOUT en vez de Rejected —
            // perdiendo el motivo real de rechazo (ver <mensajes> en la respuesta) y dejando el
            // documento reintentándose indefinidamente contra un rechazo ya definitivo del SRI.
            if (result.Status is "AUTORIZADO" or "NO AUTORIZADO")
                return result;

            // PENDIENTE o EN_PROCESO — esperar antes del siguiente intento
            if (attempt < maxAttempts)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt)); // 2s, 4s, 8s, 16s
                LogSriWaiting(result.Status, delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
            }
        }

        return new SriAutorizacionResult
        {
            Status = "TIMEOUT",
            ErrorMessage = "El SRI no respondió tras varios reintentos.",
        };
    }

    /// <summary>
    /// Verifica solo que el endpoint del WSDL responda — no envía ni consulta ningún
    /// comprobante. Usado por el diagnóstico "Validar configuración", nunca por el flujo
    /// de emisión real (que sigue usando <see cref="SendAsync"/>/<see cref="CheckAuthorizationAsync"/>).
    /// </summary>
    public async Task<bool> PingAsync(string wsdlUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = _factory.CreateClient("sri");
            using var response = await client.GetAsync(wsdlUrl, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            LogSriPingFailed(wsdlUrl, ex.Message);
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "[SRI] Ping falló para {Url}: {Reason}")]
    private partial void LogSriPingFailed(string url, string reason);

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

    /// <summary>
    /// Postea el envelope SOAP y devuelve el cuerpo de la respuesta, o <c>null</c> si la
    /// solicitud no pudo completarse (falla de red o timeout del HttpClient "sri", 20s —
    /// ver <c>DependencyInjection.cs</c>). Nunca deja escapar una excepción de HTTP sin
    /// capturar: los llamadores (<see cref="SendAsync"/>/<see cref="CheckAuthorizationAsync"/>)
    /// traducen un <c>null</c> a un resultado tipado con estado "ERROR_CONEXION" y un mensaje
    /// claro, en vez de propagar una excepción técnica. Una cancelación solicitada por el propio
    /// llamador (<paramref name="cancellationToken"/>) sigue propagándose normalmente — no se
    /// confunde con un timeout de red.
    /// </summary>
    /// <summary>
    /// Devuelve el cuerpo de la respuesta si el POST fue exitoso; <c>null</c> si la solicitud
    /// no pudo completarse (falla de red o timeout del HttpClient "sri", 20s — ver
    /// <c>DependencyInjection.cs</c>), traducido por los llamadores a "ERROR_CONEXION". Un
    /// HTTP no-2xx cuyo cuerpo sea un SOAP Fault real (`&lt;soap:Fault&gt;`) se distingue
    /// explícitamente: lanza <see cref="SriSoapFaultException"/> con faultcode/faultstring/detail
    /// reales del SRI, en vez de colapsarlo en "ERROR_CONEXION" — los llamadores lo capturan y
    /// lo traducen a un resultado tipado con el mensaje real. Una cancelación solicitada por el
    /// propio llamador (<paramref name="cancellationToken"/>) sigue propagándose normalmente.
    /// </summary>
    /// <summary>Reintentos ante fallo transitorio de red/timeout del HttpClient "sri" — nunca ante un SOAP Fault real (esa es una respuesta definitiva del SRI, no un fallo de transporte).</summary>
    private const int HttpRetryAttempts = 3;

    private async Task<string?> PostSoapAsync(
        string url,
        string envelope,
        CancellationToken cancellationToken
    )
    {
        for (var attempt = 1; attempt <= HttpRetryAttempts; attempt++)
        {
            try
            {
                using var client = _factory.CreateClient("sri");
                using var content = new StringContent(envelope, Encoding.UTF8, ContentType);
                content.Headers.TryAddWithoutValidation("SOAPAction", SoapAction);

                using var response = await client.PostAsync(url, content, cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.IsSuccessStatusCode)
                    return body;

                if (TryParseSoapFault(body, out var fault))
                {
                    LogSriSoapFault(url, fault!.FaultCode, fault.FaultString);
                    throw new SriSoapFaultException(fault);
                }

                LogSriHttpRequestFailed(
                    url,
                    $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}"
                );
                return null;
            }
            catch (HttpRequestException ex)
            {
                if (attempt == HttpRetryAttempts)
                {
                    LogSriHttpRequestFailed(url, ex.Message);
                    return null;
                }
                LogSriRetryingAfterTransientFailure(attempt, HttpRetryAttempts, url, ex.Message);
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                // TaskCanceledException con el token del llamador NO cancelado: es el timeout
                // del HttpClient "sri" (20s), no una cancelación explícita — se reintenta igual
                // que una falla de red, nunca se propaga como excepción técnica sin capturar.
                if (attempt == HttpRetryAttempts)
                {
                    LogSriRequestTimedOut(url, ex.Message);
                    return null;
                }
                LogSriRetryingAfterTransientFailure(attempt, HttpRetryAttempts, url, ex.Message);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500 * attempt), cancellationToken);
        }

        return null;
    }

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "[SRI] Fallo transitorio en intento {Attempt}/{Max} hacia {Url}: {Reason} — reintentando"
    )]
    private partial void LogSriRetryingAfterTransientFailure(
        int attempt,
        int max,
        string url,
        string reason
    );

    /// <summary>
    /// Intenta interpretar el cuerpo de una respuesta HTTP no-2xx como un SOAP 1.1 Fault
    /// (`&lt;soap:Fault&gt;&lt;faultcode&gt;/&lt;faultstring&gt;/&lt;detail&gt;`). Devuelve
    /// <c>false</c> (sin lanzar) si el cuerpo no es XML o no contiene un nodo Fault — en ese
    /// caso el llamador trata la respuesta como una falla de transporte genérica.
    /// </summary>
    private static bool TryParseSoapFault(string? body, out SriSoapFault? fault)
    {
        fault = null;
        if (string.IsNullOrWhiteSpace(body))
            return false;

        XmlDocument doc;
        try
        {
            doc = LoadXml(body);
        }
        catch (XmlException)
        {
            return false;
        }

        var faultNode = doc.SelectSingleNode("//*[local-name()='Fault']");
        if (faultNode is null)
            return false;

        var detailNode = faultNode.SelectSingleNode("*[local-name()='detail']");
        fault = new SriSoapFault(
            FaultCode: NodeText(faultNode, "faultcode"),
            FaultString: NodeText(faultNode, "faultstring"),
            Detail: string.IsNullOrWhiteSpace(detailNode?.InnerXml)
                ? null
                : detailNode.InnerXml.Trim()
        );
        return true;
    }

    // ── Parsers de respuesta ──────────────────────────────────────────────────

    private static SriRecepcionResult ParseRecepcionResponse(string soap)
    {
        var doc = LoadXml(soap);
        var status = NodeText(doc, "estado") ?? "DESCONOCIDO";

        var mensajeNodes = SelectMensajeNodes(doc);

        var errors = mensajeNodes
            .Select(m =>
                $"[{NodeText(m, "identificador")}] {NodeText(m, "mensaje")}: {NodeText(m, "informacionAdicional")}"
            )
            .ToList();

        var structuredMessages = mensajeNodes.Select(ToSriMessage).ToList();

        return new SriRecepcionResult
        {
            Status = status,
            Errors = errors,
            StructuredMessages = structuredMessages,
        };
    }

    /// <summary>
    /// Selecciona únicamente los nodos <c>&lt;mensaje&gt;</c> que son hijos directos de un
    /// contenedor <c>&lt;mensajes&gt;</c> — el esquema del SRI reutiliza el mismo nombre de
    /// etiqueta ("mensaje") para el contenedor de un mensaje individual Y para uno de sus
    /// campos internos (el texto del mensaje en sí). Usar <c>GetElementsByTagName("mensaje")</c>
    /// sobre todo el documento (defecto real encontrado al construir el diagnóstico SRI
    /// estructurado — ADR-024) capturaba ambos, produciendo un segundo mensaje fantasma vacío
    /// por cada mensaje real. Restringir a hijos directos de <c>&lt;mensajes&gt;</c> elimina la
    /// ambigüedad sin depender de la profundidad del árbol.
    /// </summary>
    private static List<XmlElement> SelectMensajeNodes(XmlNode scope) =>
        scope
            .SelectNodes(".//*[local-name()='mensajes']/*[local-name()='mensaje']")!
            .OfType<XmlElement>()
            .ToList();

    /// <summary>
    /// Traduce un nodo <c>&lt;mensaje&gt;</c> SRI a <see cref="SriMessage"/> sin resumir ni
    /// traducir ningún campo. <see cref="SriMessage.MessageType"/> usa el literal de
    /// <c>&lt;tipo&gt;</c> tal cual; si el nodo no lo trae (algunas respuestas de recepción no
    /// lo incluyen), se usa <c>"ERROR"</c> como único fallback documentado — nunca se inventa un
    /// código o mensaje que el SRI no envió.
    /// </summary>
    private static SriMessage ToSriMessage(XmlElement mensaje) =>
        new(
            Code: NodeText(mensaje, "identificador"),
            MessageType: NodeText(mensaje, "tipo") ?? "ERROR",
            Message: NodeText(mensaje, "mensaje") ?? "",
            AdditionalInfo: NodeText(mensaje, "informacionAdicional")
        );

    private static SriAutorizacionResult ParseAutorizacionResponse(string soap)
    {
        var doc = LoadXml(soap);

        var autorizacion = doc.GetElementsByTagName("autorizacion")
            .OfType<XmlElement>()
            .FirstOrDefault();

        if (autorizacion is null)
            return new SriAutorizacionResult
            {
                Status = "SIN_RESPUESTA",
                ErrorMessage = "No se encontró el nodo <autorizacion>.",
            };

        var status = NodeText(autorizacion, "estado") ?? "DESCONOCIDO";
        var number = NodeText(autorizacion, "numeroAutorizacion") ?? "";
        var dateStr = NodeText(autorizacion, "fechaAutorizacion") ?? "";
        var xmlDoc = NodeText(autorizacion, "comprobante") ?? "";

        // FECHA-01 (rechazo real del SRI 2026-07-11, segunda ronda — este bug se manifestó recién
        // tras la primera AUTORIZADO real): DateTime.TryParse sin estilos, al recibir un offset
        // explícito (p.ej. "2026-07-11T14:38:35-05:00", formato real de <fechaAutorizacion>),
        // devuelve un DateTime con Kind=Local — Npgsql rechaza escribir Kind=Local en una columna
        // "timestamp with time zone" ("solo UTC"), lanzando DbUpdateException. El SRI ya había
        // autorizado el comprobante realmente, pero MarkAuthorized nunca llegaba a persistirse:
        // el catch genérico de ElectronicDocumentIssuer.RunPipelineAsync silenciaba la excepción
        // y el documento quedaba indefinidamente en Received pese a estar autorizado en el SRI.
        // AdjustToUniversal convierte el offset real a UTC (dato correcto, nunca se pierde
        // información); AssumeUniversal solo aplica si el SRI llegara a omitir el offset.
        _ = DateTime.TryParse(
            dateStr,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AdjustToUniversal
                | System.Globalization.DateTimeStyles.AssumeUniversal,
            out var authDate
        );

        var mensajeNodes = SelectMensajeNodes(autorizacion);

        var messages = mensajeNodes
            .Select(m =>
                $"[{NodeText(m, "identificador")}] {NodeText(m, "mensaje")}: {NodeText(m, "informacionAdicional")}"
            )
            .ToList();

        var structuredMessages = mensajeNodes.Select(ToSriMessage).ToList();

        return new SriAutorizacionResult
        {
            Status = status,
            AuthorizationNumber = number,
            AuthorizationDate = authDate,
            DocumentXml = xmlDoc,
            Messages = messages,
            StructuredMessages = structuredMessages,
            ErrorMessage = status != "AUTORIZADO" ? string.Join("; ", messages) : null,
        };
    }

    // ── URL helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// El WsdlUrl almacenado en SriSettings apunta a RecepcionComprobantesOffline.
    /// Quita el sufijo ?wsdl para obtener el endpoint SOAP real.
    /// </summary>
    private static string RecepcionEndpoint(string wsdlUrl) =>
        wsdlUrl.TrimEnd('/').Replace("?wsdl", "", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Deriva la URL de autorización reemplazando "Recepcion" por "Autorizacion".
    /// Funciona tanto para pruebas (celcer) como producción (cel).
    /// </summary>
    private static string AutorizacionEndpoint(string wsdlUrl) =>
        RecepcionEndpoint(wsdlUrl)
            .Replace(
                "RecepcionComprobantesOffline",
                "AutorizacionComprobantesOffline",
                StringComparison.OrdinalIgnoreCase
            );

    // ── XML utils ─────────────────────────────────────────────────────────────

    private static XmlDocument LoadXml(string xml)
    {
        var doc = new XmlDocument();
        doc.LoadXml(xml);
        return doc;
    }

    private static string? NodeText(XmlNode parent, string localName) =>
        parent.SelectSingleNode($".//*[local-name()='{localName}']")?.InnerText?.Trim();

    [LoggerMessage(Level = LogLevel.Debug, Message = "[SRI] Enviando a recepción: {Url}")]
    private partial void LogSriSending(string url);

    [LoggerMessage(Level = LogLevel.Debug, Message = "[SRI] Respuesta recepción ({Chars} chars)")]
    private partial void LogSriRecepcionResponse(int chars);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "[SRI] Consultando autorización intento {N}/{Max}: {Key}"
    )]
    private partial void LogSriCheckingAuthorization(int n, int max, string key);

    [LoggerMessage(Level = LogLevel.Debug, Message = "[SRI] Estado: {Estado} — esperando {Delay}s")]
    private partial void LogSriWaiting(string estado, double delay);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "[SRI] Fallo de red al llamar a {Url}: {Reason}"
    )]
    private partial void LogSriHttpRequestFailed(string url, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[SRI] Timeout al llamar a {Url}: {Reason}")]
    private partial void LogSriRequestTimedOut(string url, string reason);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "[SRI] SOAP Fault recibido desde {Url}: faultcode={FaultCode} faultstring={FaultString}"
    )]
    private partial void LogSriSoapFault(string url, string? faultCode, string? faultString);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "[SRI] Esperando {Seconds}s antes de la primera consulta de autorización"
    )]
    private partial void LogSriInitialDelay(int seconds);
}

/// <summary>SOAP 1.1 Fault real del SRI — faultcode/faultstring/detail, nunca colapsado en un error de transporte genérico.</summary>
public sealed record SriSoapFault(string? FaultCode, string? FaultString, string? Detail)
{
    /// <summary>Mensaje legible para exponer en Errors/ErrorMessage — el texto real que devolvió el SRI, no un genérico.</summary>
    public string ToDisplayMessage()
    {
        var text =
            $"[SOAP Fault{(string.IsNullOrWhiteSpace(FaultCode) ? "" : $" {FaultCode}")}] {FaultString ?? "El SRI devolvió un error SOAP sin mensaje."}";
        return string.IsNullOrWhiteSpace(Detail) ? text : $"{text} — {Detail}";
    }
}

internal sealed class SriSoapFaultException : Exception
{
    public SriSoapFault Fault { get; }

    public SriSoapFaultException(SriSoapFault fault)
        : base(fault.ToDisplayMessage())
    {
        Fault = fault;
    }
}

// ── DTOs de resultado ─────────────────────────────────────────────────────────

public sealed class SriRecepcionResult
{
    public string Status { get; init; } = "";
    public List<string> Errors { get; init; } = new();

    /// <summary>Mismos mensajes de <see cref="Errors"/>, sin aplanar — código/tipo/mensaje/información adicional por separado.</summary>
    public List<SriMessage> StructuredMessages { get; init; } = new();
    public bool Received => Status.Equals("RECIBIDA", StringComparison.OrdinalIgnoreCase);
}

public sealed class SriAutorizacionResult
{
    public string Status { get; init; } = "";
    public string AuthorizationNumber { get; init; } = "";
    public DateTime AuthorizationDate { get; init; }
    public string DocumentXml { get; init; } = "";
    public List<string> Messages { get; init; } = new();

    /// <summary>Mismos mensajes de <see cref="Messages"/>, sin aplanar — código/tipo/mensaje/información adicional por separado.</summary>
    public List<SriMessage> StructuredMessages { get; init; } = new();
    public string? ErrorMessage { get; set; }
    public bool Authorized => Status.Equals("AUTORIZADO", StringComparison.OrdinalIgnoreCase);
}
