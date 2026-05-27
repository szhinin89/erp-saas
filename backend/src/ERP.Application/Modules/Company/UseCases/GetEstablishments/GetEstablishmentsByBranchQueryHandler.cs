using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Company.DTOs;
using ERP.Domain.Modules.Company.Interfaces;

namespace ERP.Application.Modules.Company.UseCases.GetEstablishments;

public sealed class GetEstablishmentsByBranchQueryHandler
    : IRequestHandler<GetEstablishmentsByBranchQuery, Result<IReadOnlyList<EstablishmentDto>>>
{
    private readonly IEstablishmentRepository _repo;
    private readonly ICurrentSubscriber       _subscriber;

    public GetEstablishmentsByBranchQueryHandler(IEstablishmentRepository repo, ICurrentSubscriber subscriber)
    {
        _repo       = repo;
        _subscriber = subscriber;
    }

    public async Task<Result<IReadOnlyList<EstablishmentDto>>> Handle(
        GetEstablishmentsByBranchQuery request, CancellationToken ct)
    {
        var items = await _repo.GetByBranchAsync(_subscriber.SubscriberId, request.BranchId, ct);
        var dtos = items.Select(ToDto).ToList();
        return Result<IReadOnlyList<EstablishmentDto>>.Success(dtos);
    }

    internal static EstablishmentDto ToDto(Domain.Modules.Company.Entities.Establishment e) => new(
        e.Id, e.BranchId, e.CompanyId, e.Code, e.Name, e.Address, e.Phone,
        e.IsMain, e.IsActive, e.CreatedAt, e.UpdatedAt);
}
