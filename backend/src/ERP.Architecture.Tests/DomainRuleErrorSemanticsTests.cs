using System.Text.RegularExpressions;
using FluentAssertions;

namespace ERP.Architecture.Tests;

/// <summary>
/// ZH-DOMAIN-RULE-ERROR-SSOT-01 — una regla de negocio viaja SOLO como
/// <c>DomainRuleViolationException</c> → <c>DomainRuleBehavior</c>/<c>Result.FromDomainRule</c>
/// (DOMAIN_RULE_VIOLATION, 422). <c>InvalidOperationException</c> significa error interno / estado
/// imposible / framework (500 sanitizado). Sin grandfather:
/// <list type="number">
/// <item>Application no captura <c>InvalidOperationException</c> para convertirla en Result (ni por
/// tipo ni por filtro <c>when</c>). Única frontera declarada: los parsers de documentos XML externos
/// (carpetas <c>Parsers</c>/<c>XmlParsing</c>), donde una excepción del framework al leer el
/// documento significa "XML mal formado", no una regla de dominio.</item>
/// <item>Domain solo lanza <c>InvalidOperationException</c> para invariantes internas, cuyo mensaje
/// empieza con "Invariante violada" — toda regla de negocio usa DomainRuleViolationException.</item>
/// <item>Una DomainRuleViolationException capturada no se re-traduce con
/// <c>ValidationFailure(ex.Message)</c>/<c>Failure(ex.Message)</c>: se usa <c>FromDomainRule</c>
/// (o se deja subir al behavior).</item>
/// </list>
/// </summary>
public sealed class DomainRuleErrorSemanticsTests
{
    private static readonly Regex CatchClause = new(
        @"catch\s*\(\s*(?<type>[\w.]+)(?:\s+(?<var>\w+))?\s*\)(?:\s*when\s*\((?<filter>(?:[^()]|\([^()]*\))*)\))?\s*\{",
        RegexOptions.Compiled
    );

    [Fact]
    public void Application_no_convierte_InvalidOperationException_en_Result()
    {
        var root = BackendSrcRoot();
        var violations = new List<string>();

        foreach (var (file, text) in SourceFiles(root, "ERP.Application"))
        {
            var parserBoundary = file.Contains("/Parsers/") || file.Contains("/XmlParsing/");
            foreach (Match m in CatchClause.Matches(text))
            {
                var type = m.Groups["type"].Value;
                var filter = m.Groups["filter"].Value;
                var body = Body(text, m.Index + m.Length - 1);
                var byType = type is "InvalidOperationException" or "System.InvalidOperationException";
                var byFilter = filter.Contains("InvalidOperationException") && body.Contains("Result<");
                if (byType || (byFilter && !parserBoundary))
                    violations.Add($"{Rel(root, file)}:{Line(text, m.Index)}: {m.Value.Trim()}");
            }
        }

        violations.Should().BeEmpty(
            "InvalidOperationException es técnica (500); una regla de negocio es DomainRuleViolationException y la traduce DomainRuleBehavior"
        );
    }

    [Fact]
    public void Domain_solo_usa_InvalidOperationException_para_invariantes_internas()
    {
        var root = BackendSrcRoot();
        var violations = new List<string>();

        foreach (var (file, text) in SourceFiles(root, "ERP.Domain"))
        {
            foreach (Match m in Regex.Matches(text, @"new\s+InvalidOperationException\(\s*"))
            {
                var message = text.Substring(m.Index + m.Length, Math.Min(40, text.Length - m.Index - m.Length));
                if (!Regex.IsMatch(message, @"^\$?@?""Invariante violada"))
                    violations.Add($"{Rel(root, file)}:{Line(text, m.Index)}: {message.Split('\n')[0]}");
            }
        }

        violations.Should().BeEmpty(
            "una regla de negocio del dominio se lanza como DomainRuleViolationException; InvalidOperationException solo para 'Invariante violada: …'"
        );
    }

    [Fact]
    public void Una_regla_capturada_no_se_retraduce_desde_su_texto()
    {
        var root = BackendSrcRoot();
        var violations = new List<string>();

        foreach (var (file, text) in SourceFiles(root, "ERP.Application"))
        {
            foreach (Match m in CatchClause.Matches(text))
            {
                if (!m.Groups["type"].Value.EndsWith("DomainRuleViolationException", StringComparison.Ordinal))
                    continue;
                var var = m.Groups["var"].Success ? m.Groups["var"].Value : "ex";
                var body = Body(text, m.Index + m.Length - 1);
                if (Regex.IsMatch(body, $@"\.(ValidationFailure|Failure)\(\s*{var}\.Message\s*\)"))
                    violations.Add($"{Rel(root, file)}:{Line(text, m.Index)}");
            }
        }

        violations.Should().BeEmpty("la traducción regla → Result es única: Result<T>.FromDomainRule(ex)");
    }

    private static string Body(string text, int openBrace)
    {
        var depth = 0;
        for (var i = openBrace; i < text.Length; i++)
        {
            if (text[i] == '{')
                depth++;
            else if (text[i] == '}' && --depth == 0)
                return text[(openBrace + 1)..i];
        }
        return text[openBrace..];
    }

    private static IEnumerable<(string File, string Text)> SourceFiles(string root, string project)
    {
        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, project), "*.cs", SearchOption.AllDirectories))
        {
            var normalized = file.Replace('\\', '/');
            if (normalized.Contains("/bin/") || normalized.Contains("/obj/"))
                continue;
            // Sin comentarios de línea: evitan falsos positivos en documentación.
            yield return (normalized, Regex.Replace(File.ReadAllText(file), @"//[^\n]*", ""));
        }
    }

    private static int Line(string text, int index) => text[..index].Count(c => c == '\n') + 1;

    private static string Rel(string root, string file) => Path.GetRelativePath(root, file).Replace('\\', '/');

    private static string BackendSrcRoot()
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
