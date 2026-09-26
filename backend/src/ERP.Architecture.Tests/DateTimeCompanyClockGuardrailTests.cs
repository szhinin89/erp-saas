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

    // ══════════════════════════════════════════════════════════════════════════════════════
    // ZH-TEMPORAL-CONTRACT-SINGLE-SOURCE-02 — contrato temporal único (docs/architecture/
    // data-standards.md § Contrato temporal): fecha de negocio = DateOnly/date/"YYYY-MM-DD";
    // instante = DateTime Kind=Utc/timestamptz/"...Z". Extensión de este mismo guard, no uno paralelo.
    // ══════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Único punto donde una hora sin zona se interpreta o se construye a partir de un día:
    /// <c>CompanyTimeZone</c> (aritmética de Company.Timezone). <c>DateTime.SpecifyKind</c> fuera de
    /// aquí es exactamente el bug que etiquetaba una hora local como UTC (drift ±5h), y
    /// <c>DateOnly.ToDateTime</c> fuera de aquí convierte una fecha de negocio en un instante falso.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> TimeZoneMathOnlyIn = new Dictionary<
        string,
        string
    >(StringComparer.Ordinal)
    {
        ["ERP.Application/Common/Services/CompanyTimeZone.cs"] =
            "Única implementación de la aritmética Company.Timezone ⇄ UTC (ICompanyClock delega aquí).",
    };

    [Fact]
    public void SpecifyKind_y_DateOnly_ToDateTime_solo_en_CompanyTimeZone()
    {
        var violations = ScanProductionLines(
            (relative, line) =>
                !TimeZoneMathOnlyIn.ContainsKey(relative)
                && (
                    line.Contains("DateTime.SpecifyKind(", StringComparison.Ordinal)
                    || line.Contains(".ToDateTime(", StringComparison.Ordinal)
                ),
            "SpecifyKind/ToDateTime"
        );

        violations
            .Should()
            .BeEmpty(
                "una hora sin zona solo se convierte con ICompanyClock/CompanyTimeZone (hora de "
                    + "empresa → UTC) y una fecha de negocio viaja como DateOnly — nunca SpecifyKind "
                    + "ni DateOnly.ToDateTime.\n" + string.Join("\n", violations)
            );
    }

    [Fact]
    public void Ninguna_conversion_a_la_zona_del_servidor()
    {
        var violations = ScanProductionLines(
            (_, line) =>
                line.Contains(".ToLocalTime(", StringComparison.Ordinal)
                || line.Contains(".LocalDateTime", StringComparison.Ordinal),
            "ToLocalTime/LocalDateTime"
        );

        violations
            .Should()
            .BeEmpty(
                "la zona del servidor nunca es la de la empresa: presentar/derivar con "
                    + "ICompanyClock (Company.Timezone).\n" + string.Join("\n", violations)
            );
    }

    /// <summary>
    /// Todo parseo de DateTime/DateTimeOffset declara <c>DateTimeStyles</c> explícito — sin él, un
    /// valor con offset se convierte a la zona del SERVIDOR (Kind=Local) y uno sin zona queda
    /// ambiguo. Los instantes de la API pasan por <c>UtcDateTime.TryParseInstant</c>.
    /// </summary>
    [Fact]
    public void Parseo_de_DateTime_declara_DateTimeStyles_explicito()
    {
        var violations = new List<string>();
        foreach (var (relative, code) in ProductionFiles())
        {
            foreach (
                System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(
                    code,
                    @"\bDateTime(?:Offset)?\.(?:Try)?Parse(?:Exact)?\s*\("
                )
            )
            {
                var call = BalancedCall(code, match.Index + match.Length - 1);
                if (!call.Contains("DateTimeStyles", StringComparison.Ordinal))
                    violations.Add($"{relative}:{LineOf(code, match.Index)} — '{match.Value}' sin DateTimeStyles");
            }
        }

        violations.Should().BeEmpty(string.Join("\n", violations));
    }

    /// <summary>
    /// ZH-TEMPORAL-CONTRACT-02J — una fecha de negocio nunca se parsea con la cultura del servidor:
    /// <c>DateOnly.(Try)Parse(Exact)</c> declara <c>CultureInfo.InvariantCulture</c> (formatos
    /// externos fijos, p. ej. SRI "dd/MM/yyyy"). En la API una fecha de negocio llega tipada como
    /// <c>DateOnly</c> ("YYYY-MM-DD", System.Text.Json ISO estricto), sin parseo manual.
    /// </summary>
    [Fact]
    public void Parseo_de_DateOnly_usa_InvariantCulture()
    {
        var violations = new List<string>();
        foreach (var (relative, code) in ProductionFiles())
        {
            foreach (
                System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(
                    code,
                    @"\bDateOnly\.(?:Try)?Parse(?:Exact)?\s*\("
                )
            )
            {
                var call = BalancedCall(code, match.Index + match.Length - 1);
                if (!call.Contains("InvariantCulture", StringComparison.Ordinal))
                    violations.Add($"{relative}:{LineOf(code, match.Index)} — '{match.Value}' sin InvariantCulture");
            }
        }

        violations.Should().BeEmpty(string.Join("\n", violations));
    }

    /// <summary>
    /// ZH-TEMPORAL-CONTRACT-02J — solo dos representaciones temporales productivas: DateOnly (fecha
    /// de negocio) y DateTime Kind=Utc (instante, "…Z"). Se prohíbe declarar <c>DateTimeOffset</c>
    /// (serializa "+00:00") y declarar fechas/instantes como <c>string</c> en contratos
    /// (<c>string …Date/…At/…Utc</c>) — ambos fueron terceras formas reales (ApiResponse.Timestamp,
    /// TransferDate/CashDate de Ventas). Usos técnicos de <c>DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()</c>
    /// no declaran el tipo y quedan fuera.
    /// </summary>
    [Fact]
    public void Solo_DateOnly_y_DateTime_UTC_como_tipos_temporales_declarados()
    {
        var violations = new List<string>();
        foreach (var (relative, code) in ProductionFiles())
        {
            foreach (
                System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(
                    code,
                    @"\bDateTimeOffset\??\s+\w+\s*[,;)={]"
                )
            )
                violations.Add($"{relative}:{LineOf(code, match.Index)} — DateTimeOffset declarado: '{match.Value.Trim()}'");

            foreach (
                System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(
                    code,
                    @"(?<!const\s)\bstring\??\s+\w*(?:Date|At|Utc)\s*[,)=;{]"
                )
            )
                violations.Add($"{relative}:{LineOf(code, match.Index)} — fecha como string: '{match.Value.Trim()}'");
        }

        violations
            .Should()
            .BeEmpty(
                "fecha de negocio = DateOnly; instante = DateTime Kind=Utc.\n" + string.Join("\n", violations)
            );
    }

    /// <summary>
    /// <c>[FromQuery] DateTime</c> solo para filtros instante explícitos (nombre terminado en
    /// <c>Utc</c>, valor ISO con zona validado por UtcInstantModelBinder). Un filtro por día es
    /// fecha de negocio: <c>DateOnly</c> (y si filtra un instante, ICompanyClock.DayUtcRangeAsync).
    /// </summary>
    [Fact]
    public void FromQuery_DateTime_solo_como_filtro_instante_Utc_explicito()
    {
        var violations = new List<string>();
        foreach (var (relative, code) in ProductionFiles())
        {
            foreach (
                System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(
                    code,
                    @"\[(?:[\w.]*\.)?FromQuery[^\]]*\]\s*DateTime\??\s+(\w+)"
                )
            )
            {
                if (!match.Groups[1].Value.EndsWith("Utc", StringComparison.Ordinal))
                    violations.Add($"{relative}:{LineOf(code, match.Index)} — '{match.Value}'");
            }
        }

        violations
            .Should()
            .BeEmpty(
                "filtro por día = [FromQuery] DateOnly; filtro instante = [FromQuery] DateTime "
                    + "<nombre>Utc.\n" + string.Join("\n", violations)
            );
    }

    [Fact]
    public void Borde_HTTP_registra_el_contrato_de_instante_UTC()
    {
        var program = File.ReadAllText(Path.Combine(ResolveBackendSrcRoot(), "ERP.API", "Program.cs"));

        program.Should().Contain("UtcInstantModelBinderProvider", "DateTime en query/route exige zona explícita");
        program.Should().Contain("UtcInstantJsonConverter", "DateTime en JSON exige/emite UTC con 'Z'");
    }

    private static List<string> ScanProductionLines(Func<string, string, bool> isViolation, string label)
    {
        var violations = new List<string>();
        foreach (var (relative, file) in ProductionFilePaths())
        {
            foreach (var (line, lineNumber) in ReadCodeLines(file))
            {
                if (isViolation(relative, line))
                    violations.Add($"{relative}:{lineNumber} — {label}: {line.Trim()}");
            }
        }
        return violations;
    }

    private static IEnumerable<(string Relative, string File)> ProductionFilePaths()
    {
        var backendSrcRoot = ResolveBackendSrcRoot();
        var sep = Path.DirectorySeparatorChar;
        foreach (var project in ScannedProjects)
        {
            var projectDir = Path.Combine(backendSrcRoot, project);
            if (!Directory.Exists(projectDir))
                continue;
            foreach (var file in Directory.EnumerateFiles(projectDir, "*.cs", SearchOption.AllDirectories))
            {
                if (
                    file.Contains($"{sep}bin{sep}", StringComparison.Ordinal)
                    || file.Contains($"{sep}obj{sep}", StringComparison.Ordinal)
                    || file.Contains($"{sep}Migrations{sep}", StringComparison.Ordinal)
                )
                    continue;
                yield return (Path.GetRelativePath(backendSrcRoot, file).Replace('\\', '/'), file);
            }
        }
    }

    /// <summary>Código del archivo sin comentarios <c>//</c> de línea completa (líneas preservadas).</summary>
    private static IEnumerable<(string Relative, string Code)> ProductionFiles()
    {
        foreach (var (relative, file) in ProductionFilePaths())
        {
            var code = string.Join(
                "\n",
                File.ReadAllLines(file)
                    .Select(l => l.TrimStart().StartsWith("//", StringComparison.Ordinal) ? string.Empty : l)
            );
            yield return (relative, code);
        }
    }

    private static string BalancedCall(string code, int openParen)
    {
        var depth = 0;
        for (var i = openParen; i < code.Length; i++)
        {
            if (code[i] == '(')
                depth++;
            else if (code[i] == ')' && --depth == 0)
                return code[openParen..(i + 1)];
        }
        return code[openParen..];
    }

    private static int LineOf(string code, int index) => code[..index].Count(c => c == '\n') + 1;

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
