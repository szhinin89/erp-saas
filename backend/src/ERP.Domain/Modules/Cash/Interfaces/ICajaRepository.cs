using ERP.Domain.Modules.Cash.Entities;

namespace ERP.Domain.Modules.Cash.Interfaces;

public interface ICajaRepository
{
    Task<CuentaBancaria?> GetCuentaBancariaByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<CuentaBancaria>> ListCuentasBancariasAsync(CancellationToken ct = default);
    Task AddCuentaBancariaAsync(CuentaBancaria entity, CancellationToken ct = default);

    Task<ExtractoBancario?> GetExtractoByIdAsync(Guid id, CancellationToken ct = default);
    Task<ExtractoBancario?> GetExtractoWithMovimientosAsync(Guid id, CancellationToken ct = default);

    /// <summary>Carga el extracto con movimientos rastreados, localizando por id de movimiento.</summary>
    Task<ExtractoBancario?> GetExtractoWithMovimientosForMovimientoAsync(Guid movimientoId, CancellationToken ct = default);
    Task<IReadOnlyList<ExtractoBancario>> ListExtractosByCuentaAsync(Guid cuentaBancariaId, CancellationToken ct = default);
    Task AddExtractoAsync(ExtractoBancario entity, CancellationToken ct = default);

    Task<MovimientoBancario?> GetMovimientoByIdAsync(Guid id, CancellationToken ct = default);

    Task<CajaChica?> GetCajaChicaByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<CajaChica>> ListCajasChicasAsync(CancellationToken ct = default);
    Task AddCajaChicaAsync(CajaChica entity, CancellationToken ct = default);

    Task AddArqueoAsync(ArqueoCaja entity, CancellationToken ct = default);
    Task<ArqueoCaja?> GetArqueoByIdAsync(Guid id, CancellationToken ct = default);
    Task AddGastoCajaAsync(GastoCajaChica entity, CancellationToken ct = default);
    Task<IReadOnlyList<GastoCajaChica>> ListGastosCajaAsync(Guid cajaChicaId, CancellationToken ct = default);
}
