using MediatR;
using ERP.Application.Common;
using ERP.Domain.Modules.Company.Interfaces;

namespace ERP.Application.Modules.Company.UseCases.EnableEstablishment;

public sealed class EnableEstablishmentCommandHandler : IRequestHandler<EnableEstablishmentCommand, Result<bool>>
{
    private readonly IEstablishmentRepository _repo;
    private readonly ICurrentSubscriber       _subscriber;
    private readonly ICurrentUser             _user;

    public EnableEstablishmentCommandHandler(IEstablishmentRepository repo, ICurrentSubscriber subscriber, ICurrentUser user)
    {
        _repo = repo; _subscriber = subscriber; _user = user;
    }

    public async Task<Result<bool>> Handle(EnableEstablishmentCommand command, CancellationToken ct)
    {
        var entity = await _repo.GetByIdAsync(_subscriber.SubscriberId, command.Id, ct);
        if (entity is null) return Result<bool>.Failure("Establecimiento no encontrado.");
        try { entity.Enable(_user.UserId); }
        catch (InvalidOperationException ex) { return Result<bool>.Failure(ex.Message); }
        await _repo.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
