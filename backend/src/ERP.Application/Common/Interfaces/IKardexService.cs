using ERP.Application.Common;
using ERP.Application.Inventario.DTOs;
using ERP.Application.Inventario.UseCases.GetKardex;

namespace ERP.Application.Common.Interfaces;

/// <summary>
/// Generación del kardex valorizado (promedio ponderado móvil), incluyendo el modo escalable
/// (snapshots, rangos largos, opcionalmente agregados diarios desde <c>mv_saldos_diarios</c>).
/// </summary>
public interface IKardexService
{
    /// <summary>
    /// Usa el <see cref="ICurrentTenant"/> del scope HTTP (o el registrado en DI).
    /// </summary>
    Task<Result<KardexResponse>> GenerarKardexEscalableAsync(
        GetKardexQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fija el tenant explícitamente (workers en segundo plano sin <c>HttpContext</c>).
    /// </summary>
    Task<Result<KardexResponse>> GenerarKardexEscalableAsync(
        Guid tenantId,
        GetKardexQuery query,
        CancellationToken cancellationToken = default);
}
