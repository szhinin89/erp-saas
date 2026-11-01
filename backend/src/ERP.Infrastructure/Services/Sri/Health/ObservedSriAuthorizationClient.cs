using ERP.Application.Common.Interfaces.SRI;

namespace ERP.Infrastructure.Services.Sri.Health;

/// <summary>
/// Decorador de DI sobre <see cref="ISriAuthorizationClient"/> (adaptador FROZEN) — mismo contrato
/// que <see cref="ObservedSriReceptionClient"/>. <see cref="CheckAsync"/> incluye la espera inicial y
/// el polling deliberado del cliente, así que su duración no mide al SRI y se registra sin ella.
/// </summary>
public sealed class ObservedSriAuthorizationClient : ISriAuthorizationClient
{
    private readonly ISriAuthorizationClient _inner;
    private readonly ISriServiceHealthRecorder _recorder;
    private readonly TimeProvider _timeProvider;

    public ObservedSriAuthorizationClient(
        ISriAuthorizationClient inner,
        ISriServiceHealthRecorder recorder,
        TimeProvider timeProvider
    )
    {
        _inner = inner;
        _recorder = recorder;
        _timeProvider = timeProvider;
    }

    public async Task<SriAuthorizationResult> CheckAsync(
        string accessKey,
        string wsdlUrl,
        CancellationToken ct = default
    )
    {
        var result = await _inner.CheckAsync(accessKey, wsdlUrl, ct);
        _recorder.Record(
            wsdlUrl,
            SriServiceKind.Authorization,
            SriObservationClassifier.FromAuthorizationStatus(result.Status),
            elapsed: null
        );
        return result;
    }

    public async Task<SriAuthorizationHistoryResult> QueryDecisionsAsync(
        string accessKey,
        string wsdlUrl,
        CancellationToken ct = default
    )
    {
        var started = _timeProvider.GetTimestamp();
        var result = await _inner.QueryDecisionsAsync(accessKey, wsdlUrl, ct);
        // Un fallo de esta consulta solo trae texto: no se interpreta (Undetermined), y su duración
        // no se usa porque no se sabe si el servicio llegó a responder.
        _recorder.Record(
            wsdlUrl,
            SriServiceKind.Authorization,
            result.Succeeded ? SriObservationOutcome.Responded : SriObservationOutcome.Undetermined,
            result.Succeeded ? _timeProvider.GetElapsedTime(started) : null
        );
        return result;
    }
}
