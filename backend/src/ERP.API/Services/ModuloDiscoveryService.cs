using System.Reflection;
using ERP.API.Attributes;
using ERP.Domain.Modules.Menu.Entities;
using ERP.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;

namespace ERP.API.Services;

/// <summary>
/// Descubre <see cref="ModuloAttribute"/> en controladores/acciones y sincroniza la tabla <c>funcionalidades</c>.
/// Omite filas cuyo <see cref="ModuloAttribute.PermisoBase"/> comienza por <c>SuperAdmin</c> (insensible a mayúsculas).
/// </summary>
public sealed class ModuloDiscoveryService
{
    private readonly ErpDbContext _db;
    private readonly ILogger<ModuloDiscoveryService> _logger;

    public ModuloDiscoveryService(ErpDbContext db, ILogger<ModuloDiscoveryService> logger)
    {
        _db = db;
        _logger = logger;
    }

    private sealed record DiscoveryRow(
        string Permiso,
        string Nombre,
        string? Icono,
        string? Ruta,
        string? PadrePermiso,
        int Orden,
        bool VisibleEnMenu,
        bool EsSuperAdmin);

    public async Task<int> SincronizarModulosAsync(CancellationToken ct = default)
    {
        var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var rows = new List<DiscoveryRow>();
        var controllerTypes = asm.GetTypes()
            .Where(t => t.IsPublic && !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t));

        foreach (var type in controllerTypes)
        {
            var classAttr = type.GetCustomAttribute<ModuloAttribute>(inherit: true);
            if (classAttr is not null && !ShouldExclude(classAttr.PermisoBase))
                rows.Add(ToRow(classAttr, NormalizePadre(classAttr.Padre)));

            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                if (!IsHttpAction(method))
                    continue;

                var methodAttr = method.GetCustomAttribute<ModuloAttribute>(inherit: false);
                if (methodAttr is null)
                    continue;

                if (ShouldExclude(methodAttr.PermisoBase))
                    continue;

                string? parentPermiso;
                if (!string.IsNullOrWhiteSpace(methodAttr.Padre))
                    parentPermiso = methodAttr.Padre.Trim();
                else if (classAttr is not null && !string.IsNullOrWhiteSpace(classAttr.PermisoBase))
                    parentPermiso = classAttr.PermisoBase.Trim();
                else
                    parentPermiso = null;

                rows.Add(ToRow(methodAttr, parentPermiso));
            }
        }

        var byPerm = new Dictionary<string, DiscoveryRow>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in rows.OrderBy(r => r.PadrePermiso is null ? 0 : 1).ThenBy(r => r.Orden).ThenBy(r => r.Permiso, StringComparer.Ordinal))
            byPerm[r.Permiso] = r;

        var utc = DateTime.UtcNow;
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var tracked = await _db.Funcionalidades.AsTracking()
            .ToDictionaryAsync(x => x.Permiso, x => x, StringComparer.OrdinalIgnoreCase, ct);

        var permToId = tracked.ToDictionary(x => x.Key, x => x.Value.Id, StringComparer.OrdinalIgnoreCase);

        var pending = byPerm.Values.ToList();
        var guard = 0;
        while (pending.Count > 0 && guard++ < 64)
        {
            var batch = pending
                .Where(r => string.IsNullOrEmpty(r.PadrePermiso) || permToId.ContainsKey(r.PadrePermiso))
                .ToList();
            if (batch.Count == 0)
            {
                foreach (var orphan in pending)
                    _logger.LogWarning("Funcionalidad con padre no resuelto (omitida): {Permiso} → padre {Padre}", orphan.Permiso, orphan.PadrePermiso);
                break;
            }

            foreach (var r in batch)
            {
                pending.Remove(r);
                Guid? padreId = null;
                if (!string.IsNullOrEmpty(r.PadrePermiso) && permToId.TryGetValue(r.PadrePermiso, out var pid))
                    padreId = pid;

                if (tracked.TryGetValue(r.Permiso, out var entity))
                {
                    entity.SyncFromDiscovery(r.Nombre, r.Icono, r.Ruta, padreId, r.Orden, r.VisibleEnMenu, r.EsSuperAdmin, utc);
                }
                else
                {
                    var created = Funcionalidad.Create(
                        r.Nombre,
                        r.Icono,
                        r.Ruta,
                        r.Permiso,
                        padreId,
                        r.Orden,
                        r.VisibleEnMenu,
                        r.EsSuperAdmin,
                        utc);
                    _db.Funcionalidades.Add(created);
                    tracked[r.Permiso] = created;
                    permToId[r.Permiso] = created.Id;
                }
            }

            await _db.SaveChangesAsync(ct);
        }

        await tx.CommitAsync(ct);
        _logger.LogInformation("Sincronización de funcionalidades completada ({Count} permisos únicos).", byPerm.Count);
        return byPerm.Count;
    }

    private static string? NormalizePadre(string? padre) =>
        string.IsNullOrWhiteSpace(padre) ? null : padre.Trim();

    private static bool ShouldExclude(string permisoBase)
    {
        var p = (permisoBase ?? string.Empty).Trim();
        return p.StartsWith("SuperAdmin", StringComparison.OrdinalIgnoreCase);
    }

    private static DiscoveryRow ToRow(ModuloAttribute a, string? padrePermisoExplicit)
    {
        var perm = (a.PermisoBase ?? string.Empty).Trim();
        var padre = NormalizePadre(padrePermisoExplicit);
        return new DiscoveryRow(
            perm,
            a.Nombre,
            a.Icono,
            a.Ruta,
            padre,
            a.Orden,
            a.VisibleEnMenu,
            a.EsSuperAdmin);
    }

    private static bool IsHttpAction(MethodInfo m)
    {
        if (m.IsSpecialName || m.ContainsGenericParameters)
            return false;
        return m.GetCustomAttributes(inherit: true).Any(static a => a is HttpMethodAttribute);
    }
}
