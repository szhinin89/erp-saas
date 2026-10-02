using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Infrastructure.Services.Sri;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace ERP.Infrastructure.Tests.Services.Sri;

/// <summary>
/// ZH-RETENTION-SRI-ANNULMENT-01B — <see cref="SriSoapClient.QueryDocumentStatusAsync"/> contra el contrato
/// de ConsultaComprobante de la Ficha Técnica Comprobantes Electrónicos Esquema Offline v2.34 §8 (respuestas
/// tomadas de los ejemplos oficiales). Separa el resultado TÉCNICO de la consulta del estado FISCAL:
/// <c>estadoConsulta=RECHAZADA</c> nunca es NO AUTORIZADO, y un timeout nunca es ANULADO.
/// </summary>
public sealed class SriSoapClientConsultaComprobanteTests
{
    private const string AccessKey = "1212202407179135268800120010010000000071234567813";
    private const string TestWsdl =
        "https://celcer.sri.gob.ec/comprobantes-electronicos-ws/RecepcionComprobantesOffline?wsdl";

    private sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class CapturingHandler(string body) : HttpMessageHandler
    {
        public string? Url { get; private set; }
        public string? RequestBody { get; private set; }
        public int Calls { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Calls++;
            Url = request.RequestUri!.ToString();
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "text/xml"),
            };
        }
    }

    private sealed class FailingHandler(Func<Exception> failure) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => throw failure();
    }

    private static SriSoapClient Client(HttpMessageHandler handler) =>
        new(new FakeHttpClientFactory(handler), NullLogger<SriSoapClient>.Instance);

    /// <summary>Respuesta de éxito (Ficha v2.34 §8.2).</summary>
    private static string Success(string estado, string key = AccessKey) =>
        $"""
            <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
              <soap:Body>
                <ns2:consultarEstadoAutorizacionComprobanteResponse xmlns:ns2="http://ec.gob.sri.ws.consultas">
                  <EstadoAutorizacionComprobante>
                    <claveAcceso>{key}</claveAcceso>
                    <mensajes/>
                    <estadoAutorizacion>{estado}</estadoAutorizacion>
                    <tipoComprobante>COMPROBANTE DE RETENCION</tipoComprobante>
                    <rucEmisor>1791352688001</rucEmisor>
                    <fechaAutorizacion>2024-12-12T10:49:37-05:00</fechaAutorizacion>
                  </EstadoAutorizacionComprobante>
                </ns2:consultarEstadoAutorizacionComprobanteResponse>
              </soap:Body>
            </soap:Envelope>
            """;

    /// <summary>Respuesta de error (Ficha v2.34 §8.3): <c>estadoConsulta=RECHAZADA</c>, identificador 99.</summary>
    private static string Rejected(string element = "estadoConsulta") =>
        $"""
            <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
              <soap:Body>
                <ns2:consultarEstadoAutorizacionComprobanteResponse xmlns:ns2="http://ec.gob.sri.ws.consultas">
                  <EstadoAutorizacionComprobante>
                    <claveAcceso>{AccessKey}</claveAcceso>
                    <mensajes>
                      <mensaje>
                        <identificador>99</identificador>
                        <mensaje>ERROR AL CONSULTAR DATOS DEL SERVICIO WEB</mensaje>
                        <informacionAdicional>No existen datos para los parámetros ingresados</informacionAdicional>
                        <tipo>ERROR</tipo>
                      </mensaje>
                    </mensajes>
                    <{element}>RECHAZADA</{element}>
                  </EstadoAutorizacionComprobante>
                </ns2:consultarEstadoAutorizacionComprobanteResponse>
              </soap:Body>
            </soap:Envelope>
            """;

    [Fact]
    public async Task Postea_consultarEstadoAutorizacionComprobante_al_ConsultaComprobante_del_mismo_ambiente()
    {
        var handler = new CapturingHandler(Success("AUTORIZADO"));

        await Client(handler).QueryDocumentStatusAsync(AccessKey, TestWsdl);

        handler.Url.Should().Be("https://celcer.sri.gob.ec/comprobantes-electronicos-ws/ConsultaComprobante");
        handler.RequestBody.Should().Contain("xmlns:ec=\"http://ec.gob.sri.ws.consultas\"");
        handler.RequestBody.Should().Contain("<ec:consultarEstadoAutorizacionComprobante>");
        handler.RequestBody.Should().Contain($"<claveAcceso>{AccessKey}</claveAcceso>");
    }

    [Theory]
    [InlineData("AUTORIZADO", SriFiscalStatus.Authorized)]
    [InlineData("NO AUTORIZADO", SriFiscalStatus.NotAuthorized)]
    [InlineData("PENDIENTE DE ANULAR", SriFiscalStatus.PendingAnnulment)]
    [InlineData("ANULADO", SriFiscalStatus.Annulled)]
    [InlineData(" pendiente  de anular ", SriFiscalStatus.PendingAnnulment)]
    public async Task Los_cuatro_estados_oficiales_se_tipan_sin_perder_el_literal(string literal, SriFiscalStatus expected)
    {
        var result = await Client(new CapturingHandler(Success(literal))).QueryDocumentStatusAsync(AccessKey, TestWsdl);

        result.Outcome.Should().Be(SriStatusQueryOutcome.Success);
        result.FiscalStatus.Should().Be(expected);
        result.RawAuthorizationStatus.Should().Be(literal.Trim());
        result.AccessKey.Should().Be(AccessKey);
        result.DocumentType.Should().Be("COMPROBANTE DE RETENCION");
        result.IssuerRuc.Should().Be("1791352688001");
        result.AuthorizationDateUtc.Should().Be(new DateTime(2024, 12, 12, 15, 49, 37, DateTimeKind.Utc));
        result.RawResponse.Should().Contain("EstadoAutorizacionComprobante");
    }

    [Theory]
    [InlineData("estadoConsulta")]
    [InlineData("estadoAutorizacion")]
    public async Task RECHAZADA_codigo_99_es_consulta_rechazada_nunca_NO_AUTORIZADO(string element)
    {
        var result = await Client(new CapturingHandler(Rejected(element))).QueryDocumentStatusAsync(AccessKey, TestWsdl);

        result.Outcome.Should().Be(SriStatusQueryOutcome.Rejected);
        result.FiscalStatus.Should().Be(SriFiscalStatus.Unknown);
        result.RawQueryStatus.Should().Be("RECHAZADA");
        result.Messages.Should().ContainSingle(m => m.Code == "99");
        result.ErrorMessage.Should().Contain("No existen datos para los parámetros ingresados");
    }

    [Fact]
    public async Task Timeout_es_Timeout_nunca_un_estado_fiscal()
    {
        var result = await Client(new FailingHandler(() => new TaskCanceledException("timeout")))
            .QueryDocumentStatusAsync(AccessKey, TestWsdl);

        result.Outcome.Should().Be(SriStatusQueryOutcome.Timeout);
        result.FiscalStatus.Should().Be(SriFiscalStatus.Unknown);
    }

    [Fact]
    public async Task Error_de_red_es_Unavailable_nunca_un_estado_fiscal()
    {
        var result = await Client(new FailingHandler(() => new HttpRequestException("no route to host")))
            .QueryDocumentStatusAsync(AccessKey, TestWsdl);

        result.Outcome.Should().Be(SriStatusQueryOutcome.Unavailable);
        result.FiscalStatus.Should().Be(SriFiscalStatus.Unknown);
    }

    [Theory]
    [InlineData("EN PROCESO")]
    [InlineData("")]
    public async Task Literal_no_oficial_es_Unknown(string literal)
    {
        var result = await Client(new CapturingHandler(Success(literal))).QueryDocumentStatusAsync(AccessKey, TestWsdl);

        result.Outcome.Should().Be(SriStatusQueryOutcome.Unknown);
        result.FiscalStatus.Should().Be(SriFiscalStatus.Unknown);
    }

    [Fact]
    public async Task Respuesta_de_otra_clave_de_acceso_se_descarta()
    {
        var result = await Client(new CapturingHandler(Success("ANULADO", key: new string('9', 49))))
            .QueryDocumentStatusAsync(AccessKey, TestWsdl);

        result.Outcome.Should().Be(SriStatusQueryOutcome.Unknown);
        result.FiscalStatus.Should().Be(SriFiscalStatus.Unknown);
    }

    [Fact]
    public async Task Respuesta_no_XML_es_Unknown_y_no_lanza()
    {
        var result = await Client(new CapturingHandler("<html>proxy error")).QueryDocumentStatusAsync(AccessKey, TestWsdl);

        result.Outcome.Should().Be(SriStatusQueryOutcome.Unknown);
        result.FiscalStatus.Should().Be(SriFiscalStatus.Unknown);
    }

    [Fact]
    public async Task Sin_WsdlUrl_de_Recepcion_no_se_adivina_el_endpoint()
    {
        var handler = new CapturingHandler(Success("ANULADO"));

        var result = await Client(handler).QueryDocumentStatusAsync(AccessKey, "https://example.com/otro-servicio?wsdl");

        result.Outcome.Should().Be(SriStatusQueryOutcome.Unavailable);
        handler.Calls.Should().Be(0);
    }

    [Fact]
    public async Task El_adaptador_ISriDocumentStatusQuery_delega_en_el_mismo_SriSoapClient()
    {
        var handler = new CapturingHandler(Success("PENDIENTE DE ANULAR"));

        var result = await new SriDocumentStatusQuery(Client(handler)).QueryAsync(AccessKey, TestWsdl);

        result.FiscalStatus.Should().Be(SriFiscalStatus.PendingAnnulment);
        handler.Calls.Should().Be(1);
    }
}
