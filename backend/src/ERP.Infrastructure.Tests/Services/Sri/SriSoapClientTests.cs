using ERP.Application.Common.Config;
using ERP.Infrastructure.Services.Sri;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace ERP.Infrastructure.Tests.Services.Sri;

/// <summary>
/// Fase 9 — endurecimiento de <see cref="SriSoapClient"/>: verifica que una falla de red
/// (servidor caído, DNS inexistente, etc.) nunca deja escapar una <see cref="HttpRequestException"/>
/// sin capturar, sino que se traduce en un resultado tipado con estado "ERROR_CONEXION" y un
/// mensaje claro. No conecta este cliente a ningún flujo real (eso queda para una fase
/// posterior) — solo ejercita <see cref="SriSoapClient"/> de forma aislada.
/// </summary>
public sealed class SriSoapClientTests
{
    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("Simulated connection failure — no route to host.");
    }

    private sealed class FakeHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        public FakeHttpClientFactory(HttpMessageHandler handler) => _handler = handler;
        public HttpClient CreateClient(string name) => new(_handler);
    }

    /// <summary>Cuenta las invocaciones — usado para verificar el número real de reintentos HTTP (Fase 6).</summary>
    private sealed class CountingThrowingHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            throw new HttpRequestException("Simulated connection failure — no route to host.");
        }
    }

    /// <summary>Falla con un error transitorio de red las primeras <paramref name="failuresBeforeSuccess"/> veces, luego responde con éxito — simula una caída puntual de red que un reintento SÍ debe recuperar.</summary>
    private sealed class FlakyThenRespondingHandler : HttpMessageHandler
    {
        private readonly int _failuresBeforeSuccess;
        private readonly string _body;
        private int _calls;

        public FlakyThenRespondingHandler(int failuresBeforeSuccess, string body)
        {
            _failuresBeforeSuccess = failuresBeforeSuccess;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _calls++;
            if (_calls <= _failuresBeforeSuccess)
                throw new HttpRequestException("Simulated transient connection failure.");

            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(_body, System.Text.Encoding.UTF8, "text/xml"),
            });
        }
    }

    private sealed class RespondingHandler : HttpMessageHandler
    {
        private readonly string _body;
        private readonly System.Net.HttpStatusCode _statusCode;
        public RespondingHandler(string body, System.Net.HttpStatusCode statusCode = System.Net.HttpStatusCode.OK)
        {
            _body = body;
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_body, System.Text.Encoding.UTF8, "text/xml"),
            });
    }

    private static SriSoapClient BuildClient(HttpMessageHandler handler)
        => new(new FakeHttpClientFactory(handler), NullLogger<SriSoapClient>.Instance);

    private static SriSoapClient BuildClient(HttpMessageHandler handler, SriPollingOptions? pollingOptions)
        => new(new FakeHttpClientFactory(handler), NullLogger<SriSoapClient>.Instance,
            pollingOptions is null ? null : Microsoft.Extensions.Options.Options.Create(pollingOptions));

    [Fact]
    public async Task SendAsync_on_network_failure_returns_connection_error_status_never_throws()
    {
        var client = BuildClient(new ThrowingHandler());

        var result = await client.SendAsync([1, 2, 3], "https://celcer.sri.gob.ec/fake?wsdl");

        result.Status.Should().Be("ERROR_CONEXION");
        result.Errors.Should().ContainSingle();
        result.Received.Should().BeFalse();
    }

    /// <summary>
    /// Fase 6 (auditoría SRI): antes de este fix, un único fallo transitorio de red (p.ej. un
    /// timeout puntual del lado del SRI) descartaba el intento de inmediato — sin ningún
    /// reintento HTTP. Verifica que una falla que se recupera en el segundo intento SÍ termina
    /// en éxito, no en ERROR_CONEXION.
    /// </summary>
    [Fact]
    public async Task SendAsync_recovers_after_one_transient_network_failure()
    {
        const string soap = """
            <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
              <soap:Body>
                <ns2:validarComprobanteResponse xmlns:ns2="http://ec.gob.sri.ws.recepcion">
                  <RespuestaRecepcionComprobante>
                    <estado>RECIBIDA</estado>
                    <comprobantes/>
                  </RespuestaRecepcionComprobante>
                </ns2:validarComprobanteResponse>
              </soap:Body>
            </soap:Envelope>
            """;
        var handler = new FlakyThenRespondingHandler(failuresBeforeSuccess: 1, soap);
        var client = BuildClient(handler);

        var result = await client.SendAsync([1, 2, 3], "https://celcer.sri.gob.ec/fake?wsdl");

        result.Status.Should().Be("RECIBIDA");
        result.Received.Should().BeTrue();
    }

    /// <summary>Confirma el número real de intentos (no solo el resultado final) ante una falla persistente.</summary>
    [Fact]
    public async Task SendAsync_on_persistent_network_failure_retries_exactly_the_configured_number_of_attempts()
    {
        var handler = new CountingThrowingHandler();
        var client = BuildClient(handler);

        var result = await client.SendAsync([1, 2, 3], "https://celcer.sri.gob.ec/fake?wsdl");

        result.Status.Should().Be("ERROR_CONEXION");
        handler.Calls.Should().Be(3);
    }

    [Fact]
    public async Task CheckAuthorizationAsync_on_network_failure_returns_connection_error_status_never_throws()
    {
        var client = BuildClient(new ThrowingHandler());

        var result = await client.CheckAuthorizationAsync("0".PadLeft(49, '0'), "https://celcer.sri.gob.ec/fake?wsdl");

        result.Status.Should().Be("ERROR_CONEXION");
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        result.Authorized.Should().BeFalse();
    }

    [Fact]
    public async Task SendAsync_on_malformed_non_xml_response_returns_invalid_response_status_never_throws()
    {
        // Defecto real confirmado por auditoría (Fase 8): un cuerpo no-XML (p.ej. una página de
        // error de texto plano de un proxy/WAF) hacía que XmlDocument.LoadXml lanzara
        // XmlException sin capturar.
        var client = BuildClient(new RespondingHandler("502 Bad Gateway - upstream connection reset"));

        var result = await client.SendAsync([1, 2, 3], "https://celcer.sri.gob.ec/fake?wsdl");

        result.Status.Should().Be("ERROR_RESPUESTA_INVALIDA");
        result.Errors.Should().ContainSingle();
        result.Received.Should().BeFalse();
    }

    [Fact]
    public async Task SendAsync_on_recibida_response_parses_status_correctly()
    {
        const string soap = """
            <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
              <soap:Body>
                <ns2:validarComprobanteResponse xmlns:ns2="http://ec.gob.sri.ws.recepcion">
                  <RespuestaRecepcionComprobante>
                    <estado>RECIBIDA</estado>
                    <comprobantes/>
                  </RespuestaRecepcionComprobante>
                </ns2:validarComprobanteResponse>
              </soap:Body>
            </soap:Envelope>
            """;
        var client = BuildClient(new RespondingHandler(soap));

        var result = await client.SendAsync([1, 2, 3], "https://celcer.sri.gob.ec/fake?wsdl");

        result.Status.Should().Be("RECIBIDA");
        result.Received.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task SendAsync_on_devuelta_response_parses_status_and_error_messages()
    {
        const string soap = """
            <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
              <soap:Body>
                <ns2:validarComprobanteResponse xmlns:ns2="http://ec.gob.sri.ws.recepcion">
                  <RespuestaRecepcionComprobante>
                    <estado>DEVUELTA</estado>
                    <comprobantes>
                      <comprobante>
                        <claveAcceso>1702201205176001321000110010030001000011234567816</claveAcceso>
                        <mensajes>
                          <mensaje>
                            <identificador>35</identificador>
                            <mensaje>DOCUMENTO INVÁLIDO</mensaje>
                            <informacionAdicional>Error de estructura.</informacionAdicional>
                            <tipo>ERROR</tipo>
                          </mensaje>
                        </mensajes>
                      </comprobante>
                    </comprobantes>
                  </RespuestaRecepcionComprobante>
                </ns2:validarComprobanteResponse>
              </soap:Body>
            </soap:Envelope>
            """;
        var client = BuildClient(new RespondingHandler(soap));

        var result = await client.SendAsync([1, 2, 3], "https://celcer.sri.gob.ec/fake?wsdl");

        result.Status.Should().Be("DEVUELTA");
        result.Received.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("DOCUMENTO INVÁLIDO"));

        result.StructuredMessages.Should().ContainSingle();
        var message = result.StructuredMessages[0];
        message.Code.Should().Be("35");
        message.MessageType.Should().Be("ERROR");
        message.Message.Should().Be("DOCUMENTO INVÁLIDO");
        message.AdditionalInfo.Should().Be("Error de estructura.");
    }

    [Fact]
    public async Task SendAsync_on_devuelta_response_without_tipo_node_falls_back_to_error_message_type()
    {
        // Algunas respuestas de recepción no incluyen <tipo> — el fallback documentado es
        // "ERROR", nunca se inventa un identificador o mensaje que el SRI no envió.
        const string soap = """
            <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
              <soap:Body>
                <ns2:validarComprobanteResponse xmlns:ns2="http://ec.gob.sri.ws.recepcion">
                  <RespuestaRecepcionComprobante>
                    <estado>DEVUELTA</estado>
                    <comprobantes>
                      <comprobante>
                        <mensajes>
                          <mensaje>
                            <identificador>70</identificador>
                            <mensaje>CLAVE ACCESO EN PROCESAMIENTO</mensaje>
                            <informacionAdicional>Ya existe un envío previo.</informacionAdicional>
                          </mensaje>
                        </mensajes>
                      </comprobante>
                    </comprobantes>
                  </RespuestaRecepcionComprobante>
                </ns2:validarComprobanteResponse>
              </soap:Body>
            </soap:Envelope>
            """;
        var client = BuildClient(new RespondingHandler(soap));

        var result = await client.SendAsync([1, 2, 3], "https://celcer.sri.gob.ec/fake?wsdl");

        result.StructuredMessages.Should().ContainSingle();
        result.StructuredMessages[0].MessageType.Should().Be("ERROR");
        result.StructuredMessages[0].Code.Should().Be("70");
    }

    // ── Fase 9: CheckAuthorizationAsync ──────────────────────────────────────

    [Fact]
    public async Task CheckAuthorizationAsync_on_malformed_non_xml_response_returns_invalid_response_status_never_throws()
    {
        // Mismo defecto real que en SendAsync (Fase 8), confirmado por auditoría (Fase 9):
        // ParseAutorizacionResponse no estaba protegido dentro del loop de polling.
        var client = BuildClient(new RespondingHandler("502 Bad Gateway - upstream connection reset"));

        var result = await client.CheckAuthorizationAsync(new string('1', 49), "https://celcer.sri.gob.ec/fake?wsdl");

        result.Status.Should().Be("ERROR_RESPUESTA_INVALIDA");
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        result.Authorized.Should().BeFalse();
    }

    [Fact]
    public async Task CheckAuthorizationAsync_on_autorizado_response_parses_status_and_authorization_data()
    {
        const string soap = """
            <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
              <soap:Body>
                <ns2:autorizacionComprobanteResponse xmlns:ns2="http://ec.gob.sri.ws.autorizacion">
                  <RespuestaAutorizacionComprobante>
                    <claveAccesoConsultada>0503201201176001321000110010030009900641234567814</claveAccesoConsultada>
                    <numeroComprobantes>1</numeroComprobantes>
                    <autorizaciones>
                      <autorizacion>
                        <estado>AUTORIZADO</estado>
                        <numeroAutorizacion>0503201201176001321000110010030009900641234567814</numeroAutorizacion>
                        <fechaAutorizacion>2012-03-05T16:57:34.997-05:00</fechaAutorizacion>
                        <ambiente>PRUEBAS</ambiente>
                        <comprobante><![CDATA[<factura id="comprobante" version="1.1.0"></factura>]]></comprobante>
                      </autorizacion>
                    </autorizaciones>
                  </RespuestaAutorizacionComprobante>
                </ns2:autorizacionComprobanteResponse>
              </soap:Body>
            </soap:Envelope>
            """;
        var client = BuildClient(new RespondingHandler(soap));

        var result = await client.CheckAuthorizationAsync(new string('1', 49), "https://celcer.sri.gob.ec/fake?wsdl");

        result.Status.Should().Be("AUTORIZADO");
        result.Authorized.Should().BeTrue();
        result.AuthorizationNumber.Should().Be("0503201201176001321000110010030009900641234567814");
        result.DocumentXml.Should().Contain("factura");
        result.ErrorMessage.Should().BeNull();
        // FECHA-01 (rechazo real del SRI 2026-07-11, segunda ronda): un AuthorizationDate con
        // Kind=Local (el resultado de DateTime.TryParse sin estilos explícitos sobre un offset
        // como "-05:00") hace que Npgsql rechace la escritura en una columna "timestamp with time
        // zone" con DbUpdateException — el SRI autorizaba el comprobante realmente, pero
        // MarkAuthorized nunca llegaba a persistirse. AdjustToUniversal/AssumeUniversal garantiza
        // Kind=Utc siempre.
        result.AuthorizationDate.Kind.Should().Be(DateTimeKind.Utc);
        result.AuthorizationDate.Should().Be(new DateTime(2012, 3, 5, 21, 57, 34, 997, DateTimeKind.Utc));
    }

    [Fact]
    public async Task CheckAuthorizationAsync_polling_stops_immediately_on_recognized_terminal_status()
    {
        const string soap = """
            <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
              <soap:Body>
                <ns2:autorizacionComprobanteResponse xmlns:ns2="http://ec.gob.sri.ws.autorizacion">
                  <RespuestaAutorizacionComprobante>
                    <autorizaciones>
                      <autorizacion>
                        <estado>NO AUTORIZADO</estado>
                        <mensajes>
                          <mensaje>
                            <identificador>46</identificador>
                            <mensaje>RUC no existe</mensaje>
                            <tipo>ERROR</tipo>
                          </mensaje>
                        </mensajes>
                      </autorizacion>
                    </autorizaciones>
                  </RespuestaAutorizacionComprobante>
                </ns2:autorizacionComprobanteResponse>
              </soap:Body>
            </soap:Envelope>
            """;
        var client = BuildClient(new RespondingHandler(soap));

        var result = await client.CheckAuthorizationAsync(
            new string('1', 49), "https://celcer.sri.gob.ec/fake?wsdl", maxAttempts: 5);

        result.Status.Should().Be("NO AUTORIZADO");
        result.Authorized.Should().BeFalse();
        result.ErrorMessage.Should().Contain("RUC no existe");

        result.StructuredMessages.Should().ContainSingle();
        result.StructuredMessages[0].Code.Should().Be("46");
        result.StructuredMessages[0].MessageType.Should().Be("ERROR");
        result.StructuredMessages[0].Message.Should().Be("RUC no existe");
        result.StructuredMessages[0].AdditionalInfo.Should().BeNull();
    }

    private const string AuthorizedSoap = """
        <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
          <soap:Body>
            <ns2:autorizacionComprobanteResponse xmlns:ns2="http://ec.gob.sri.ws.autorizacion">
              <RespuestaAutorizacionComprobante>
                <autorizaciones>
                  <autorizacion>
                    <estado>AUTORIZADO</estado>
                  </autorizacion>
                </autorizaciones>
              </RespuestaAutorizacionComprobante>
            </ns2:autorizacionComprobanteResponse>
          </soap:Body>
        </soap:Envelope>
        """;

    [Fact]
    public async Task CheckAuthorizationAsync_waits_the_configured_initial_delay_before_first_request()
    {
        // SOAP-01 (auditoría SRI, Fase 2): la ficha técnica (sección 7.4) recomienda esperar un
        // tiempo configurable antes de la primera consulta de autorización tras "RECIBIDA". Esto
        // verifica que SriSoapClient realmente respeta
        // SriPollingOptions.InitialAuthorizationDelaySeconds — no solo que el mecanismo de
        // configuración exista, sino que efectivamente espera.
        var client = BuildClient(new RespondingHandler(AuthorizedSoap),
            new SriPollingOptions { InitialAuthorizationDelaySeconds = 1 });

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await client.CheckAuthorizationAsync(new string('1', 49), "https://celcer.sri.gob.ec/fake?wsdl");
        stopwatch.Stop();

        result.Status.Should().Be("AUTORIZADO");
        stopwatch.Elapsed.Should().BeGreaterThanOrEqualTo(TimeSpan.FromSeconds(1),
            "debe esperar el tiempo configurado antes de la primera consulta de autorización");
    }

    [Fact]
    public async Task CheckAuthorizationAsync_without_configured_delay_does_not_wait()
    {
        var client = BuildClient(new RespondingHandler(AuthorizedSoap), pollingOptions: null);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await client.CheckAuthorizationAsync(new string('1', 49), "https://celcer.sri.gob.ec/fake?wsdl");
        stopwatch.Stop();

        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(800),
            "sin espera configurada (0, el default de SriPollingOptions) no debe introducirse ningún delay");
    }
}
