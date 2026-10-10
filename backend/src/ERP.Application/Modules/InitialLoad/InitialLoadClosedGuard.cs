using ERP.Application.Modules.InitialLoad.Interfaces;
using CompanyEntity = ERP.Domain.Modules.Company.Entities.Company;

namespace ERP.Application.Modules.InitialLoad;

/// <summary>
/// IL-8E — guard ÚNICO del cierre definitivo de la Carga Inicial. Tras <c>Company.CloseInitialLoad</c>
/// nada modifica la apertura: lotes de saldos (crear/subir/validar/confirmar), contabilización por lote
/// (IL-7B), publicación (IL-8A) y reverso (IL-8B) del ASI, y la fecha de apertura (esta última vía
/// <c>Company.OpeningBalanceDateLockReason</c>, mismo mensaje). Las importaciones de catálogos
/// (Productos/Clientes/Proveedores) no son apertura de saldos y no se bloquean: los procesadores de
/// saldos lo exponen con <see cref="Interfaces.IOpeningBalanceImport"/>. Los llamadores que
/// escriben leen el estado DESPUÉS del bloqueo de la empresa o del lote (el cierre bloquea ambos).
/// </summary>
public static class InitialLoadClosedGuard
{
    public const string Code = "INITIAL_LOAD_CLOSED";

    /// <summary>Motivo de rechazo si la Carga Inicial está cerrada; <c>null</c> si sigue abierta.</summary>
    public static async Task<string?> CheckOpenAsync(IOpeningBalanceConstraintsReader reader, CancellationToken ct) =>
        await reader.GetInitialLoadClosureAsync(ct) is null ? null : CompanyEntity.InitialLoadClosedMessage;
}
