using FluentAssertions;

namespace ERP.Architecture.Tests;

/// <summary>
/// DATETIME-COMPANY-CLOCK-GLOBAL-FIX-01 — guardrail que evita que el patrón que causó
/// PRICE-LIST-COMPANY-CLOCK-01 (vigencia de PriceList evaluada contra <c>DateTime.UtcNow</c> en
/// vez del día operativo de la empresa) y sus 15+ hermanos (Accounting Posting Translators,
/// Kardex, catálogo SRI, reportes) vuelva a aparecer. Regla del ticket: UTC crudo nunca decide una
/// fecha de negocio (vigencia, período contable, fecha de movimiento de Kardex, "hoy" de un
/// reporte) — eso pasa siempre por <c>ICompanyClock</c>. UTC sigue siendo correcto para
/// CreatedAt/UpdatedAt/*AtUtc, tokens, certificados, locks y el propio
/// <c>BaseDomainEvent.OccurredOn</c> — ninguno de esos usa los patrones prohibidos de este test
/// (son asignaciones directas de <c>DateTime.UtcNow</c>, no las expresiones que derivan una fecha
/// de negocio de él), así que no necesitan allowlist.
/// </summary>
public sealed class DateTimeCompanyClockGuardrailTests
{
    /// <summary>
    /// Cada patrón deriva una fecha de negocio (DateOnly/"hoy") directamente de un reloj crudo,
    /// nunca de <c>ICompanyClock</c> — exactamente la forma del bug de PRICE-LIST-COMPANY-CLOCK-01.
    /// </summary>
    private static readonly string[] ForbiddenPatterns =
    {
        "DateOnly.FromDateTime(DateTime.UtcNow)",
        "DateTime.UtcNow.Date",
        "DateTimeOffset.UtcNow.Date",
        "DateTime.Now",
        "DateTime.Today",
        "DateTimeOffset.Now",
    };

    /// <summary>
    /// Excepciones justificadas y documentadas — nunca "no tuve tiempo de arreglarlo". Cada entrada
    /// es (ruta relativa a backend/src, motivo). Antes de agregar una nueva, confirmar que
    /// realmente es un uso técnico (auditoría/token/certificado/lock), no una fecha de negocio.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> AllowedFiles = new Dictionary<
        string,
        string
    >(StringComparer.Ordinal)
    {
        // X509Certificate2.NotBefore/NotAfter son expuestos por .NET en hora LOCAL (comportamiento
        // documentado de la API, no un bug) — comparar contra DateTime.Now es el patrón correcto
        // aquí, nunca DateTime.UtcNow. No es una fecha de negocio: es vigencia de certificado.
        ["ERP.Infrastructure/Services/Sri/XadesBesSigner.cs"] =
            "Comparación contra X509Certificate2.NotBefore/NotAfter, expuestos en hora local por .NET — no es fecha de negocio.",
    };

    /// <summary>Proyectos de producción auditados por DATETIME-COMPANY-CLOCK-GLOBAL-FIX-01 — los *.Tests quedan fuera (dobles de prueba con fecha fija, no lógica de negocio real).</summary>
    private static readonly string[] ScannedProjects =
    {
        "ERP.Domain",
        "ERP.Application",
        "ERP.Infrastructure",
        "ERP.API",
    };

    [Fact]
    public void Ningun_uso_de_reloj_crudo_deriva_una_fecha_de_negocio_fuera_de_ICompanyClock()
    {
        var backendSrcRoot = ResolveBackendSrcRoot();
        var violations = new List<string>();

        foreach (var project in ScannedProjects)
        {
            var projectDir = Path.Combine(backendSrcRoot, project);
            if (!Directory.Exists(projectDir))
                continue;

            foreach (var file in Directory.EnumerateFiles(projectDir, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                    continue;
                if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                    continue;
                if (file.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                    continue;

                var relative = Path.GetRelativePath(backendSrcRoot, file).Replace('\\', '/');
                if (AllowedFiles.ContainsKey(relative))
                    continue;

                foreach (var (line, lineNumber) in ReadCodeLines(file))
                {
                    foreach (var pattern in ForbiddenPatterns)
                    {
                        if (line.Contains(pattern, StringComparison.Ordinal))
                        {
                            violations.Add($"{relative}:{lineNumber} — '{pattern}'");
                        }
                    }
                }
            }
        }

        violations
            .Should()
            .BeEmpty(
                "una fecha de negocio (vigencia/período contable/Kardex/\"hoy\" de reporte) nunca "
                    + "se deriva de un reloj crudo (DateTime.UtcNow/.Now/.Today) — debe resolverse vía "
                    + "ICompanyClock.TodayAsync/LocalDateAsync (día operativo de la empresa). Si el "
                    + "hallazgo es un uso técnico legítimo (auditoría, token, certificado, lock), "
                    + "agregarlo a AllowedFiles con el motivo documentado — nunca silenciar sin "
                    + "justificación.\n" + string.Join("\n", violations)
            );
    }

    /// <summary>
    /// Líneas de código real, excluyendo comentarios de una línea (`//`, `///`) — este propio
    /// archivo y varios ya corregidos documentan el patrón prohibido en prosa como ejemplo de qué
    /// NO hacer, y eso no debe contarse como una violación real.
    /// </summary>
    private static IEnumerable<(string Line, int LineNumber)> ReadCodeLines(string file)
    {
        var lines = File.ReadAllLines(file);
        for (var i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].TrimStart();
            if (trimmed.StartsWith("//", StringComparison.Ordinal))
                continue;
            yield return (lines[i], i + 1);
        }
    }

    private static string ResolveBackendSrcRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "ERP.API")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("No se encontró backend/src (ERP.API).");
    }
}
