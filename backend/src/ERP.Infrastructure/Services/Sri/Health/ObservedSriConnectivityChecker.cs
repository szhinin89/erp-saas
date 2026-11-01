using ERP.Application.Common.Interfaces.SRI;

namespace ERP.Infrastructure.Services.Sri.Health;

/// <summary>
/// Decorador de DI sobre <see cref="ISriConnectivityChecker"/>: el ping existente (GET del WSDL de
/// Recepción configurado) se registra como evidencia débil de alcance. No agrega pings: solo observa
/// los que ya disparan Ventas y "Validar configuración".
/// </summary>
public sealed class ObservedSriConnectivityChecker : ISriConnectivityChecker
{
    private readonly ISriConnectivityChecker _inner;
    private readonly ISriServiceHealthRecorder _recorder;

    public ObservedSriConnectivityChecker(
        ISriConnectivityChecker inner,
        ISriServiceHealthRecorder recorder
    )
    {
        _inner = inner;
        _recorder = recorder;
    }

    public async Task<bool> PingAsync(string wsdlUrl, CancellationToken ct = default)
    {
        var reachable = await _inner.PingAsync(wsdlUrl, ct);
        _recorder.Record(
            wsdlUrl,
            SriServiceKind.Reception,
            reachable ? SriObservationOutcome.ProbeReachable : SriObservationOutcome.ProbeUnreachable,
            elapsed: null
        );
        return reachable;
    }
}
