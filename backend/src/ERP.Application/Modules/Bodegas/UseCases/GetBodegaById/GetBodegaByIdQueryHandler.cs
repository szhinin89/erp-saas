using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Bodegas.DTOs;
using ERP.Domain.Bodegas.Interfaces;

namespace ERP.Application.Modules.Bodegas.UseCases.GetBodegaById;

public sealed class GetBodegaByIdQueryHandler
    : IRequestHandler<GetBodegaByIdQuery, Result<BodegaDetailDto?>>
{
    private readonly IBodegaRepository _repo;
    private readonly ICurrentTenant    _tenant;

    public GetBodegaByIdQueryHandler(IBodegaRepository repo, ICurrentTenant tenant)
    {
        _repo   = repo;
        _tenant = tenant;
    }

    public async Task<Result<BodegaDetailDto?>> Handle(
        GetBodegaByIdQuery query, CancellationToken ct)
    {
        var b = await _repo.GetByIdAsync(_tenant.TenantId, query.Id, ct);
        if (b is null) return Result<BodegaDetailDto?>.Success(null);

        return Result<BodegaDetailDto?>.Success(new BodegaDetailDto(
            b.Id, b.SucursalId, b.Nombre, b.Ubicacion, b.Encargado,
            b.IsActive, b.CreatedAt, b.UpdatedAt, b.CreatedBy, b.UpdatedBy));
    }
}
