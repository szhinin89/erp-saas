using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Compras.DTOs;
using ERP.Domain.Modules.Compras.Interfaces;

namespace ERP.Application.Modules.Compras.UseCases.Retenciones;

public sealed record GetCompraRetencionesEmitidasListQuery(Guid? ProveedorId)
    : IRequest<Result<IReadOnlyList<CompraRetencionEmitidaListItemDto>>>;

public sealed class GetCompraRetencionesEmitidasListQueryHandler
    : IRequestHandler<GetCompraRetencionesEmitidasListQuery, Result<IReadOnlyList<CompraRetencionEmitidaListItemDto>>>
{
    private readonly ICompraRepository _compraRepository;
    private readonly ICurrentTenant  _currentTenant;

    public GetCompraRetencionesEmitidasListQueryHandler(
        ICompraRepository compraRepository,
        ICurrentTenant currentTenant)
    {
        _compraRepository = compraRepository;
        _currentTenant    = currentTenant;
    }

    public async Task<Result<IReadOnlyList<CompraRetencionEmitidaListItemDto>>> Handle(
        GetCompraRetencionesEmitidasListQuery request,
        CancellationToken ct)
    {
        var items = await _compraRepository.GetRetencionesEmitidasAsync(
            _currentTenant.TenantId, request.ProveedorId, ct);
        var dto = items.Select(r => new CompraRetencionEmitidaListItemDto(
            r.Id, r.ProveedorId, r.ClaveAcceso, r.Estado, r.TotalRetenido, r.FechaEmision)).ToList();
        return Result<IReadOnlyList<CompraRetencionEmitidaListItemDto>>.Success(dto);
    }
}
