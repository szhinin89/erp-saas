using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;
using ERP.Domain.Modules.Purchasing.Interfaces;

namespace ERP.Application.Modules.Purchasing.UseCases.ListarProveedores;

public sealed class GetSuppliersQueryHandler
    : IRequestHandler<GetSuppliersQuery, Result<IReadOnlyList<SupplierDto>>>
{
    private readonly ISupplierRepository _repo;
    private readonly ICurrentTenant       _tenant;

    public GetSuppliersQueryHandler(ISupplierRepository repo, ICurrentTenant tenant)
    {
        _repo   = repo;
        _tenant = tenant;
    }

    public async Task<Result<IReadOnlyList<SupplierDto>>> Handle(
        GetSuppliersQuery query, CancellationToken ct)
    {
        var list = await _repo.GetAsync(
            _tenant.TenantId, query.ActiveFilter, query.Search, query.PersonType, ct);

        var dtos = list.Select(p => new SupplierDto(
            p.Id, p.PersonType, p.LegalName, p.Ruc,
            p.Email, p.Phone, p.Address, p.PaymentTerms, p.IsActive))
            .ToList();

        return Result<IReadOnlyList<SupplierDto>>.Success(dtos);
    }
}
