using ERP.Domain.Common;

namespace ERP.Domain.Modules.Inventario.Entities;

/// <summary>
/// Registro de un trabajo de Kardex solicitado de forma asíncrona.
/// Estados: Pendiente → Procesando → Completado | Error.
/// </summary>
/// <remarks>
/// Implementa solo <see cref="ITenantEntity"/> (no <see cref="AuditableEntity"/>):
/// la trazabilidad es el propio ciclo de estados y <see cref="SolicitadoEn"/> / <see cref="CompletadoEn"/>,
/// actualizado por API + background processor, no por un patrón maestro “quién editó”.
/// </remarks>
public sealed class KardexReporte : ITenantEntity
{
    public const string EstadoPendiente   = "Pendiente";
    public const string EstadoProcesando  = "Procesando";
    public const string EstadoCompletado  = "Completado";
    public const string EstadoError       = "Error";

    public Guid     Id            { get; private set; }
    public Guid     TenantId      { get; private set; }
    public Guid     ProductoId    { get; private set; }
    public Guid     BodegaId      { get; private set; }
    public DateTime? FechaInicio  { get; private set; }
    public DateTime? FechaFin     { get; private set; }
    public string   Estado        { get; private set; } = EstadoPendiente;
    /// <summary>JSON serializado de <c>KardexResponse</c> cuando el estado es Completado.</summary>
    public string?  ResultadoJson { get; private set; }
    public string?  ErrorMensaje  { get; private set; }
    public DateTime SolicitadoEn  { get; private set; }
    public DateTime? CompletadoEn { get; private set; }

    private KardexReporte() { }

    public static KardexReporte Create(
        Guid tenantId, Guid productoId, Guid bodegaId,
        DateTime? fechaInicio, DateTime? fechaFin)
        => new()
        {
            Id           = Guid.NewGuid(),
            TenantId     = tenantId,
            ProductoId   = productoId,
            BodegaId     = bodegaId,
            FechaInicio  = fechaInicio,
            FechaFin     = fechaFin,
            Estado       = EstadoPendiente,
            SolicitadoEn = DateTime.UtcNow,
        };

    public void MarcarProcesando()
    {
        Estado = EstadoProcesando;
    }

    public void MarcarCompletado(string resultadoJson)
    {
        Estado        = EstadoCompletado;
        ResultadoJson = resultadoJson;
        CompletadoEn  = DateTime.UtcNow;
    }

    public void MarcarError(string mensaje)
    {
        Estado       = EstadoError;
        ErrorMensaje = mensaje;
        CompletadoEn = DateTime.UtcNow;
    }
}
