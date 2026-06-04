using MediatR;
using ERP.Application.Common;
using ERP.Domain.Modules.Items.Interfaces;

namespace ERP.Application.Items.UseCases.EnableItemVariant;

public sealed record EnableItemVariantCommand(Guid ItemId, Guid VariantId)
    : IRequest<Result<bool>>, ICompanyScopedRequest;

public sealed class EnableItemVariantCommandHandler
    : IRequestHandler<EnableItemVariantCommand, Result<bool>>
{
    private readonly IItemRepository _repository;
    private readonly ICurrentSubscriber _subscriber;
    private readonly ICurrentUser _user;

    public EnableItemVariantCommandHandler(
        IItemRepository repository, ICurrentSubscriber subscriber, ICurrentUser user)
    {
        _repository = repository;
        _subscriber = subscriber;
        _user       = user;
    }

    public async Task<Result<bool>> Handle(EnableItemVariantCommand cmd, CancellationToken ct)
    {
        var item = await _repository.GetByIdAsync(cmd.ItemId, _subscriber.SubscriberId, ct);
        if (item is null)
            return Result<bool>.NotFound("Ítem no encontrado.");

        try
        {
            item.EnableVariant(cmd.VariantId, _user.UserId);
            await _repository.SaveChangesAsync(ct);
            return Result<bool>.Success(true);
        }
        catch (InvalidOperationException ex)
        {
            return Result<bool>.ValidationFailure(ex.Message);
        }
    }
}
