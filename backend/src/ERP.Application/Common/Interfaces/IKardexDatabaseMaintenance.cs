namespace ERP.Application.Common.Interfaces;

/// <summary>
/// Mantenimiento de objetos de base de datos relacionados con kardex (p. ej. vista materializada de saldos diarios).
/// </summary>
public interface IKardexDatabaseMaintenance
{
    /// <summary>
    /// Refresca <c>mv_saldos_diarios</c> si existe. Pensado para job recurrente (Hangfire) fuera de horario pico.
    /// </summary>
    Task RefreshDailyBalancesMaterializedViewAsync(CancellationToken cancellationToken = default);
}
