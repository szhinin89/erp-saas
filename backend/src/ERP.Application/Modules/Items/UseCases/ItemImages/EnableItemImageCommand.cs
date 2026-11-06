using ERP.Application.Common;
using ERP.Application.Items.DTOs;
using ERP.Domain.Modules.Items.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Items.UseCases.ItemImages;

public sealed record EnableItemImageCommand(Guid ItemId, Guid ImageId)
    : IRequest<Result<ItemImageDto>>, ICompanyScopedRequest;

public sealed class EnableItemImageCommandValidator : AbstractValidator<EnableItemImageCommand>
{
    public EnableItemImageCommandValidator()
    {
        RuleFor(x => x.ItemId).NotEmpty();
        RuleFor(x => x.ImageId).NotEmpty();
    }
}

public sealed class EnableItemImageCommandHandler(
    IItemRepository repository, IUnitOfWork unitOfWork, ICurrentTenant tenant, ICurrentUser user)
    : IRequestHandler<EnableItemImageCommand, Result<ItemImageDto>>
{
    public async Task<Result<ItemImageDto>> Handle(EnableItemImageCommand cmd, CancellationToken cancellationToken)
    {
        if (unitOfWork.HasActiveTransaction)
            throw new InvalidOperationException("Image mutation must own its transaction.");
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var item = await repository.GetWithImagesForUpdateAsync(cmd.ItemId, tenant.TenantId, cancellationToken);
            if (item is null)
                return Result<ItemImageDto>.NotFound("Ítem no encontrado.");
            var image = item.Images.FirstOrDefault(i => i.Id == cmd.ImageId);
            if (image is null)
                return Result<ItemImageDto>.NotFound("Imagen no encontrada.");

            if (image.Enable(user.UserId))
            {
                item.UpdateSaleConfig(item.SaleConfig, user.UserId);
                await repository.SaveChangesAsync(cancellationToken);
            }
            var dto = new ItemImageDto(image.Id, image.VariantId, image.StorageObjectId,
                image.AltText, image.IsMain, image.IsEcommerce, image.SortOrder, image.IsActive);
            await unitOfWork.CommitAsync(cancellationToken);
            return Result<ItemImageDto>.Success(dto);
        }
        finally
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
        }
    }
}
