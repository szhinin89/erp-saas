using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Contabilidad.DTOs;
using ERP.Domain.Modules.Contabilidad.Interfaces;

namespace ERP.Application.Modules.Contabilidad.UseCases.ConfiguracionContable;

public sealed class GetConfiguracionGastoCategoriasQueryHandler
    : IRequestHandler<GetConfiguracionGastoCategoriasQuery, Result<IReadOnlyList<ConfiguracionGastoCategoriaDto>>>
{
    private readonly IConfiguracionContableRepository _repo;

    public GetConfiguracionGastoCategoriasQueryHandler(IConfiguracionContableRepository repo)
    {
        _repo = repo;
    }

    public async Task<Result<IReadOnlyList<ConfiguracionGastoCategoriaDto>>> Handle(
        GetConfiguracionGastoCategoriasQuery request,
        CancellationToken ct)
    {
        var items = await _repo.GetGastoCategoriasAsync(ct);
        var dtos = items.Select(g => new ConfiguracionGastoCategoriaDto(g.Id, g.Categoria, g.CuentaGastoId)).ToList();
        return Result<IReadOnlyList<ConfiguracionGastoCategoriaDto>>.Success(dtos);
    }
}
