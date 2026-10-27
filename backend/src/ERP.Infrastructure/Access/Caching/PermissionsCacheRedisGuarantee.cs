using StackExchange.Redis;

namespace ERP.Infrastructure.Access.Caching;

/// <summary>
/// SEC-06 (opción A) — contrato del Redis usado por la caché de permisos efectivos. PostgreSQL es la
/// autoridad: Redis nunca debe recuperar un estado anterior (versiones, marcas y entradas viejas
/// volverían a coincidir) ni expulsar marcas de invalidación por presión de memoria.
/// <list type="bullet">
/// <item><c>maxmemory-policy noeviction</c>.</item>
/// <item><c>save ""</c> (sin snapshots RDB) y <c>appendonly no</c> (sin AOF).</item>
/// <item>Nodo primario sin réplicas conectadas: no hay réplica asíncrona que pueda promoverse
/// atrasada.</item>
/// </list>
/// Lo no verificable (conexión fallida, <c>CONFIG</c>/<c>INFO</c> deshabilitados, propiedad ausente)
/// cuenta como incumplimiento: nunca se acepta por defecto.
/// </summary>
public static class PermissionsCacheRedisGuarantee
{
    private const string Unverifiable = "<no verificable>";

    /// <summary>Incumplimientos de un nodo; vacío si cumple.</summary>
    public static IReadOnlyList<string> Evaluate(
        string? maxmemoryPolicy,
        string? save,
        string? appendonly,
        string? role,
        string? connectedReplicas
    )
    {
        var violations = new List<string>();
        Require("maxmemory-policy", maxmemoryPolicy, "noeviction");
        Require("save", save, "");
        Require("appendonly", appendonly, "no");
        Require("role", role, "master");
        Require("connected_slaves", connectedReplicas, "0");
        return violations;

        void Require(string property, string? actual, string expected)
        {
            if (actual is null || !string.Equals(actual.Trim(), expected, StringComparison.Ordinal))
                violations.Add(
                    $"{property}={(actual is null ? Unverifiable : $"\"{actual}\"")} (requerido \"{expected}\")"
                );
        }
    }

    /// <summary>
    /// Inspecciona cada nodo con una conexión propia y efímera en modo admin (necesario para
    /// <c>CONFIG GET</c>; la conexión compartida del host no lo habilita). Lanza
    /// <see cref="InvalidOperationException"/> si no cumple o no se puede verificar. El mensaje
    /// nunca incluye la cadena de conexión.
    /// </summary>
    public static async Task EnsureAsync(string connectionString, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> violations;
        try
        {
            violations = await InspectAsync(connectionString, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidOperationException(
                "SEC-06: no se pudo verificar el Redis de la caché de permisos (conexión o "
                    + "CONFIG/INFO no disponibles). Un Redis no verificable no se acepta como seguro.",
                ex
            );
        }

        if (violations.Count > 0)
            throw new InvalidOperationException(
                "SEC-06: el Redis de la caché de permisos no cumple el contrato fail-closed: "
                    + string.Join("; ", violations)
                    + "."
            );
    }

    private static async Task<IReadOnlyList<string>> InspectAsync(
        string connectionString,
        CancellationToken cancellationToken
    )
    {
        var options = ConfigurationOptions.Parse(connectionString);
        options.AllowAdmin = true;
        options.AbortOnConnectFail = true;

        await using var connection = await ConnectionMultiplexer.ConnectAsync(options);
        var endpoints = connection.GetEndPoints();
        if (endpoints.Length == 0)
            return ["endpoints=<ninguno>"];

        var violations = new List<string>();
        foreach (var endpoint in endpoints)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var server = connection.GetServer(endpoint);
            var config = (await server.ConfigGetAsync("*")).ToDictionary(
                kv => kv.Key,
                kv => kv.Value,
                StringComparer.OrdinalIgnoreCase
            );
            var replication = (await server.InfoAsync("replication"))
                .SelectMany(group => group)
                .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);

            violations.AddRange(
                Evaluate(
                    config.GetValueOrDefault("maxmemory-policy"),
                    config.GetValueOrDefault("save"),
                    config.GetValueOrDefault("appendonly"),
                    replication.GetValueOrDefault("role"),
                    replication.GetValueOrDefault("connected_slaves")
                )
            );
        }

        return violations;
    }
}
