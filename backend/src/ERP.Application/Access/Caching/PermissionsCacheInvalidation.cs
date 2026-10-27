using ERP.Application.Common;

namespace ERP.Application.Access.Caching;

/// <summary>
/// SEC-06 — único punto de uso del protocolo de <see cref="IPermissionsCacheInvalidator"/> en los
/// handlers que cambian permisos efectivos: Begin → mutación (persistencia) → Complete.
/// <list type="bullet">
/// <item>Begin falla: la mutación no se ejecuta (nada persistido) →
/// <see cref="ApiResponseCodes.Access.PermissionsInvalidationUnavailable"/>.</item>
/// <item>Complete se ejecuta siempre tras la mutación (también si falla o lanza): incrementar la
/// versión sin cambio real es inocuo. Con <see cref="CancellationToken.None"/>: cancelar la request
/// no debe dejar la invalidación a medias.</item>
/// <item>Mutación exitosa y Complete falla: el alcance sigue protegido (la marca de Begin persiste) y
/// se informa <see cref="ApiResponseCodes.Access.PermissionsInvalidationIncomplete"/>.</item>
/// </list>
/// </summary>
public static class PermissionsCacheInvalidation
{
    public static async Task<Result<T>> RunAsync<T>(
        this IPermissionsCacheInvalidator invalidator,
        PermissionsCacheScope scope,
        Func<CancellationToken, Task<Result<T>>> mutation,
        CancellationToken cancellationToken
    )
    {
        if (!await invalidator.TryBeginAsync(scope, cancellationToken))
            return Result<T>.Failure(
                "No se pudo garantizar la actualización de permisos. No se aplicó ningún cambio.",
                ApiResponseCodes.Access.PermissionsInvalidationUnavailable
            );

        Result<T> result;
        try
        {
            result = await mutation(cancellationToken);
        }
        catch
        {
            await invalidator.TryCompleteAsync(scope, CancellationToken.None);
            throw;
        }

        var completed = await invalidator.TryCompleteAsync(scope, CancellationToken.None);
        if (result.IsSuccess && !completed)
            return Result<T>.Failure(
                "El cambio se guardó, pero la propagación de permisos no pudo confirmarse.",
                ApiResponseCodes.Access.PermissionsInvalidationIncomplete
            );

        return result;
    }
}
