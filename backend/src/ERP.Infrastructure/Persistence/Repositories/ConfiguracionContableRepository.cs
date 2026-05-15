using Microsoft.EntityFrameworkCore;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class ConfiguracionContableRepository : IConfiguracionContableRepository
{
    private readonly ErpDbContext _context;

    public ConfiguracionContableRepository(ErpDbContext context)
    {
        _context = context;
    }

    public async Task<ConfiguracionContableEmpresa?> GetConfiguracionEmpresaAsync(CancellationToken ct = default)
        => await _context.ConfiguracionContableEmpresas.FirstOrDefaultAsync(ct);

    public async Task AddConfiguracionEmpresaAsync(ConfiguracionContableEmpresa entity, CancellationToken ct = default)
        => await _context.ConfiguracionContableEmpresas.AddAsync(entity, ct);

    public async Task<IReadOnlyList<ConfiguracionGastoCategoria>> GetGastoCategoriasAsync(CancellationToken ct = default)
        => await _context.ConfiguracionGastoCategorias
            .OrderBy(g => g.Categoria)
            .ToListAsync(ct);

    public async Task<ConfiguracionGastoCategoria?> GetGastoCategoriaByCategoriaAsync(string categoria, CancellationToken ct = default)
    {
        var c = categoria.Trim();
        return await _context.ConfiguracionGastoCategorias
            .FirstOrDefaultAsync(
                g => g.Categoria.ToLower() == c.ToLower(),
                ct);
    }

    public async Task<ConfiguracionGastoCategoria?> GetGastoCategoriaByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.ConfiguracionGastoCategorias.FirstOrDefaultAsync(g => g.Id == id, ct);

    public async Task AddGastoCategoriaAsync(ConfiguracionGastoCategoria entity, CancellationToken ct = default)
        => await _context.ConfiguracionGastoCategorias.AddAsync(entity, ct);

    public void RemoveGastoCategoria(ConfiguracionGastoCategoria entity)
        => _context.ConfiguracionGastoCategorias.Remove(entity);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}
