using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ERP.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Corrige una clasificación errónea de EF Core: una entidad hija NUEVA, con clave
/// generada por el dominio (Guid.NewGuid() en un factory Create()), agregada a una
/// colección de navegación de un agregado YA TRACKEADO (no Added) — p. ej. dentro de
/// un domain event handler entre dos SaveChanges — es descubierta recién por
/// DetectChanges(). Como su clave no es "temporal" para EF, el change tracker no
/// puede inferir que es nueva y la adjunta como Modified, con TODAS las propiedades
/// marcadas IsModified=true pero OriginalValue == CurrentValue (no existe snapshot
/// real de BD). Ese UPDATE no-op afecta 0 filas → DbUpdateConcurrencyException.
///
/// Regla de decisión (dos condiciones, ambas necesarias):
///   1. State == Modified pero ninguna propiedad tiene una diferencia real
///      (Original == Current para todas las marcadas IsModified).
///   2. La entidad nunca fue materializada por una query en ESTE DbContext
///      (ErpDbContext.WasTrackedFromQuery == false).
///
/// Si se cumplen ambas → es la firma inequívoca de una entidad nunca persistida:
/// se corrige a Added.
///
/// Si se cumple (1) pero NO (2) — una entidad que SÍ vino de una query queda
/// Modified sin ningún cambio real — es una combinación anómala que no debería
/// ocurrir bajo operación normal de EF Core (normalmente quedaría Unchanged).
/// En ese caso NO se adivina: se falla con una excepción clara, porque mutar el
/// estado de una entidad con snapshot real de BD sin evidencia inequívoca de que
/// es nueva podría enmascarar un bug distinto o, en el peor caso, generar un
/// INSERT duplicado sobre una fila que sí existe.
///
/// Esta corrección depende de un invariante arquitectónico: ningún módulo debe
/// reatachar una entidad detached con ID real (Attach/Update) sin que pase antes
/// por una query trackeada en el mismo DbContext. Ese invariante está garantizado
/// por NewChildEntityTrackingArchitectureTests (gate CI-bloqueante), no solo por
/// convención documental.
/// </summary>
public sealed class NewChildEntityTrackingInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        Reconcile(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Reconcile(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void Reconcile(DbContext? context)
    {
        if (context is not ErpDbContext db) return;

        db.ChangeTracker.DetectChanges();

        foreach (var entry in db.ChangeTracker.Entries())
        {
            if (entry.State != EntityState.Modified) continue;
            if (HasRealChange(entry)) continue;

            if (!db.WasTrackedFromQuery(entry.Entity))
            {
                entry.State = EntityState.Added;
                continue;
            }

            throw new InvalidOperationException(
                $"[NewChildEntityTracking] '{entry.Metadata.DisplayName()}' fue cargada por una query en " +
                "este DbContext pero quedó marcada Modified sin ninguna diferencia real de valores. Esta " +
                "combinación es anómala bajo operación normal de EF Core y no puede resolverse " +
                "automáticamente sin riesgo de enmascarar otro defecto. Revisar manualmente el flujo que " +
                "produjo este estado antes de continuar.");
        }
    }

    private static bool HasRealChange(EntityEntry entry)
        => entry.Properties.Any(p => p.IsModified && !Equals(p.OriginalValue, p.CurrentValue));
}
