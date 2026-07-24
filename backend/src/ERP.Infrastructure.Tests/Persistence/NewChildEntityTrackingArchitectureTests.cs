using System.Text.RegularExpressions;
using FluentAssertions;

namespace ERP.Infrastructure.Tests.Persistence;

/// <summary>
/// Gate de arquitectura — invariante del que depende NewChildEntityTrackingInterceptor.
///
/// REGLA ABSOLUTA: ningún módulo puede reatachar una entidad detached con ID real
/// (DbSet&lt;T&gt;.Attach() / DbSet&lt;T&gt;.Update()) sin que esa entidad haya pasado antes
/// por una query trackeada en el mismo DbContext.
///
/// Razón: NewChildEntityTrackingInterceptor decide si una entidad Modified-sin-diff-real
/// es "nueva" (la corrige a Added) o "anómala" (lanza excepción) basándose en
/// ErpDbContext.WasTrackedFromQuery(). Esa señal solo es confiable si ningún código
/// de producción inyecta una entidad directamente al ChangeTracker como Modified —
/// vía Attach()/Update() — sin pasar primero por una query. Si esa regla se rompe,
/// una entidad legítimamente existente pero reatachada "a ciegas" sería indistinguible
/// de una entidad nueva descubierta por fixup de navegación.
///
/// Guard CI-bloqueante único: ATT-GATE-01.
/// </summary>
public sealed class NewChildEntityTrackingArchitectureTests
{
    // Repositorios cuyo patrón de "upsert por reemplazo total" llama deliberadamente
    // a DbSet.Update()/Attach() sobre una entidad de catálogo/configuración simple,
    // sin colecciones de navegación hijas — no expuestos al bug que corrige el
    // interceptor (no hay fixup de grafo posible: la entidad no tiene hijos).
    private static readonly HashSet<string> AllowedAttachOrUpdateCallers =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "src/ERP.Infrastructure/MasterData/Repositories/PaymentTermRepository.cs",
            "src/ERP.Infrastructure/Persistence/Repositories/Sales/PaymentMethodRepository.cs",
            "src/ERP.Infrastructure/Persistence/Repositories/SriSettingsRepository.cs",
            "src/ERP.Infrastructure/Persistence/Repositories/Items/ItemTypeRepository.cs",
        };

    /// <summary>
    /// Ningún archivo de producción fuera de la lista autorizada puede llamar a
    /// DbSet&lt;T&gt;.Attach(entity) o DbSet&lt;T&gt;.Update(entity) sobre una propiedad de
    /// DbContext (p. ej. _db.Items.Update(x), context.Set&lt;T&gt;().Attach(x)).
    ///
    /// El patrón excluye deliberadamente las llamadas a métodos de dominio homónimos
    /// (p. ej. entity.Update(name, ...)) exigiendo que el receptor sea un acceso a
    /// miembro de DbContext (_db./context./Database.) seguido de un nombre de
    /// propiedad DbSet y .Attach(/.Update( — no una sola palabra "Update(".
    /// </summary>
    [Fact]
    public void ATT_GATE_01_no_blind_reattach_of_detached_entities_outside_allowed_repositories()
    {
        var pattern = new Regex(
            @"(_db|_context|context)\.\w+\.(Attach|Update)\(",
            RegexOptions.Compiled);

        var violations = ScanForRegex(pattern, AllowedAttachOrUpdateCallers);

        violations.Should().BeEmpty(
            "Reatachar una entidad detached con ID real (Attach/Update sobre un DbSet) " +
            "sin que haya pasado antes por una query trackeada en este DbContext rompe el " +
            "invariante del que depende NewChildEntityTrackingInterceptor: una entidad " +
            "legítimamente existente pero reatachada 'a ciegas' queda indistinguible de una " +
            "entidad nueva descubierta por fixup de navegación. Si el módulo necesita " +
            "actualizar una entidad existente, debe cargarla primero con una query en este " +
            "DbContext (lo que la registra como query-tracked) y mutarla con sus métodos de " +
            "dominio — nunca Attach()/Update() directo sobre un DbSet.");
    }

    // ── Infraestructura de scan ───────────────────────────────────────────────

    private static List<string> ScanForRegex(Regex pattern, HashSet<string> allowedPaths)
    {
        var backendRoot = ResolveBackendRoot();
        var violations   = new List<string>();

        foreach (var file in Directory.EnumerateFiles(backendRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                continue;
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                continue;
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                continue;
            if (file.Contains(".Tests", StringComparison.OrdinalIgnoreCase))
                continue;

            var relative = Path.GetRelativePath(backendRoot, file).Replace('\\', '/');
            if (allowedPaths.Contains(relative))
                continue;

            var text = File.ReadAllText(file);
            if (pattern.IsMatch(text))
                violations.Add(relative);
        }

        return violations;
    }

    private static string ResolveBackendRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ERP.sln")) ||
                Directory.Exists(Path.Combine(dir.FullName, "src", "ERP.API")))
                return dir.FullName;

            dir = dir.Parent;
        }

        throw new InvalidOperationException("No se encontró la raíz backend (ERP.sln / src/ERP.API).");
    }
}
