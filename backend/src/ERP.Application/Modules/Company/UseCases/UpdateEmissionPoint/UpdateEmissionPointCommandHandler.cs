using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Company.DTOs;
using ERP.Application.Modules.Company.UseCases.GetEmissionPoints;
using ERP.Domain.Modules.Company.Interfaces;

namespace ERP.Application.Modules.Company.UseCases.UpdateEmissionPoint;

public sealed class UpdateEmissionPointCommandHandler
    : IRequestHandler<UpdateEmissionPointCommand, Result<EmissionPointDto>>
{
    private readonly IEmissionPointRepository _repo;
    private readonly ICurrentSubscriber       _subscriber;
    private readonly ICurrentUser             _user;

    public UpdateEmissionPointCommandHandler(IEmissionPointRepository repo, ICurrentSubscriber subscriber, ICurrentUser user)
    {
        _repo = repo; _subscriber = subscriber; _user = user;
    }

    public async Task<Result<EmissionPointDto>> Handle(UpdateEmissionPointCommand command, CancellationToken ct)
    {
        var subscriberId = _subscriber.SubscriberId;
        var entity = await _repo.GetByIdAsync(command.Id, subscriberId, ct);
        if (entity is null) return Result<EmissionPointDto>.Failure("Punto de emisión no encontrado.");

        if (command.IsDefault && !entity.IsDefault)
            await _repo.ClearDefaultExceptAsync(subscriberId, entity.EstablishmentId, command.Id, _user.UserId, ct);

        entity.Update(command.Name, _user.UserId);
        entity.SetDefault(command.IsDefault, _user.UserId);
        await _repo.SaveChangesAsync(ct);

        return Result<EmissionPointDto>.Success(GetEmissionPointsByEstablishmentQueryHandler.ToDto(entity));
    }
}
