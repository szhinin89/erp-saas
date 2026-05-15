using ERP.Domain.Modules.Accounting.Entities;

namespace ERP.Domain.Modules.Accounting.Interfaces;

public interface IConfiguracionContableRepository
{
    Task<ConfiguracionContableEmpresa?> GetConfiguracionEmpresaAsync(CancellationToken ct = default);
    Task AddConfiguracionEmpresaAsync(ConfiguracionContableEmpresa entity, CancellationToken ct = default);
    Task<IReadOnlyList<ConfiguracionGastoCategoria>> GetGastoCategoriasAsync(CancellationToken ct = default);
    Task<ConfiguracionGastoCategoria?> GetGastoCategoriaByCategoriaAsync(string categoria, CancellationToken ct = default);
    Task<ConfiguracionGastoCategoria?> GetGastoCategoriaByIdAsync(Guid id, CancellationToken ct = default);
    Task AddGastoCategoriaAsync(ConfiguracionGastoCategoria entity, CancellationToken ct = default);
    void RemoveGastoCategoria(ConfiguracionGastoCategoria entity);
    Task SaveChangesAsync(CancellationToken ct = default);
}
