using MediatR;
using ERP.Application.Common;
using ERP.Domain.Modules.Company.Interfaces;

namespace ERP.Application.Modules.Company.UseCases.DisableEmissionPoint;

public sealed class DisableEmissionPointCommandHandler : IRequestHandler<DisableEmissionPointCommand, Result<bool>>
{
    private readonly IEmissionPointRepository _repo;
    private readonly ICurrentSubscriber       _subscriber;
    private readonly ICurrentUser             _user;

    public DisableEmissionPointCommandHandler(IEmissionPointRepository repo, ICurrentSubscriber subscriber, ICurrentUser user)
    {
        _repo = repo; _subscriber = subscriber; _user = user;
    }

    public async Task<Result<bool>> Handle(DisableEmissionPointCommand command, CancellationToken ct)
    {
        var entity = await _repo.GetByIdAsync(command.Id, _subscriber.SubscriberId, ct);
        if (entity is null) return Result<bool>.Failure("Punto de emisión no encontrado.");
        try { entity.Disable(_user.UserId); }
        catch (InvalidOperationException ex) { return Result<bool>.Failure(ex.Message); }
        await _repo.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
