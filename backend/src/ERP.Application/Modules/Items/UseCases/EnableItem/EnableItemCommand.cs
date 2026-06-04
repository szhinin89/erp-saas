using MediatR;
using ERP.Application.Common;
using ERP.Domain.Modules.Items.Interfaces;

namespace ERP.Application.Items.UseCases.EnableItem;

public sealed record EnableItemCommand(Guid Id)
    : IRequest<Result<bool>>, ICompanyScopedRequest;

public sealed class EnableItemCommandHandler
    : IRequestHandler<EnableItemCommand, Result<bool>>
{
    private readonly IItemRepository _repository;
    private readonly ICurrentSubscriber _subscriber;
    private readonly ICurrentUser _user;

    public EnableItemCommandHandler(
        IItemRepository repository, ICurrentSubscriber subscriber, ICurrentUser user)
    {
        _repository = repository;
        _subscriber = subscriber;
        _user       = user;
    }

    public async Task<Result<bool>> Handle(EnableItemCommand cmd, CancellationToken ct)
    {
        var item = await _repository.GetByIdLightAsync(cmd.Id, _subscriber.SubscriberId, ct);
        if (item is null)
            return Result<bool>.NotFound("Ítem no encontrado.");

        try { item.Enable(_user.UserId); }
        catch (InvalidOperationException ex) { return Result<bool>.ValidationFailure(ex.Message); }

        await _repository.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
