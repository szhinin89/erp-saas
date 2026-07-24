using ERP.Application.Common;

namespace ERP.Application.Modules.Branches;

public sealed record InterBranchAccessContext(
    Guid UserId,
    Guid TenantId,
    Guid CompanyId,
    Guid OperationBranchId,
    Guid SourceWarehouseId,
    Guid SourceBranchId,
    Guid TargetWarehouseId,
    Guid DestinationBranchId);

/// <summary>
/// Autorización para operaciones que involucran dos sucursales potencialmente distintas
/// (origen y destino), como una transferencia de inventario entre bodegas.
/// A diferencia de <c>IBranchAccessGuard</c> (una sola sucursal, la activa del caller), este
/// guard resuelve y valida <em>dos</em> sucursales derivadas de las bodegas de origen/destino,
/// componiendo <c>ICompanyAccessGuard</c>, <c>IWarehouseRepository</c> y <c>IBranchAccessGuard</c>
/// existentes — no reimplementa ninguna de sus reglas.
/// </summary>
public interface IInterBranchAccessGuard
{
    /// <summary>
    /// Valida, en orden: empresa operativa activa → sucursal activa del caller (contexto de
    /// operación) → bodega origen existe/activa/pertenece a la empresa → bodega destino
    /// existe/activa/pertenece a la empresa → usuario con acceso a la sucursal de origen →
    /// usuario con acceso a la sucursal de destino. Ambas autorizaciones de sucursal son
    /// bloqueantes.
    /// </summary>
    Task<Result<InterBranchAccessContext>> RequireInterBranchAccessAsync(
        Guid sourceWarehouseId,
        Guid targetWarehouseId,
        CancellationToken cancellationToken = default);
}
