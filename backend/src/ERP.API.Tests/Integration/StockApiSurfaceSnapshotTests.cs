using ERP.API.Attributes;
using ERP.API.Tests.Support;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ERP.API.Tests.Integration;

/// <summary>
/// ZH-API-THIN-STOCK-01 — superficie pública de <c>/api/v1/inventory/stock*</c> fijada antes de
/// separar StockController por responsabilidad (consultas / ajustes / transferencias): rutas +
/// métodos + policies efectivas, documento OpenAPI de esas rutas (más cualquier tag global que
/// nombre a Stock) y las filas de AppFeature de inventario. Solo superficie de Stock.
/// Regenerar SOLO si el cambio de contrato es intencional: ZH_UPDATE_SNAPSHOTS=1.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class StockApiSurfaceSnapshotTests : IAsyncLifetime
{
    private const string RoutePrefix = "api/v1/inventory/stock";
    private readonly PostgreSqlTestWebAppFactory _factory = new();

    public async Task InitializeAsync()
    {
        await _factory.InitializeAsync();
        await _factory.MigrateAsync();
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private static string SnapshotPath(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "ERP.API.Tests")))
            dir = dir.Parent;
        dir.Should().NotBeNull("no se encontró backend/src/ERP.API.Tests");
        return Path.Combine(dir!.FullName, "ERP.API.Tests", "Snapshots", name);
    }

    private static void MatchSnapshot(string name, string actual)
    {
        var path = SnapshotPath(name);
        if (Environment.GetEnvironmentVariable("ZH_UPDATE_SNAPSHOTS") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, actual, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return;
        }

        File.Exists(path).Should().BeTrue($"falta el snapshot {name}");
        actual.Should().Be(File.ReadAllText(path).ReplaceLineEndings("\n"), $"la superficie de {name} cambió");
    }

    private static JsonNode? Canonical(JsonNode? node) =>
        node switch
        {
            JsonObject o => new JsonObject(o.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => KeyValuePair.Create(p.Key, Canonical(p.Value)))),
            JsonArray a => new JsonArray(a.Select(Canonical).ToArray()),
            null => null,
            _ => JsonNode.Parse(node.ToJsonString()),
        };

    [Fact]
    public void Rutas_metodos_y_policies_efectivas_de_Stock_no_cambian()
    {
        var endpoints = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.TrimStart('/').StartsWith(RoutePrefix, StringComparison.Ordinal) == true)
            .Select(e =>
            {
                var methods = string.Join(",", e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? []);
                var policies = e.Metadata.GetOrderedMetadata<IAuthorizeData>()
                    .Select(a => a.Policy ?? "(default)")
                    .OrderBy(p => p, StringComparer.Ordinal);
                var anonymous = e.Metadata.GetMetadata<IAllowAnonymous>() is not null ? " [AllowAnonymous]" : "";
                return $"{methods} /{e.RoutePattern.RawText!.TrimStart('/')} policies=[{string.Join(", ", policies)}]{anonymous}";
            })
            .OrderBy(l => l, StringComparer.Ordinal)
            .ToList();

        endpoints.Should().HaveCount(13);
        MatchSnapshot("stock-routes.txt", string.Join("\n", endpoints) + "\n");
    }

    [Fact]
    public async Task Documento_OpenAPI_de_Stock_no_cambia()
    {
        var json = JsonNode.Parse(await _factory.CreateClient().GetStringAsync("/swagger/v1/swagger.json"))!;

        var paths = new JsonObject(json["paths"]!.AsObject()
            .Where(p => p.Key.StartsWith("/" + RoutePrefix, StringComparison.Ordinal))
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => KeyValuePair.Create(p.Key, Canonical(p.Value))));
        // Tags globales que mencionen Stock: un controller nuevo con <summary> XML de clase
        // agregaría uno aquí (hoy no hay ninguno).
        var stockDocumentTags = new JsonArray((json["tags"]?.AsArray() ?? [])
            .Where(t => t?["name"]?.GetValue<string>().Contains("Stock", StringComparison.OrdinalIgnoreCase) == true)
            .Select(Canonical)
            .ToArray());

        paths.Count.Should().BeGreaterThan(0);
        var surface = new JsonObject { ["paths"] = paths, ["stockDocumentTags"] = stockDocumentTags };
        MatchSnapshot("stock-openapi.json", surface.ToJsonString(new JsonSerializerOptions { WriteIndented = true }).ReplaceLineEndings("\n") + "\n");
    }

    [Fact]
    public void Filas_de_AppFeature_de_inventario_no_cambian()
    {
        // Misma regla que AppFeatureDiscoveryService: fila por [AppFeature] de clase y de acción HTTP;
        // el padre de una acción es su ParentPermission explícito o el permiso del [AppFeature] de clase.
        var rows = new List<string>();
        foreach (var type in typeof(Program).Assembly.GetTypes()
                     .Where(t => t.IsPublic && !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t)))
        {
            var classAttr = type.GetCustomAttribute<AppFeatureAttribute>(inherit: true);
            if (classAttr is not null && !string.IsNullOrWhiteSpace(classAttr.Permission))
                rows.Add(Row(classAttr, classAttr.ParentPermission));

            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                if (!method.GetCustomAttributes(inherit: true).OfType<HttpMethodAttribute>().Any())
                    continue;
                var methodAttr = method.GetCustomAttribute<AppFeatureAttribute>(inherit: false);
                if (methodAttr is null || string.IsNullOrWhiteSpace(methodAttr.Permission))
                    continue;
                rows.Add(Row(methodAttr, !string.IsNullOrWhiteSpace(methodAttr.ParentPermission) ? methodAttr.ParentPermission : classAttr?.Permission));
            }
        }

        var inventory = rows.Where(r => r.StartsWith("perm:inventory.", StringComparison.Ordinal))
            .Distinct().OrderBy(r => r, StringComparer.Ordinal).ToList();
        inventory.Should().NotBeEmpty();
        MatchSnapshot("stock-app-features.txt", string.Join("\n", inventory) + "\n");

        static string Row(AppFeatureAttribute a, string? parent) =>
            $"{a.Permission.Trim()} | {a.Name} | {a.Icon} | {a.Path} | parent={parent?.Trim()} | sort={a.SortOrder} | menu={a.IsVisibleInMenu}";
    }
}
