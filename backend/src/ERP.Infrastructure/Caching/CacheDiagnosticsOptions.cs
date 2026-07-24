namespace ERP.Infrastructure.Caching;

public sealed class CacheDiagnosticsOptions
{
    public const string SectionName = "CacheDiagnostics";

    /// <summary>Activa logs CACHE HIT/MISS/SET para claves de alto valor (permisos, entitlements, queries).</summary>
    public bool LogEnabled { get; set; }

    /// <summary>Activa logs de snapshot IMemoryCache en ConfigService (cfg:*).</summary>
    public bool LogConfigSnapshot { get; set; }
}
