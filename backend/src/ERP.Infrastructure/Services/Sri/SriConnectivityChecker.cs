using ERP.Application.Common.Interfaces.SRI;

namespace ERP.Infrastructure.Services.Sri;

/// <summary>Adapta <see cref="SriSoapClient.PingAsync"/> a <see cref="ISriConnectivityChecker"/> (Application).</summary>
public sealed class SriConnectivityChecker : ISriConnectivityChecker
{
    private readonly SriSoapClient _soapClient;

    public SriConnectivityChecker(SriSoapClient soapClient) => _soapClient = soapClient;

    public Task<bool> PingAsync(string wsdlUrl, CancellationToken ct = default) =>
        _soapClient.PingAsync(wsdlUrl, ct);
}
