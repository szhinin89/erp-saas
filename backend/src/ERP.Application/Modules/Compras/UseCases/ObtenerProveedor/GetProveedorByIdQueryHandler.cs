using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Compras.DTOs;
using ERP.Domain.Modules.Compras.Interfaces;

namespace ERP.Application.Modules.Compras.UseCases.ObtenerProveedor;

public sealed class GetProveedorByIdQueryHandler
    : IRequestHandler<GetProveedorByIdQuery, Result<ProveedorDetailDto?>>
{
    private readonly IProveedorRepository _repo;
    private readonly ICurrentTenant       _tenant;

    public GetProveedorByIdQueryHandler(IProveedorRepository repo, ICurrentTenant tenant)
    {
        _repo   = repo;
        _tenant = tenant;
    }

    public async Task<Result<ProveedorDetailDto?>> Handle(
        GetProveedorByIdQuery query, CancellationToken ct)
    {
        var p = await _repo.GetByIdAsync(_tenant.TenantId, query.Id, ct);
        if (p is null) return Result<ProveedorDetailDto?>.Success(null);

        return Result<ProveedorDetailDto?>.Success(new ProveedorDetailDto(
            p.Id, p.TipoPersona, p.RazonSocial, p.Ruc,
            p.Correo, p.Telefono, p.Direccion, p.CondicionPago,
            p.IsActive, p.CreatedAt, p.UpdatedAt, p.CreatedBy, p.UpdatedBy));
    }
}
