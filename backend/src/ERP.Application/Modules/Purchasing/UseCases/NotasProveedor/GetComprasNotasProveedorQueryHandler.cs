using MediatR;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.Purchasing.DTOs;
using ERP.Domain.Modules.Purchasing.Interfaces;

namespace ERP.Application.Modules.Purchasing.UseCases.NotasProveedor;

public sealed class GetComprasNotasProveedorQueryHandler
    : IRequestHandler<GetComprasNotasProveedorQuery, Result<IReadOnlyList<CompraNotaProveedorDto>>>
{
    private readonly ICompraRepository _repo;
    private readonly ICurrentTenant    _tenant;

    public GetComprasNotasProveedorQueryHandler(ICompraRepository repo, ICurrentTenant tenant)
    {
        _repo   = repo;
        _tenant = tenant;
    }

    public async Task<Result<IReadOnlyList<CompraNotaProveedorDto>>> Handle(
        GetComprasNotasProveedorQuery query,
        CancellationToken ct)
    {
        var list = await _repo.GetNotasProveedorAsync(
            _tenant.TenantId,
            query.ProveedorId,
            query.CompraFacturaId,
            query.GastoFacturaId,
            query.Estado,
            ct);

        IReadOnlyList<CompraNotaProveedorDto> dtos = list.Select(n => new CompraNotaProveedorDto(
            n.Id,
            n.ProveedorId,
            n.CompraFacturaId,
            n.GastoFacturaId,
            n.TipoNota,
            n.Motivo,
            n.ClaveAcceso,
            n.FechaEmision,
            n.Establecimiento,
            n.PuntoEmision,
            n.Secuencial,
            n.Subtotal,
            n.Impuesto,
            n.Total,
            n.Estado,
            n.XmlPath,
            n.AsientoContableId,
            n.CreatedAt)).ToList();

        return Result<IReadOnlyList<CompraNotaProveedorDto>>.Success(dtos);
    }
}
