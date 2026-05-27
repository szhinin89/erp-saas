using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Company.DTOs;
using ERP.Domain.Modules.Company.Interfaces;

namespace ERP.Application.Modules.Company.UseCases.GetEmissionPoints;

public sealed class GetEmissionPointsByEstablishmentQueryHandler
    : IRequestHandler<GetEmissionPointsByEstablishmentQuery, Result<IReadOnlyList<EmissionPointDto>>>
{
    private readonly IEmissionPointRepository _repo;
    private readonly ICurrentSubscriber       _subscriber;

    public GetEmissionPointsByEstablishmentQueryHandler(IEmissionPointRepository repo, ICurrentSubscriber subscriber)
    {
        _repo = repo; _subscriber = subscriber;
    }

    public async Task<Result<IReadOnlyList<EmissionPointDto>>> Handle(
        GetEmissionPointsByEstablishmentQuery request, CancellationToken ct)
    {
        var items = await _repo.GetByEstablishmentAsync(_subscriber.SubscriberId, request.EstablishmentId, ct);
        var dtos = items.Select(ToDto).ToList();
        return Result<IReadOnlyList<EmissionPointDto>>.Success(dtos);
    }

    internal static EmissionPointDto ToDto(Domain.Modules.Company.Entities.EmissionPoint ep) => new(
        ep.Id, ep.EstablishmentId, ep.CompanyId, ep.Code, ep.Name,
        ep.IsDefault, ep.IsActive, ep.CreatedAt, ep.UpdatedAt);
}
