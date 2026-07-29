using ERP.Application.Modules.ElectronicDocuments.SchemaValidation;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Xml;
using System.Xml.Schema;

namespace ERP.Infrastructure.Services.ElectronicDocuments;

/// <summary>
/// Resuelve XSD oficiales del SRI incorporados como <c>EmbeddedResource</c> del propio
/// ensamblado (ver <c>ElectronicDocuments/Resources/SRI/</c>, glob en
/// <c>ERP.Infrastructure.csproj</c>), guiado por <c>manifest.json</c> — nunca por una
/// convención de nombre de archivo derivada del tipo/versión: los XSD del SRI no siguen un
/// patrón de nombre consistente entre comprobantes (p.ej. "factura_V1.1.0.xsd" vs.
/// "ComprobanteRetencion_V2.0.0.xsd"), así que el manifiesto es la única fuente de verdad de
/// qué archivo físico corresponde a cada (tipo, versión), incluidas sus dependencias
/// (<c>xs:import</c>/<c>xs:include</c> — p.ej. el esquema de firma XML compartido en
/// <c>Common/</c>). Nunca se descargan recursos de Internet en tiempo de ejecución.
///
/// Registrado como singleton: una vez compilado, un <see cref="XmlSchemaSet"/> es seguro para
/// lecturas concurrentes (todas las validaciones solo leen, nunca lo modifican), así que la
/// caché interna evita recompilar el esquema en cada validación — transparente para quien
/// consume <see cref="IXmlSchemaProvider"/>.
/// </summary>
public sealed partial class EmbeddedXmlSchemaProvider : IXmlSchemaProvider
{
    private const string ManifestResourceName = "ElectronicDocuments.Resources.SRI.manifest.json";
    private const string ResourceRootPrefix = "ElectronicDocuments.Resources.SRI.";

    private static readonly JsonSerializerOptions ManifestSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly ConcurrentDictionary<(ElectronicDocumentType DocumentType, string SchemaVersion), XmlSchemaSet?> _cache = new();
    private readonly Lazy<SriResourceManifest?> _manifest;
    private readonly ILogger<EmbeddedXmlSchemaProvider> _logger;

    public EmbeddedXmlSchemaProvider(ILogger<EmbeddedXmlSchemaProvider> logger)
    {
        _logger = logger;
        _manifest = new Lazy<SriResourceManifest?>(LoadManifest);
    }

    public Task<XmlSchemaSet?> GetSchemaSetAsync(
        ElectronicDocumentType documentType, string schemaVersion, CancellationToken ct = default)
    {
        var schemaSet = _cache.GetOrAdd((documentType, schemaVersion), key => LoadSchemaSet(key.DocumentType, key.SchemaVersion));
        return Task.FromResult(schemaSet);
    }

    private SriResourceManifest? LoadManifest()
    {
        var assembly = typeof(EmbeddedXmlSchemaProvider).Assembly;
        var resourceName = $"{assembly.GetName().Name}.{ManifestResourceName}";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            LogManifestMissing(resourceName);
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<SriResourceManifest>(stream, ManifestSerializerOptions);
        }
        catch (JsonException ex)
        {
            LogManifestInvalid(resourceName, ex.Message);
            return null;
        }
    }

    private XmlSchemaSet? LoadSchemaSet(ElectronicDocumentType documentType, string schemaVersion)
    {
        var manifest = _manifest.Value;
        if (manifest is null)
            return null;

        if (!manifest.DocumentTypes.TryGetValue(documentType.ToString(), out var typeEntry))
        {
            LogNoManifestEntryForType(documentType);
            return null;
        }

        var versionEntry = typeEntry.Versions.FirstOrDefault(v => v.Version == schemaVersion);
        if (versionEntry is null)
        {
            LogNoManifestEntryForVersion(documentType, schemaVersion);
            return null;
        }

        var schemaSet = new XmlSchemaSet();
        var relativePaths = new[] { versionEntry.Xsd }.Concat(versionEntry.Dependencies ?? []);
        foreach (var relativePath in relativePaths)
        {
            var schema = LoadEmbeddedSchema(relativePath);
            // Cualquier dependencia faltante (incluida la principal) invalida el set completo —
            // nunca se compila una validación parcial que dé una falsa sensación de cobertura.
            if (schema is null)
                return null;
            schemaSet.Add(schema);
        }

        try
        {
            schemaSet.Compile();
            return schemaSet;
        }
        catch (XmlSchemaException ex)
        {
            LogSchemaSetCompileFailed(documentType, schemaVersion, ex.Message);
            return null;
        }
    }

    private XmlSchema? LoadEmbeddedSchema(string manifestRelativePath)
    {
        var assembly = typeof(EmbeddedXmlSchemaProvider).Assembly;
        var resourceName = $"{assembly.GetName().Name}.{ResourceRootPrefix}{manifestRelativePath.Replace('/', '.')}";

        using var resourceStream = assembly.GetManifestResourceStream(resourceName);
        if (resourceStream is null)
        {
            LogResourceMissing(resourceName);
            return null;
        }

        try
        {
            using var xmlReader = XmlReader.Create(resourceStream);
            var schema = XmlSchema.Read(xmlReader, (_, e) => LogSchemaReadWarning(resourceName, e.Message));
            if (schema is null)
                LogSchemaUnreadable(resourceName);
            return schema;
        }
        catch (XmlException ex)
        {
            LogSchemaMalformed(resourceName, ex.Message);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "No se encontró el manifiesto de recursos SRI '{ResourceName}'.")]
    private partial void LogManifestMissing(string resourceName);

    [LoggerMessage(Level = LogLevel.Error, Message = "El manifiesto de recursos SRI '{ResourceName}' no es JSON válido: {Reason}")]
    private partial void LogManifestInvalid(string resourceName, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "El manifiesto de recursos SRI no tiene ninguna entrada para el tipo de documento {DocumentType}.")]
    private partial void LogNoManifestEntryForType(ElectronicDocumentType documentType);

    [LoggerMessage(Level = LogLevel.Information, Message = "El manifiesto de recursos SRI no tiene la versión {SchemaVersion} para {DocumentType}.")]
    private partial void LogNoManifestEntryForVersion(ElectronicDocumentType documentType, string schemaVersion);

    [LoggerMessage(Level = LogLevel.Information, Message = "El recurso XSD '{ResourceName}' todavía no fue incorporado al proyecto.")]
    private partial void LogResourceMissing(string resourceName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Advertencia al leer el esquema XSD '{ResourceName}': {Message}")]
    private partial void LogSchemaReadWarning(string resourceName, string message);

    [LoggerMessage(Level = LogLevel.Error, Message = "No se pudo leer el esquema XSD '{ResourceName}'.")]
    private partial void LogSchemaUnreadable(string resourceName);

    [LoggerMessage(Level = LogLevel.Error, Message = "El recurso XSD '{ResourceName}' no es un XML bien formado: {Reason}")]
    private partial void LogSchemaMalformed(string resourceName, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "El conjunto de esquemas SRI para {DocumentType} v{SchemaVersion} no compiló: {Reason}")]
    private partial void LogSchemaSetCompileFailed(ElectronicDocumentType documentType, string schemaVersion, string reason);
}

/// <summary>Forma de <c>Resources/SRI/manifest.json</c> — única fuente de verdad de qué archivo físico corresponde a cada (tipo, versión).</summary>
internal sealed class SriResourceManifest
{
    public Dictionary<string, SriDocumentTypeEntry> DocumentTypes { get; set; } = new();
}

internal sealed class SriDocumentTypeEntry
{
    public string? ActiveVersion { get; set; }
    public List<SriSchemaVersionEntry> Versions { get; set; } = new();
}

internal sealed class SriSchemaVersionEntry
{
    public string Version { get; set; } = string.Empty;
    public string Xsd { get; set; } = string.Empty;
    public List<string>? Dependencies { get; set; }
}
