using ERP.Application.Common;
using ERP.Domain.Modules.Employees.Enums;
using ERP.Domain.Modules.Employees.Interfaces;

namespace ERP.Application.Modules.Employees.UseCases;

/// <summary>Piezas comunes de los casos de uso de Empleados (sin lógica de negocio propia).</summary>
internal static class EmployeeUseCaseSupport
{
    /// <summary>
    /// Ejecuta <paramref name="body"/> en una transacción: usa la ambiente si existe y solo
    /// confirma o revierte la propia (patrón de EMP-E3). Un fallo o una excepción revierten la propia.
    /// </summary>
    public static async Task<Result<T>> RunInTransactionAsync<T>(
        IUnitOfWork unitOfWork,
        Func<Task<Result<T>>> body,
        CancellationToken ct
    )
    {
        var ownsTransaction = !unitOfWork.HasActiveTransaction;
        if (ownsTransaction)
            await unitOfWork.BeginTransactionAsync(ct);

        try
        {
            var result = await body();
            if (ownsTransaction)
            {
                if (result.IsSuccess)
                    await unitOfWork.CommitAsync(ct);
                else
                    await unitOfWork.RollbackAsync(ct);
            }
            return result;
        }
        catch
        {
            if (ownsTransaction)
                await unitOfWork.RollbackAsync(ct);
            throw;
        }
    }

    public static Result<T> EmployeeNotFound<T>() =>
        Result<T>.Failure("El empleado no existe.", ApiResponseCodes.Employees.NotFound);

    public static Result<T> ConcurrencyConflict<T>() =>
        Result<T>.Conflict(
            "La ficha fue modificada por otro usuario. Recargue e intente de nuevo.",
            ApiResponseCodes.Common.ConcurrencyConflict
        );

    /// <summary>
    /// Valida que cada referencia exista en el tenant y la empresa (indistinguible de «fuera de
    /// ámbito», D-8 → 422) y esté activa (EMPLOYEE_CATALOG_INACTIVE → 400). Debe ejecutarse dentro
    /// de la transacción: bloquea los activos FOR SHARE. Devuelve null si todo es válido.
    /// </summary>
    public static async Task<Result<T>?> ValidateAssignableAsync<T>(
        IEmployeeCatalogRepository catalogs,
        EmployeeCatalogType type,
        string label,
        Guid tenantId,
        Guid companyId,
        IReadOnlyCollection<Guid> ids,
        CancellationToken ct
    )
    {
        if (ids.Count == 0)
            return null;

        var resolved = await catalogs.ResolveForAssignmentAsync(type, tenantId, companyId, ids, ct);
        foreach (var id in ids)
        {
            if (!resolved.TryGetValue(id, out var active))
                return Result<T>.ValidationFailure($"{label} no válido.");
            if (!active)
                return Result<T>.Failure(
                    $"{label} está inactivo y no puede asignarse.",
                    ApiResponseCodes.Employees.CatalogInactive
                );
        }
        return null;
    }
}
