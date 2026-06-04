using MediatR;
using ERP.Application.Common;
using ERP.Domain.Modules.Items.Interfaces;

namespace ERP.Application.Items.UseCases.DisableItemVariant;

public sealed record DisableItemVariantCommand(Guid ItemId, Guid VariantId)
    : IRequest<Result<bool>>, ICompanyScopedRequest;

public sealed class DisableItemVariantCommandHandler
    : IRequestHandler<DisableItemVariantCommand, Result<bool>>
{
    private readonly IItemRepository _repository;
    private readonly ICurrentSubscriber _subscriber;
    private readonly ICurrentUser _user;

    public DisableItemVariantCommandHandler(
        IItemRepository repository, ICurrentSubscriber subscriber, ICurrentUser user)
    {
        _repository = repository;
        _subscriber = subscriber;
        _user       = user;
    }

    public async Task<Result<bool>> Handle(DisableItemVariantCommand cmd, CancellationToken ct)
    {
        var item = await _repository.GetByIdAsync(cmd.ItemId, _subscriber.SubscriberId, ct);
        if (item is null)
            return Result<bool>.NotFound("Ítem no encontrado.");

        try
        {
            item.DisableVariant(cmd.VariantId, _user.UserId);
            await _repository.SaveChangesAsync(ct);
            return Result<bool>.Success(true);
        }
        catch (InvalidOperationException ex)
        {
            return Result<bool>.ValidationFailure(ex.Message);
        }
    }
}
