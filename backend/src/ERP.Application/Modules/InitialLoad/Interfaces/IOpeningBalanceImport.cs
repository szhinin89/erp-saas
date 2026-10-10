namespace ERP.Application.Modules.InitialLoad.Interfaces;

/// <summary>
/// IL-8E — procesador de un lote de SALDOS iniciales (Inventario, CxC, CxP): sujeto al cierre
/// definitivo de la Carga Inicial (<see cref="InitialLoadClosedGuard"/>). Los motores genéricos
/// (crear/subir/validar/confirmar) lo consultan antes de escribir; los catálogos no lo implementan.
/// </summary>
public interface IOpeningBalanceImport
{
    /// <summary>Motivo de rechazo si la Carga Inicial está cerrada; <c>null</c> si sigue abierta.</summary>
    Task<string?> CheckInitialLoadOpenAsync(CancellationToken ct);
}
