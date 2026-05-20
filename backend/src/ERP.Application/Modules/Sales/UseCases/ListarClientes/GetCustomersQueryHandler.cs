using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Sales.DTOs;
using ERP.Domain.Modules.Sales.Interfaces;

namespace ERP.Application.Modules.Sales.UseCases.ListarClientes;

public sealed class GetCustomersQueryHandler
    : IRequestHandler<GetCustomersQuery, Result<IReadOnlyList<CustomerDto>>>
{
    private readonly ICustomerRepository _repo;
    private readonly ICurrentSubscriber _tenant;

    public GetCustomersQueryHandler(ICustomerRepository repo, ICurrentSubscriber tenant)
    {
        _repo = repo;
        _tenant = tenant;
    }

    public async Task<Result<IReadOnlyList<CustomerDto>>> Handle(GetCustomersQuery request, CancellationToken ct)
    {
        if (!_tenant.IsAuthenticated || _tenant.SubscriberId == Guid.Empty)
            return Result<IReadOnlyList<CustomerDto>>.Failure("Subscriber no autenticado.");

        var items = await _repo.GetAsync(_tenant.SubscriberId, request.ActiveFilter, request.Search, ct);
        var dtos = items.Select(x => new CustomerDto(
            x.Id,
            x.IdentificationType,
            x.IdentificationNumber,
            x.LegalName,
            x.TradeName,
            x.AddressLine,
            x.Phone,
            x.Email,
            x.Notes,
            x.IsActive)).ToList();

        return Result<IReadOnlyList<CustomerDto>>.Success(dtos);
    }
}
