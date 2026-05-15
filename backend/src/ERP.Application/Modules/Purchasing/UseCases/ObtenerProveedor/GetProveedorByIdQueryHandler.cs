using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;
using ERP.Domain.Modules.Purchasing.Interfaces;

namespace ERP.Application.Modules.Purchasing.UseCases.ObtenerProveedor;

public sealed class GetProveedorByIdQueryHandler
    : IRequestHandler<GetProveedorByIdQuery, Result<ProveedorDetailDto?>>
{
    private readonly ISupplierRepository _repo;
    private readonly ICurrentTenant       _tenant;

    public GetProveedorByIdQueryHandler(ISupplierRepository repo, ICurrentTenant tenant)
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
            p.Id, p.PersonType, p.LegalName, p.Ruc,
            p.Email, p.Phone, p.Address, p.PaymentTerms,
            p.IsActive, p.CreatedAt, p.UpdatedAt, p.CreatedBy, p.UpdatedBy));
    }
}
