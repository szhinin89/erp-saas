using ERP.Application.Common;
using ERP.Application.Modules.Logistics.DTOs;
using ERP.Domain.Modules.Logistics.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Logistics.UseCases.GetCarriers;

public class GetCarriersHandler : IRequestHandler<GetCarriersQuery, Result<List<CarrierDto>>>
{
    private readonly ICarrierRepository _repo;
    private readonly ICurrentTenant     _currentTenant;

    public GetCarriersHandler(ICarrierRepository repo, ICurrentTenant currentTenant)
    {
        _repo          = repo;
        _currentTenant = currentTenant;
    }

    public async Task<Result<List<CarrierDto>>> Handle(GetCarriersQuery query, CancellationToken ct)
    {
        var carriers = await _repo.GetAllAsync(_currentTenant.TenantId, query.Search, query.IsActive, ct);

        var dtos = carriers.Select(c => new CarrierDto(
            c.Id,
            c.IdentificationType,
            c.IdentificationNumber,
            c.LegalName,
            c.LicensePlate,
            c.Phone,
            c.Email,
            c.IsActive)).ToList();

        return Result<List<CarrierDto>>.Success(dtos);
    }
}
