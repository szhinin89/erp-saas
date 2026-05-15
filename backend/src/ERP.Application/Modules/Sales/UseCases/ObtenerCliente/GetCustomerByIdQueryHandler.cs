using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Sales.DTOs;
using ERP.Domain.Modules.Sales.Interfaces;

namespace ERP.Application.Modules.Sales.UseCases.ObtenerCliente;

public sealed class GetCustomerByIdQueryHandler
    : IRequestHandler<GetCustomerByIdQuery, Result<CustomerDetailDto>>
{
    private readonly ICustomerRepository _repo;
    private readonly ICurrentTenant _tenant;

    public GetCustomerByIdQueryHandler(ICustomerRepository repo, ICurrentTenant tenant)
    {
        _repo = repo;
        _tenant = tenant;
    }

    public async Task<Result<CustomerDetailDto>> Handle(GetCustomerByIdQuery request, CancellationToken ct)
    {
        var x = await _repo.GetByIdAsync(_tenant.TenantId, request.Id, ct);
        if (x is null)
            return Result<CustomerDetailDto>.Failure("Cliente no encontrado.");

        return Result<CustomerDetailDto>.Success(new CustomerDetailDto(
            x.Id,
            x.IdentificationType,
            x.IdentificationNumber,
            x.LegalName,
            x.TradeName,
            x.AddressLine,
            x.Phone,
            x.Email,
            x.Notes,
            x.IsActive,
            x.CreatedAt,
            x.UpdatedAt,
            x.CreatedBy,
            x.UpdatedBy));
    }
}
