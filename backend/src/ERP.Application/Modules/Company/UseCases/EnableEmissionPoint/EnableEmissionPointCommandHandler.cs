using MediatR;
using ERP.Application.Common;
using ERP.Domain.Modules.Company.Interfaces;

namespace ERP.Application.Modules.Company.UseCases.EnableEmissionPoint;

public sealed class EnableEmissionPointCommandHandler : IRequestHandler<EnableEmissionPointCommand, Result<bool>>
{
    private readonly IEmissionPointRepository _repo;
    private readonly ICurrentSubscriber       _subscriber;
    private readonly ICurrentUser             _user;

    public EnableEmissionPointCommandHandler(IEmissionPointRepository repo, ICurrentSubscriber subscriber, ICurrentUser user)
    {
        _repo = repo; _subscriber = subscriber; _user = user;
    }

    public async Task<Result<bool>> Handle(EnableEmissionPointCommand command, CancellationToken ct)
    {
        var entity = await _repo.GetByIdAsync(command.Id, _subscriber.SubscriberId, ct);
        if (entity is null) return Result<bool>.Failure("Punto de emisión no encontrado.");
        try { entity.Enable(_user.UserId); }
        catch (InvalidOperationException ex) { return Result<bool>.Failure(ex.Message); }
        await _repo.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
