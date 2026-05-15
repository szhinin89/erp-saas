using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;
using ERP.Domain.Modules.Purchasing.Interfaces;

namespace ERP.Application.Modules.Purchasing.UseCases.ListarProveedores;

public sealed class GetProveedoresQueryHandler
    : IRequestHandler<GetProveedoresQuery, Result<IReadOnlyList<ProveedorDto>>>
{
    private readonly IProveedorRepository _repo;
    private readonly ICurrentTenant       _tenant;

    public GetProveedoresQueryHandler(IProveedorRepository repo, ICurrentTenant tenant)
    {
        _repo   = repo;
        _tenant = tenant;
    }

    public async Task<Result<IReadOnlyList<ProveedorDto>>> Handle(
        GetProveedoresQuery query, CancellationToken ct)
    {
        var list = await _repo.GetAsync(
            _tenant.TenantId, query.ActiveFilter, query.Search, query.TipoPersona, ct);

        var dtos = list.Select(p => new ProveedorDto(
            p.Id, p.TipoPersona, p.RazonSocial, p.Ruc,
            p.Correo, p.Telefono, p.Direccion, p.CondicionPago, p.IsActive))
            .ToList();

        return Result<IReadOnlyList<ProveedorDto>>.Success(dtos);
    }
}
