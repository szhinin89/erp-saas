using Npgsql;

namespace ERP.API.Hosting;

/// <summary>Fail-closed guard for the launcher's destructive local reset; never opens a connection.</summary>
public static class LauncherResetGuard
{
    public static void Confirm(
        string connectionString,
        string environment,
        Action<string> display,
        Func<string?> readConfirmation
    )
    {
        NpgsqlConnectionStringBuilder target;
        try
        {
            target = new NpgsqlConnectionStringBuilder(connectionString);
        }
        catch
        {
            throw new InvalidOperationException("Reset bloqueado: conexión inválida.");
        }

        display($"Servidor: {target.Host}; puerto: {target.Port}; base: {target.Database}; entorno: {environment}");
        if (
            environment != "Development"
            || target.Host is not ("localhost" or "127.0.0.1" or "::1")
            || target.Port != 5435
            || target.Database != "erp_zh_dev"
        )
            throw new InvalidOperationException("Reset bloqueado: destino no autorizado para el launcher local.");

        var expected = $"ELIMINAR {target.Host}:{target.Port}/{target.Database} {environment}";
        display($"Escriba exactamente: {expected}");
        if (!string.Equals(readConfirmation(), expected, StringComparison.Ordinal))
            throw new InvalidOperationException("Reset cancelado: confirmación incorrecta.");
    }
}
