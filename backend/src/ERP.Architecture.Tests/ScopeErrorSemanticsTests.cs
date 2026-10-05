using System.Text.RegularExpressions;
using FluentAssertions;

namespace ERP.Architecture.Tests;

/// <summary>
/// ZH-SCOPE-ERROR-SEMANTICS-01 — la semántica de scope viaja por <c>Code</c>, nunca por el texto
/// del error. Dos reglas acotadas (no es un checker global de strings):
/// <list type="number">
/// <item>Los <c>*ScopeBehavior</c> y <c>*AccessGuard</c> no comparan <c>Error</c>/<c>Message</c>
/// (<c>Contains</c>, <c>StartsWith</c>, <c>Equals</c>, <c>==</c>…).</item>
/// <item>Un resultado obtenido de un guard de acceso (<c>Require*Async</c> de
/// ICompanyAccessGuard / IBranchAccessGuard / IInterBranchAccessGuard) nunca se re-envuelve con solo
/// su mensaje (<c>Result.Failure(access.Error!)</c>): hay que conservar <c>access.Code</c>.</item>
/// </list>
/// </summary>
public sealed class ScopeErrorSemanticsTests
{
    private static readonly Regex TextComparison = new(
        @"\.(Error|Message)\??\.(Contains|StartsWith|EndsWith|Equals|IndexOf)\(|\.(Error|Message)\s*[!=]=\s*""",
        RegexOptions.Compiled
    );

    private static readonly Regex GuardCall = new(
        @"var\s+(\w+)\s*=\s*await\s+[\w.]+\.Require(ActiveTenant|Membership|CurrentCompany|Branch|CurrentBranch|InterBranchAccess)Async\(",
        RegexOptions.Compiled
    );

    [Fact]
    public void ScopeBehaviors_y_AccessGuards_no_deciden_por_el_texto_del_error()
    {
        var root = BackendSrcRoot();
        var files = Directory
            .EnumerateFiles(Path.Combine(root, "ERP.Application", "Behaviors"), "*ScopeBehavior.cs")
            .Concat(
                Directory.EnumerateFiles(
                    Path.Combine(root, "ERP.Infrastructure", "Services"),
                    "*AccessGuard.cs"
                )
            )
            .ToList();

        files.Should().NotBeEmpty();
        var violations = files
            .SelectMany(f => File.ReadAllLines(f).Select((line, i) => (f, line, i)))
            .Where(x =>
                !x.line.TrimStart().StartsWith("//", StringComparison.Ordinal)
                && TextComparison.IsMatch(x.line)
            )
            .Select(x => $"{Path.GetRelativePath(root, x.f)}:{x.i + 1}: {x.line.Trim()}")
            .ToList();

        violations
            .Should()
            .BeEmpty("la decisión de scope usa Code/estado explícito, nunca el texto del error");
    }

    [Fact]
    public void Resultado_de_guard_de_acceso_nunca_se_reenvuelve_perdiendo_el_Code()
    {
        var root = BackendSrcRoot();
        var violations = new List<string>();

        foreach (var dir in new[] { "ERP.Application", "ERP.Infrastructure", "ERP.API" })
        {
            foreach (
                var file in Directory.EnumerateFiles(
                    Path.Combine(root, dir),
                    "*.cs",
                    SearchOption.AllDirectories
                )
            )
            {
                if (
                    file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                    || file.Contains(
                        $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"
                    )
                )
                    continue;

                var text = File.ReadAllText(file);
                foreach (
                    var name in GuardCall.Matches(text).Select(m => m.Groups[1].Value).Distinct()
                )
                {
                    var rewrap = new Regex(
                        $@"\.(Failure|Forbidden|NotFound|ValidationFailure|Conflict)\(\s*{name}\.Error\s*(!|\?\?[^,)]*)?\s*\)"
                    );
                    foreach (Match m in rewrap.Matches(text))
                    {
                        var line = text[..m.Index].Count(c => c == '\n') + 1;
                        violations.Add($"{Path.GetRelativePath(root, file)}:{line}: {m.Value}");
                    }
                }
            }
        }

        violations
            .Should()
            .BeEmpty(
                "el Code del guard debe llegar al mapper HTTP: usar Failure(x.Error!, x.Code)"
            );
    }

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
