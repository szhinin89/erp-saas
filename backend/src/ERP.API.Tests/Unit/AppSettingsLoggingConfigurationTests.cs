using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace ERP.API.Tests.Unit;

/// <summary>
/// ELECTRONIC-INVOICING-SRI-CONNECTIVITY-CHECK-SCOPE-01: valida que el override de logging que
/// silencia el ruido de diagnóstico por defecto de <c>HttpClientFactory</c> para el named client
/// "sri" (SocketException 10054 al pingear el WSDL del SRI, ya manejado por la app — ver
/// SriSoapClient.PingAsync) esté presente en appsettings.json, y que sea *aditivo*: no debe bajar
/// ni relajar ningún otro override de logging existente (Microsoft, Microsoft.Hosting.Lifetime).
/// </summary>
public sealed class AppSettingsLoggingConfigurationTests
{
    [Fact]
    public void AppSettings_json_scopes_sri_httpclient_logging_without_relaxing_other_overrides()
    {
        var overrides = ReadSerilogOverrides();

        overrides.Should().ContainKey("System.Net.Http.HttpClient.sri.LogicalHandler");
        overrides["System.Net.Http.HttpClient.sri.LogicalHandler"].Should().Be("Warning");

        overrides.Should().ContainKey("System.Net.Http.HttpClient.sri.ClientHandler");
        overrides["System.Net.Http.HttpClient.sri.ClientHandler"].Should().Be("Warning");

        // Overrides preexistentes — deben seguir intactos, nunca relajados por este cambio.
        overrides.Should().ContainKey("Microsoft");
        overrides["Microsoft"].Should().Be("Warning");
        overrides.Should().ContainKey("Microsoft.Hosting.Lifetime");
        overrides["Microsoft.Hosting.Lifetime"].Should().Be("Information");
    }

    private static Dictionary<string, string> ReadSerilogOverrides()
    {
        var path = FindAppSettingsPath();
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var overrideElement = doc
            .RootElement.GetProperty("Serilog")
            .GetProperty("MinimumLevel")
            .GetProperty("Override");

        return overrideElement
            .EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.GetString()!);
    }

    private static string FindAppSettingsPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "ERP.API", "appsettings.json");
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            "No se encontró src/ERP.API/appsettings.json subiendo desde " + AppContext.BaseDirectory
        );
    }
}
