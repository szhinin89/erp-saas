using ERP.Application.Common.Config;
using ERP.Application.Common.Interfaces.SRI;
using ERP.Application.Modules.ElectronicInvoicing.DTOs;
using ERP.Application.Modules.ElectronicInvoicing.Enums;
using Microsoft.Extensions.Options;

namespace ERP.Application.Modules.ElectronicInvoicing.Health;

/// <summary>Lectura del estado técnico de un servicio SRI para la URL configurada de una empresa.</summary>
public interface ISriServiceHealthReader
{
    SriServiceStatusDto GetStatus(string? wsdlUrl, SriServiceKind service);
}

/// <summary>
/// Monitor central de salud SRI — singleton en memoria del proceso, agrupado por (host del SRI,
/// servicio). El SRI es un servicio público único: la evidencia de cualquier empresa vale para todas
/// y no contiene nada propio de un tenant. Pruebas (celcer) y Producción (cel) quedan separados por
/// host. Sin persistencia por diseño (V1, sin migraciones): un reinicio vuelve a "Sin verificar".
/// </summary>
public sealed class SriServiceHealthMonitor : ISriServiceHealthRecorder, ISriServiceHealthReader
{
    /// <summary>Observaciones retenidas por (host, servicio); las vencidas se descartan al registrar y al leer.</summary>
    public const int MaxObservationsPerService = 50;

    private readonly Dictionary<(string Host, SriServiceKind Service), LinkedList<SriServiceObservation>> _observations = new();
    private readonly Lock _lock = new();
    private readonly SriHealthOptions _options;
    private readonly TimeProvider _timeProvider;

    public SriServiceHealthMonitor(IOptions<SriHealthOptions> options, TimeProvider timeProvider)
    {
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public void Record(
        string? wsdlUrl,
        SriServiceKind service,
        SriObservationOutcome outcome,
        TimeSpan? elapsed
    )
    {
        try
        {
            if (TryGetHost(wsdlUrl) is not { } host)
                return;

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            lock (_lock)
            {
                if (!_observations.TryGetValue((host, service), out var list))
                    _observations[(host, service)] = list = new LinkedList<SriServiceObservation>();

                list.AddFirst(new SriServiceObservation(outcome, elapsed, now));
                Prune(list, now);
            }
        }
        catch
        {
            // Invariante de los decoradores: registrar evidencia jamás altera ni interrumpe la
            // llamada SOAP observada. Una observación perdida solo degrada a "Sin verificar".
        }
    }

    public SriServiceStatusDto GetStatus(string? wsdlUrl, SriServiceKind service)
    {
        if (TryGetHost(wsdlUrl) is not { } host)
            return SriServiceHealthEvaluator.Unverified(SriServiceHealthReason.NotConfigured);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        List<SriServiceObservation> fresh;
        lock (_lock)
        {
            if (!_observations.TryGetValue((host, service), out var list))
                return SriServiceHealthEvaluator.Unverified(SriServiceHealthReason.NoRecentEvidence);
            Prune(list, now);
            fresh = list.ToList();
        }

        return SriServiceHealthEvaluator.Evaluate(fresh, _options);
    }

    private void Prune(LinkedList<SriServiceObservation> list, DateTime now)
    {
        var oldestAllowed = now - TimeSpan.FromMinutes(_options.EvidenceFreshnessMinutes);
        while (list.Count > 0
            && (list.Count > MaxObservationsPerService || list.Last!.Value.ObservedAtUtc < oldestAllowed))
            list.RemoveLast();
    }

    private static string? TryGetHost(string? wsdlUrl) =>
        Uri.TryCreate(wsdlUrl, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host)
            ? uri.Host.ToLowerInvariant()
            : null;
}
