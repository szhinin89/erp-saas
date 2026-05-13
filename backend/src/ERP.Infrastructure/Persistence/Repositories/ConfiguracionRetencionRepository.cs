using Microsoft.EntityFrameworkCore;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class ConfiguracionRetencionRepository : IConfiguracionRetencionRepository
{
    private readonly ErpDbContext _context;

    public ConfiguracionRetencionRepository(ErpDbContext context) => _context = context;

    public async Task<IReadOnlyList<ConfiguracionRetencion>> GetActivosParaProveedorAsync(
        Guid tenantId,
        CancellationToken ct = default)
        => await _context.ConfiguracionRetenciones
            .Where(r => r.TenantId == tenantId && r.Activo &&
                        (r.TipoSujeto == "PROVEEDOR" || r.TipoSujeto == "AMBOS"))
            .OrderBy(r => r.Impuesto)
            .ThenBy(r => r.CodigoSri)
            .ToListAsync(ct);

    public Task AddAsync(ConfiguracionRetencion entity, CancellationToken ct = default)
        => _context.ConfiguracionRetenciones.AddAsync(entity, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}
