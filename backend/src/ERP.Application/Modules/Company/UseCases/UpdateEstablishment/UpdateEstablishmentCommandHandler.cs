using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Company.DTOs;
using ERP.Application.Modules.Company.UseCases.GetEstablishments;
using ERP.Domain.Modules.Company.Interfaces;

namespace ERP.Application.Modules.Company.UseCases.UpdateEstablishment;

public sealed class UpdateEstablishmentCommandHandler
    : IRequestHandler<UpdateEstablishmentCommand, Result<EstablishmentDto>>
{
    private readonly IEstablishmentRepository _repo;
    private readonly ICurrentSubscriber       _subscriber;
    private readonly ICurrentUser             _user;

    public UpdateEstablishmentCommandHandler(
        IEstablishmentRepository repo, ICurrentSubscriber subscriber, ICurrentUser user)
    {
        _repo       = repo;
        _subscriber = subscriber;
        _user       = user;
    }

    public async Task<Result<EstablishmentDto>> Handle(UpdateEstablishmentCommand command, CancellationToken ct)
    {
        var entity = await _repo.GetByIdAsync(_subscriber.SubscriberId, command.Id, ct);
        if (entity is null)
            return Result<EstablishmentDto>.Failure("Establecimiento no encontrado.");

        entity.Update(command.Name, command.Address, command.Phone, _user.UserId);
        entity.SetMain(command.IsMain, _user.UserId);
        await _repo.SaveChangesAsync(ct);

        return Result<EstablishmentDto>.Success(GetEstablishmentsByBranchQueryHandler.ToDto(entity));
    }
}
