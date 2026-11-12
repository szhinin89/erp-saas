using System.Collections.Concurrent;
using System.Reflection;
using System.Text;

namespace ERP.API.Swagger;

/// <summary>
/// Ids de esquema OpenAPI portables (API-DOC-V1 / Fase 2A). Sustituye a <c>FullName</c>, que
/// producía ids con <c>`</c>, corchetes y <c>PublicKeyToken</c> inválidos para generadores de
/// clientes. Regla: nombre simple; genéricos como <c>ApiResponseOfCompanyDto</c>; solo si dos tipos
/// de los ensamblados ERP comparten nombre, esos tipos (y solo ellos) se califican con su namespace.
/// El resultado depende únicamente de los tipos, no del orden de visita.
/// </summary>
public sealed class OpenApiSchemaIds
{
    private readonly HashSet<string> _ambiguousNames;
    private readonly ConcurrentDictionary<Type, string> _cache = new();
    private readonly object _gate = new();
    private readonly Dictionary<string, Type> _claimed = new(StringComparer.Ordinal);

    public OpenApiSchemaIds(IEnumerable<Assembly> assemblies)
    {
        var byName = new Dictionary<string, HashSet<Type>>(StringComparer.Ordinal);
        foreach (var assembly in assemblies.Distinct())
        {
            foreach (var type in SafeExportedTypes(assembly))
            {
                if (type.IsGenericTypeDefinition || type.IsGenericParameter)
                    continue;
                var name = Plain(type);
                if (!byName.TryGetValue(name, out var set))
                    byName[name] = set = [];
                set.Add(type);
            }
        }

        _ambiguousNames = byName.Where(p => p.Value.Count > 1).Select(p => p.Key).ToHashSet();
    }

    /// <summary>Ids simples que colisionan entre tipos ERP y por tanto se califican.</summary>
    public IReadOnlyCollection<string> AmbiguousNames => _ambiguousNames;

    public string For(Type type) => _cache.GetOrAdd(type, Resolve);

    private string Resolve(Type type)
    {
        var id = Build(type, qualify: false);
        lock (_gate)
        {
            if (_claimed.TryGetValue(id, out var owner) && owner != type)
            {
                // Colisión con un tipo fuera de los ensamblados ERP: calificar con namespace.
                id = Build(type, qualify: true);
            }

            _claimed.TryAdd(id, type);
            return id;
        }
    }

    private string Build(Type type, bool qualify)
    {
        if (type.IsArray)
            return Build(type.GetElementType()!, qualify) + "Array";

        if (type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition();
            var name = Plain(definition);
            var tick = name.IndexOf('`');
            if (tick >= 0)
                name = name[..tick];
            var args = type.GetGenericArguments().Select(a => Build(a, qualify));
            return Sanitize(Qualified(definition, name, qualify) + "Of" + string.Join("And", args));
        }

        var plain = Plain(type);
        return Sanitize(Qualified(type, plain, qualify || _ambiguousNames.Contains(plain)));
    }

    private static string Qualified(Type type, string name, bool qualify)
    {
        if (!qualify || string.IsNullOrEmpty(type.Namespace))
            return name;
        return type.Namespace + "." + name;
    }

    /// <summary>Nombre sin namespace; los tipos anidados se componen como <c>Outer.Inner</c>.</summary>
    private static string Plain(Type type)
    {
        var sb = new StringBuilder(type.Name);
        for (var parent = type.DeclaringType; parent is not null; parent = parent.DeclaringType)
        {
            var parentName = parent.Name;
            var tick = parentName.IndexOf('`');
            if (tick >= 0)
                parentName = parentName[..tick];
            sb.Insert(0, parentName + ".");
        }

        return sb.ToString();
    }

    /// <summary>Solo <c>[A-Za-z0-9._-]</c> (patrón de nombres de componentes de OpenAPI 3).</summary>
    public static string Sanitize(string id)
    {
        var sb = new StringBuilder(id.Length);
        foreach (var c in id)
            sb.Append(char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-' ? c : '_');
        return sb.ToString();
    }

    private static IEnumerable<Type> SafeExportedTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetExportedTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null)!;
        }
        catch (NotSupportedException)
        {
            return [];
        }
    }
}
