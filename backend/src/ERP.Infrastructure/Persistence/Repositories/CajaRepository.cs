using Microsoft.EntityFrameworkCore;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class CajaRepository : ICajaRepository
{
    private readonly ErpDbContext _db;

    public CajaRepository(ErpDbContext db) => _db = db;

    public Task<CuentaBancaria?> GetCuentaBancariaByIdAsync(Guid id, CancellationToken ct = default)
        => _db.CuentasBancarias.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<CuentaBancaria>> ListCuentasBancariasAsync(CancellationToken ct = default)
        => await _db.CuentasBancarias.OrderBy(x => x.Nombre).ToListAsync(ct);

    public Task AddCuentaBancariaAsync(CuentaBancaria entity, CancellationToken ct = default)
        => _db.CuentasBancarias.AddAsync(entity, ct).AsTask();

    public Task<ExtractoBancario?> GetExtractoByIdAsync(Guid id, CancellationToken ct = default)
        => _db.ExtractosBancarios.FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<ExtractoBancario?> GetExtractoWithMovimientosAsync(Guid id, CancellationToken ct = default)
        => _db.ExtractosBancarios
            .Include(x => x.Movimientos)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<ExtractoBancario?> GetExtractoWithMovimientosForMovimientoAsync(
        Guid movimientoId,
        CancellationToken ct = default)
    {
        var mov = await _db.MovimientosBancarios.AsNoTracking().FirstOrDefaultAsync(m => m.Id == movimientoId, ct);
        if (mov is null)
            return null;
        return await GetExtractoWithMovimientosAsync(mov.ExtractoBancarioId, ct);
    }

    public async Task<IReadOnlyList<ExtractoBancario>> ListExtractosByCuentaAsync(
        Guid cuentaBancariaId,
        CancellationToken ct = default)
        => await _db.ExtractosBancarios
            .Include(x => x.Movimientos)
            .Where(x => x.CuentaBancariaId == cuentaBancariaId)
            .OrderByDescending(x => x.FechaCarga)
            .ToListAsync(ct);

    public Task AddExtractoAsync(ExtractoBancario entity, CancellationToken ct = default)
        => _db.ExtractosBancarios.AddAsync(entity, ct).AsTask();

    public Task<MovimientoBancario?> GetMovimientoByIdAsync(Guid id, CancellationToken ct = default)
        => _db.MovimientosBancarios.FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<CajaChica?> GetCajaChicaByIdAsync(Guid id, CancellationToken ct = default)
        => _db.CajasChicas.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<CajaChica>> ListCajasChicasAsync(CancellationToken ct = default)
        => await _db.CajasChicas.OrderBy(x => x.Nombre).ToListAsync(ct);

    public Task AddCajaChicaAsync(CajaChica entity, CancellationToken ct = default)
        => _db.CajasChicas.AddAsync(entity, ct).AsTask();

    public Task AddArqueoAsync(ArqueoCaja entity, CancellationToken ct = default)
        => _db.ArqueosCaja.AddAsync(entity, ct).AsTask();

    public Task<ArqueoCaja?> GetArqueoByIdAsync(Guid id, CancellationToken ct = default)
        => _db.ArqueosCaja.FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task AddGastoCajaAsync(GastoCajaChica entity, CancellationToken ct = default)
        => _db.GastosCajaChica.AddAsync(entity, ct).AsTask();

    public async Task<IReadOnlyList<GastoCajaChica>> ListGastosCajaAsync(Guid cajaChicaId, CancellationToken ct = default)
        => await _db.GastosCajaChica
            .Where(x => x.CajaChicaId == cajaChicaId)
            .OrderByDescending(x => x.Fecha)
            .ToListAsync(ct);
}
