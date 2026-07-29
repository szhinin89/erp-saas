using ERP.Infrastructure.Services.Sri;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace ERP.Infrastructure.Tests.Services.Sri;

/// <summary>
/// Verifica que <see cref="SriAuthorizationClient"/> delega íntegramente en <see cref="SriSoapClient"/>
/// y solo traduce el DTO de resultado — sin reimplementar polling ni parseo.
/// </summary>
public sealed class SriAuthorizationClientTests
{
    private sealed class RespondingHandler : HttpMessageHandler
    {
        private readonly string _body;

        public RespondingHandler(string body) => _body = body;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) =>
            Task.FromResult(
                new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(_body, System.Text.Encoding.UTF8, "text/xml"),
                }
            );
    }

    private sealed class FakeHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        public FakeHttpClientFactory(HttpMessageHandler handler) => _handler = handler;

        public HttpClient CreateClient(string name) => new(_handler);
    }

    [Fact]
    public async Task CheckAsync_translates_SriSoapClient_result_to_application_dto()
    {
        const string soap = """
            <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
              <soap:Body>
                <ns2:autorizacionComprobanteResponse xmlns:ns2="http://ec.gob.sri.ws.autorizacion">
                  <RespuestaAutorizacionComprobante>
                    <autorizaciones>
                      <autorizacion>
                        <estado>AUTORIZADO</estado>
                        <numeroAutorizacion>0503201201176001321000110010030009900641234567814</numeroAutorizacion>
                        <fechaAutorizacion>2012-03-05T16:57:34.997-05:00</fechaAutorizacion>
                        <comprobante><![CDATA[<factura/>]]></comprobante>
                      </autorizacion>
                    </autorizaciones>
                  </RespuestaAutorizacionComprobante>
                </ns2:autorizacionComprobanteResponse>
              </soap:Body>
            </soap:Envelope>
            """;
        var soapClient = new SriSoapClient(
            new FakeHttpClientFactory(new RespondingHandler(soap)),
            NullLogger<SriSoapClient>.Instance
        );
        var adapter = new SriAuthorizationClient(soapClient);

        var result = await adapter.CheckAsync(
            new string('1', 49),
            "https://celcer.sri.gob.ec/fake?wsdl"
        );

        result.Status.Should().Be("AUTORIZADO");
        result.Authorized.Should().BeTrue();
        result.AuthorizationNumber.Should().Be("0503201201176001321000110010030009900641234567814");
        result.DocumentXml.Should().Contain("factura");
    }
}
