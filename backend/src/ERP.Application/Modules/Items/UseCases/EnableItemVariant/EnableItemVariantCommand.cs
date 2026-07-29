using ERP.Application.Common;
using ERP.Domain.Modules.Items.Interfaces;
using MediatR;

namespace ERP.Application.Items.UseCases.EnableItemVariant;

public sealed record EnableItemVariantCommand(Guid ItemId, Guid VariantId)
    : IRequest<Result<bool>>, ICompanyScopedRequest;

public sealed class EnableItemVariantCommandHandler
    : IRequestHandler<EnableItemVariantCommand, Result<bool>>
{
    private readonly IItemRepository _repository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _user;

    public EnableItemVariantCommandHandler(
        IItemRepository repository, ICurrentTenant tenant, ICurrentUser user)
    {
        _repository = repository;
        _currentTenant = tenant;
        _user = user;
    }

    public async Task<Result<bool>> Handle(EnableItemVariantCommand cmd, CancellationToken cancellationToken)
    {
        var item = await _repository.GetByIdAsync(cmd.ItemId, _currentTenant.TenantId, cancellationToken);
        if (item is null)
            return Result<bool>.NotFound("Ítem no encontrado.");

        try
        {
            item.EnableVariant(cmd.VariantId, _user.UserId);
            await _repository.SaveChangesAsync(cancellationToken);
            return Result<bool>.Success(true);
        }
        catch (InvalidOperationException ex)
        {
            return Result<bool>.ValidationFailure(ex.Message);
        }
    }
}
