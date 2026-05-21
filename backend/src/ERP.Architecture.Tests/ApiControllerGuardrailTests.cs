using FluentAssertions;

namespace ERP.Architecture.Tests;

/// <summary>
/// B-05 / PR-1: controllers delgados sin ErpDbContext.
/// </summary>
public sealed class ApiControllerGuardrailTests
{
    [Fact]
    public void Controllers_must_not_reference_ErpDbContext()
    {
        var backendRoot = ResolveBackendSrcRoot();
        var controllersDir = Path.Combine(backendRoot, "ERP.API", "Controllers");
        Directory.Exists(controllersDir).Should().BeTrue();

        var violations = new List<string>();
        foreach (var file in Directory.EnumerateFiles(controllersDir, "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            if (text.Contains("ErpDbContext", StringComparison.Ordinal))
            {
                violations.Add(Path.GetRelativePath(backendRoot, file).Replace('\\', '/'));
            }
        }

        violations.Should().BeEmpty("use MediatR desde controllers; ErpDbContext solo en Infrastructure/Program startup");
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
