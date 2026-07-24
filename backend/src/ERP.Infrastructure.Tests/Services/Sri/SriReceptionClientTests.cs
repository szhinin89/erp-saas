using ERP.Infrastructure.Services.Sri;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace ERP.Infrastructure.Tests.Services.Sri;

/// <summary>
/// Verifica que <see cref="SriReceptionClient"/> delega íntegramente en <see cref="SriSoapClient"/>
/// y solo traduce el DTO de resultado — sin reimplementar envío ni parseo.
/// </summary>
public sealed class SriReceptionClientTests
{
    private sealed class RespondingHandler : HttpMessageHandler
    {
        private readonly string _body;
        public RespondingHandler(string body) => _body = body;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(_body, System.Text.Encoding.UTF8, "text/xml"),
            });
    }

    private sealed class FakeHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        public FakeHttpClientFactory(HttpMessageHandler handler) => _handler = handler;
        public HttpClient CreateClient(string name) => new(_handler);
    }

    [Fact]
    public async Task SendAsync_translates_SriSoapClient_result_to_application_dto()
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
        var soapClient = new SriSoapClient(new FakeHttpClientFactory(new RespondingHandler(soap)), NullLogger<SriSoapClient>.Instance);
        var adapter = new SriReceptionClient(soapClient);

        var result = await adapter.SendAsync([1, 2, 3], "https://celcer.sri.gob.ec/fake?wsdl");

        result.Status.Should().Be("RECIBIDA");
        result.Received.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }
}
