using System.Data;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace ERP.Infrastructure.Seeding.InstallData;

/// <summary>
/// Ejecuta scripts embebidos en Seeding/InstallData una sola vez por checksum.
/// Diseñado para poblar catálogos base al primer arranque de una instalación.
/// </summary>
public sealed class InstallDataBootstrapService : IInstallDataBootstrapService
{
    private const string ResourcePrefix = "ERP.Infrastructure.Seeding.InstallData.";

    private static readonly Regex SourceResourceRegex = new(
        @"^\s*--\s*@source-resource\s+(.+?)\s*$",
        RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);

    private static readonly Regex DestructiveTagRegex = new(
        @"^\s*--\s*@destructive\b",
        RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);

    private static readonly Regex DestructiveSqlRegex = new(
        @"\b(delete\s+from|truncate\s+table|drop\s+table)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly ErpDbContext _db;
    private readonly InstallDataOptions _options;
    private readonly ILogger<InstallDataBootstrapService> _logger;
    private readonly Assembly _assembly;

    public InstallDataBootstrapService(
        ErpDbContext db,
        IOptions<InstallDataOptions> options,
        ILogger<InstallDataBootstrapService> logger)
    {
        _db = db;
        _options = options.Value ?? new InstallDataOptions();
        _logger = logger;
        _assembly = typeof(ErpDbContext).Assembly;
    }

    public async Task ApplyPendingAsync(CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("InstallData deshabilitado por configuración.");
            return;
        }

        var resources = _assembly
            .GetManifestResourceNames()
            .Where(r => r.StartsWith(ResourcePrefix, StringComparison.Ordinal) && r.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .OrderBy(r => r, StringComparer.Ordinal)
            .ToList();

        if (resources.Count == 0)
        {
            _logger.LogInformation("No se encontraron scripts en {Prefix}.", ResourcePrefix);
            return;
        }

        var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
        var shouldCloseConnection = conn.State != ConnectionState.Open;
        if (shouldCloseConnection)
            await conn.OpenAsync(ct);

        try
        {
            await EnsureMetadataTableAsync(conn, ct);

            foreach (var resource in resources)
            {
                var scriptText = await ReadResourceAsync(resource, ct);
                if (string.IsNullOrWhiteSpace(scriptText))
                    continue;

                var effectiveScript = await ResolveSourceResourceIfAnyAsync(scriptText, ct);
                var isDestructive = IsDestructiveScript(effectiveScript);

                if (isDestructive && !IsDestructiveConfirmed())
                {
                    throw new InvalidOperationException(
                        $"El script '{resource}' fue detectado como destructivo y está bloqueado. " +
                        $"Para habilitarlo: InstallData:AllowDestructive=true y InstallData:ConfirmDestructive='{InstallDataOptions.DestructiveConfirmPhrase}'.");
                }

                var checksum = ComputeSha256(effectiveScript);
                var existingChecksum = await GetExistingChecksumAsync(conn, resource, ct);

                if (existingChecksum is not null)
                {
                    if (string.Equals(existingChecksum, checksum, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation("InstallData skip (ya aplicado): {Script}", resource);
                        continue;
                    }

                    throw new InvalidOperationException(
                        $"El script '{resource}' ya fue aplicado con otro checksum. " +
                        "No se permite modificar scripts históricos; agrega un nuevo archivo numerado.");
                }

                await using var tx = await conn.BeginTransactionAsync(ct);
                try
                {
                    await ExecuteSqlAsync(conn, tx, effectiveScript, ct);
                    await RegisterAppliedAsync(conn, tx, resource, checksum, isDestructive, ct);
                    await tx.CommitAsync(ct);
                    _logger.LogInformation("InstallData aplicado: {Script}", resource);
                }
                catch
                {
                    await tx.RollbackAsync(ct);
                    throw;
                }
            }
        }
        finally
        {
            if (shouldCloseConnection && conn.State == ConnectionState.Open)
                await conn.CloseAsync();
        }
    }

    private static async Task EnsureMetadataTableAsync(NpgsqlConnection conn, CancellationToken ct)
    {
        const string sql = """
                           CREATE TABLE IF NOT EXISTS install_data_scripts
                           (
                               id BIGSERIAL PRIMARY KEY,
                               script_name TEXT NOT NULL UNIQUE,
                               checksum TEXT NOT NULL,
                               is_destructive BOOLEAN NOT NULL DEFAULT FALSE,
                               applied_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW()
                           );
                           """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task<string> ReadResourceAsync(string resourceName, CancellationToken ct)
    {
        await using var stream = _assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"No se pudo leer recurso embebido: {resourceName}");
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var text = await reader.ReadToEndAsync(ct);
        return text;
    }

    private async Task<string> ResolveSourceResourceIfAnyAsync(string scriptText, CancellationToken ct)
    {
        var match = SourceResourceRegex.Match(scriptText);
        if (!match.Success)
            return scriptText;

        var targetResource = match.Groups[1].Value.Trim();
        if (string.IsNullOrWhiteSpace(targetResource))
            return scriptText;

        _logger.LogInformation("InstallData script utiliza source-resource: {Resource}", targetResource);
        return await ReadResourceAsync(targetResource, ct);
    }

    private bool IsDestructiveScript(string sql)
        => DestructiveTagRegex.IsMatch(sql) || DestructiveSqlRegex.IsMatch(sql);

    private bool IsDestructiveConfirmed()
        => _options.AllowDestructive
           && string.Equals(
               (_options.ConfirmDestructive ?? string.Empty).Trim(),
               InstallDataOptions.DestructiveConfirmPhrase,
               StringComparison.Ordinal);

    private static string ComputeSha256(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private static async Task<string?> GetExistingChecksumAsync(
        NpgsqlConnection conn,
        string scriptName,
        CancellationToken ct)
    {
        const string sql = "SELECT checksum FROM install_data_scripts WHERE script_name = @name LIMIT 1;";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@name", scriptName);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result as string;
    }

    private static async Task ExecuteSqlAsync(
        NpgsqlConnection conn,
        NpgsqlTransaction tx,
        string sql,
        CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task RegisterAppliedAsync(
        NpgsqlConnection conn,
        NpgsqlTransaction tx,
        string scriptName,
        string checksum,
        bool isDestructive,
        CancellationToken ct)
    {
        const string sql = """
                           INSERT INTO install_data_scripts (script_name, checksum, is_destructive)
                           VALUES (@name, @checksum, @isDestructive)
                           ON CONFLICT (script_name) DO UPDATE
                               SET checksum = EXCLUDED.checksum,
                                   is_destructive = EXCLUDED.is_destructive,
                                   applied_at_utc = NOW();
                           """;

        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("@name", scriptName);
        cmd.Parameters.AddWithValue("@checksum", checksum);
        cmd.Parameters.AddWithValue("@isDestructive", isDestructive);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
