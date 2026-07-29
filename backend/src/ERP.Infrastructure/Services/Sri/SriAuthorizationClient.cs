using ERP.Application.Common.Interfaces.SRI;

namespace ERP.Infrastructure.Services.Sri;

/// <summary>
/// Adapta <see cref="SriSoapClient"/> a la interfaz inyectable <see cref="ISriAuthorizationClient"/>
/// — mismo patrón que <see cref="SriReceptionClient"/>. No reimplementa el polling ni el
/// parseo: delega íntegramente en <see cref="SriSoapClient.CheckAuthorizationAsync"/> y solo
/// traduce el DTO de resultado de Infrastructure al de Application.
/// </summary>
public sealed class SriAuthorizationClient : ISriAuthorizationClient
{
    private readonly SriSoapClient _client;

    public SriAuthorizationClient(SriSoapClient client) => _client = client;

    public async Task<SriAuthorizationResult> CheckAsync(
        string accessKey,
        string wsdlUrl,
        CancellationToken ct = default
    )
    {
        var result = await _client.CheckAuthorizationAsync(
            accessKey,
            wsdlUrl,
            cancellationToken: ct
        );
        return new SriAuthorizationResult
        {
            Status = result.Status,
            AuthorizationNumber = string.IsNullOrWhiteSpace(result.AuthorizationNumber)
                ? null
                : result.AuthorizationNumber,
            AuthorizationDate =
                result.AuthorizationDate == default ? null : result.AuthorizationDate,
            DocumentXml = string.IsNullOrWhiteSpace(result.DocumentXml) ? null : result.DocumentXml,
            Messages = result.Messages,
            StructuredMessages = result.StructuredMessages,
            ErrorMessage = result.ErrorMessage,
        };
    }
}
