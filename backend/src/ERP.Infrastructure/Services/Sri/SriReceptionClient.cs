using ERP.Application.Common.Interfaces.SRI;

namespace ERP.Infrastructure.Services.Sri;

/// <summary>
/// Adapta <see cref="SriSoapClient"/> (cliente SOAP concreto, HttpClient puro) a la interfaz
/// inyectable <see cref="ISriReceptionClient"/> — Application nunca conoce SriSoapClient
/// directamente, solo este contrato. No reimplementa el envío ni el parseo: delega
/// íntegramente y solo traduce el DTO de resultado de Infrastructure al de Application.
/// </summary>
public sealed class SriReceptionClient : ISriReceptionClient
{
    private readonly SriSoapClient _client;

    public SriReceptionClient(SriSoapClient client) => _client = client;

    public async Task<SriReceptionResult> SendAsync(
        byte[] signedXmlBytes, string wsdlUrl, CancellationToken ct = default)
    {
        var result = await _client.SendAsync(signedXmlBytes, wsdlUrl, ct);
        return new SriReceptionResult
        {
            Status = result.Status,
            Errors = result.Errors,
            StructuredMessages = result.StructuredMessages,
        };
    }
}
