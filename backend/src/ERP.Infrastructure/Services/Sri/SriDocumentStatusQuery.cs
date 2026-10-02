using ERP.Application.Common.Interfaces.SRI;

namespace ERP.Infrastructure.Services.Sri;

/// <summary>
/// ZH-RETENTION-SRI-ANNULMENT-01B — adapta <see cref="SriSoapClient.QueryDocumentStatusAsync"/>
/// (ConsultaComprobante) a <see cref="ISriDocumentStatusQuery"/>; mismo patrón que
/// <see cref="SriAuthorizationClient"/>. No reimplementa HTTP, reintentos, SOAP Fault ni parseo.
/// </summary>
public sealed class SriDocumentStatusQuery : ISriDocumentStatusQuery
{
    private readonly SriSoapClient _client;

    public SriDocumentStatusQuery(SriSoapClient client) => _client = client;

    public Task<SriDocumentStatusResult> QueryAsync(
        string accessKey,
        string wsdlUrl,
        CancellationToken ct = default
    ) => _client.QueryDocumentStatusAsync(accessKey, wsdlUrl, ct);
}
