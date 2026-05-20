using ERP.Application.Common;
using ERP.Application.Modules.Logistics.DTOs;
using ERP.Domain.Modules.Logistics.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Logistics.UseCases.GetCarriers;

public class GetCarriersHandler : IRequestHandler<GetCarriersQuery, Result<List<CarrierDto>>>
{
    private readonly ICarrierRepository _repo;
    private readonly ICurrentSubscriber     _currentSubscriber;

    public GetCarriersHandler(ICarrierRepository repo, ICurrentSubscriber currentSubscriber)
    {
        _repo          = repo;
        _currentSubscriber = currentSubscriber;
    }

    public async Task<Result<List<CarrierDto>>> Handle(GetCarriersQuery query, CancellationToken ct)
    {
        var carriers = await _repo.GetAllAsync(_currentSubscriber.SubscriberId, query.Search, query.IsActive, ct);

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
