using ERP.Application.Common;
using ERP.Domain.Modules.Items.Interfaces;
using MediatR;

namespace ERP.Application.Items.UseCases.DisableItem;

public sealed record DisableItemCommand(Guid Id) : IRequest<Result<bool>>, ICompanyScopedRequest;

public sealed class DisableItemCommandHandler : IRequestHandler<DisableItemCommand, Result<bool>>
{
    private readonly IItemRepository _repository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _user;

    public DisableItemCommandHandler(
        IItemRepository repository,
        ICurrentTenant tenant,
        ICurrentUser user
    )
    {
        _repository = repository;
        _currentTenant = tenant;
        _user = user;
    }

    public async Task<Result<bool>> Handle(
        DisableItemCommand cmd,
        CancellationToken cancellationToken
    )
    {
        var item = await _repository.GetByIdLightAsync(
            cmd.Id,
            _currentTenant.TenantId,
            cancellationToken
        );
        if (item is null)
            return Result<bool>.NotFound("Ítem no encontrado.");

        try
        {
            item.Disable(_user.UserId);
        }
        catch (InvalidOperationException ex)
        {
            return Result<bool>.ValidationFailure(ex.Message);
        }

        await _repository.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}
