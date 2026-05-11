using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Inventario.DTOs;
using ERP.Domain.Modules.Inventario.Interfaces;

namespace ERP.Application.Modules.Inventario.UseCases.ListarBodegas;

public sealed class GetBodegasQueryHandler
    : IRequestHandler<GetBodegasQuery, Result<IReadOnlyList<BodegaDto>>>
{
    private readonly IBodegaRepository _repo;
    private readonly ICurrentTenant    _tenant;

    public GetBodegasQueryHandler(IBodegaRepository repo, ICurrentTenant tenant)
    {
        _repo   = repo;
        _tenant = tenant;
    }

    public async Task<Result<IReadOnlyList<BodegaDto>>> Handle(
        GetBodegasQuery query, CancellationToken ct)
    {
        var list = await _repo.GetAsync(
            _tenant.TenantId, query.ActiveFilter, query.Search, query.SucursalId, ct);

        var dtos = list.Select(b => new BodegaDto(
            b.Id, b.SucursalId, b.Nombre, b.Ubicacion, b.Encargado, b.IsActive))
            .ToList();

        return Result<IReadOnlyList<BodegaDto>>.Success(dtos);
    }
}
