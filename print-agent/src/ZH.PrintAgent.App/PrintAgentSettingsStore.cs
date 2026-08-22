using System.Security.Cryptography;
using System.Text.Json;

namespace ZH.PrintAgent.App;

public static class PrintAgentSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static async Task EnsureCreatedAsync(string settingsFilePath, PrintAgentOptions inheritedOptions)
    {
        if (File.Exists(settingsFilePath))
        {
            return;
        }

        var hasRealApiKey =
            !string.IsNullOrWhiteSpace(inheritedOptions.ApiKey) &&
            !string.Equals(inheritedOptions.ApiKey, PrintAgentStartupValidator.DevelopmentApiKey, StringComparison.Ordinal) &&
            !string.Equals(inheritedOptions.ApiKey, PrintAgentStartupValidator.SampleProductionApiKey, StringComparison.Ordinal);

        var seeded = inheritedOptions with
        {
            SetupCompleted = inheritedOptions.SetupCompleted || hasRealApiKey
        };

        await SaveAsync(settingsFilePath, seeded);
    }

    public static async Task<PrintAgentOptions?> LoadAsync(string settingsFilePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(settingsFilePath))
        {
            return null;
        }

        await using var stream = File.OpenRead(settingsFilePath);
        var document = await JsonSerializer.DeserializeAsync<SettingsFile>(stream, JsonOptions, cancellationToken);
        return document?.PrintAgent;
    }

    public static async Task SaveAsync(string settingsFilePath, PrintAgentOptions options, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(settingsFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var backupPath = settingsFilePath + ".bak";
        if (File.Exists(settingsFilePath))
        {
            File.Copy(settingsFilePath, backupPath, overwrite: true);
        }

        var temporaryPath = settingsFilePath + ".tmp";
        var document = new SettingsFile { PrintAgent = options };
        await using (var stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(stream, document, JsonOptions, cancellationToken);
        }

        File.Move(temporaryPath, settingsFilePath, overwrite: true);
    }

    public static string GenerateApiKey()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private sealed record SettingsFile
    {
        public PrintAgentOptions? PrintAgent { get; init; }
    }
}
