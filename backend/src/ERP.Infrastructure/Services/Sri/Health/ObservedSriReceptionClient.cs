using ERP.Application.Common.Interfaces.SRI;

namespace ERP.Infrastructure.Services.Sri.Health;

/// <summary>
/// Decorador de DI sobre <see cref="ISriReceptionClient"/> (adaptador FROZEN): delega la llamada sin
/// tocar parámetros ni resultado y solo registra la evidencia técnica en el monitor de salud SRI.
/// Si la llamada interna lanza (cancelación del llamador), la excepción se propaga intacta y no se
/// registra nada.
/// </summary>
public sealed class ObservedSriReceptionClient : ISriReceptionClient
{
    private readonly ISriReceptionClient _inner;
    private readonly ISriServiceHealthRecorder _recorder;
    private readonly TimeProvider _timeProvider;

    public ObservedSriReceptionClient(
        ISriReceptionClient inner,
        ISriServiceHealthRecorder recorder,
        TimeProvider timeProvider
    )
    {
        _inner = inner;
        _recorder = recorder;
        _timeProvider = timeProvider;
    }

    public async Task<SriReceptionResult> SendAsync(
        byte[] signedXmlBytes,
        string wsdlUrl,
        CancellationToken ct = default
    )
    {
        var started = _timeProvider.GetTimestamp();
        var result = await _inner.SendAsync(signedXmlBytes, wsdlUrl, ct);
        _recorder.Record(
            wsdlUrl,
            SriServiceKind.Reception,
            SriObservationClassifier.FromReceptionStatus(result.Status),
            _timeProvider.GetElapsedTime(started)
        );
        return result;
    }
}
