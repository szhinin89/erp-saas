namespace ERP.Application.Modules.InitialLoad.Interfaces;

/// <summary>
/// IL-4C — alcance adicional al Tenant+Company del lote, derivado de su staging (sin columna
/// propia): un lote de Inventario Inicial pertenece a la sucursal de las bodegas que resolvió al
/// validar. Validar, confirmar o cancelar desde otra sucursal se rechaza. Un lote sin staging
/// resuelto todavía no está ligado a ninguna sucursal.
/// </summary>
public interface IImportBatchScopeGuard
{
    /// <summary>Motivo de rechazo si el staging pertenece a otro alcance; <c>null</c> si es válido.</summary>
    Task<string?> CheckStagingScopeAsync(IReadOnlyList<string> parsedDataJson, CancellationToken ct);
}
