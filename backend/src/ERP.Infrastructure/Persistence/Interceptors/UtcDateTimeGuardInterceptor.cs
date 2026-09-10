using ERP.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ERP.Infrastructure.Persistence.Interceptors;

/// <summary>
/// ZH-DATETIME-UTC-GUARDRAILS-01: guardrail global — ningún <see cref="DateTime"/> persistible
/// puede llegar a PostgreSQL (timestamptz) con Kind distinto de <see cref="DateTimeKind.Utc"/>.
///
/// Se ejecuta en SavingChanges/SavingChangesAsync, antes de que EF traduzca el comando SQL, sobre
/// toda entidad Added/Modified. Revisa únicamente propiedades CLR <c>DateTime</c>/<c>DateTime?</c>
/// (DateOnly/DateTimeOffset quedan fuera de alcance — ver ZH-DATETIME-UTC-GUARDRAILS-01). Si detecta
/// Unspecified o Local, falla con <see cref="UnspecifiedDateTimeKindException"/> identificando
/// "Entidad.Propiedad" — la normalización correcta vive en la entidad/factory/mutador de origen
/// (<see cref="ERP.Domain.Common.UtcDateTime"/>), este guard es la última línea de defensa, no el
/// punto donde se corrige el valor.
/// </summary>
public sealed class UtcDateTimeGuardInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result
    )
    {
        Guard(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default
    )
    {
        Guard(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void Guard(DbContext? context)
    {
        if (context is null)
            return;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
                continue;

            foreach (var property in entry.Properties)
            {
                if (property.Metadata.ClrType != typeof(DateTime) && property.Metadata.ClrType != typeof(DateTime?))
                    continue;

                if (property.CurrentValue is not DateTime value)
                    continue;

                if (value.Kind == DateTimeKind.Utc)
                    continue;

                throw new UnspecifiedDateTimeKindException(
                    entry.Metadata.ClrType.Name,
                    property.Metadata.Name,
                    value.Kind
                );
            }
        }
    }
}
