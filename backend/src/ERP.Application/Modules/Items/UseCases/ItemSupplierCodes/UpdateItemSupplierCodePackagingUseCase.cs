using ERP.Application.Common;
using ERP.Application.Items.DTOs;
using ERP.Domain.Modules.Items.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Items.UseCases.ItemSupplierCodes;

public sealed record UpdateItemSupplierCodePackagingCommand(
    Guid ItemId,
    Guid SupplierId,
    string Code,
    Guid? PackagingLevelId
) : IRequest<Result<ItemDetailDto>>, ICompanyScopedRequest;

public sealed class UpdateItemSupplierCodePackagingCommandValidator
    : AbstractValidator<UpdateItemSupplierCodePackagingCommand>
{
    public UpdateItemSupplierCodePackagingCommandValidator()
    {
        RuleFor(x => x.ItemId).NotEmpty();
        RuleFor(x => x.SupplierId).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PackagingLevelId)
            .Must(id => id is null || id.Value != Guid.Empty)
            .WithMessage("El nivel de empaque no es válido.");
    }
}

public sealed class UpdateItemSupplierCodePackagingCommandHandler
    : IRequestHandler<UpdateItemSupplierCodePackagingCommand, Result<ItemDetailDto>>
{
    private readonly IItemRepository _repository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _currentUser;
    private readonly ISriCatalogResolver _sri;
    private readonly IItemTypeRepository _itemTypeRepo;

    public UpdateItemSupplierCodePackagingCommandHandler(
        IItemRepository repository,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        ISriCatalogResolver sri,
        IItemTypeRepository itemTypeRepo
    )
    {
        _repository = repository;
        _currentTenant = currentTenant;
        _currentUser = currentUser;
        _sri = sri;
        _itemTypeRepo = itemTypeRepo;
    }

    public async Task<Result<ItemDetailDto>> Handle(
        UpdateItemSupplierCodePackagingCommand request,
        CancellationToken cancellationToken
    )
    {
        if (
            request.PackagingLevelId.HasValue
            && !await _repository.PackagingLevelBelongsToItemAsync(
                request.ItemId,
                request.PackagingLevelId.Value,
                _currentTenant.TenantId,
                cancellationToken
            )
        )
        {
            return Result<ItemDetailDto>.ValidationFailure(
                "La presentación seleccionada no pertenece al ítem."
            );
        }

        try
        {
            await _repository.UpdateSupplierCodePackagingLevelAsync(
                request.ItemId,
                request.SupplierId,
                request.Code,
                request.PackagingLevelId,
                _currentTenant.TenantId,
                _currentUser.UserId,
                cancellationToken
            );
            await _repository.SaveChangesAsync(cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return Result<ItemDetailDto>.ValidationFailure(ex.Message);
        }

        var updated = await _repository.GetByIdAsync(
            request.ItemId,
            _currentTenant.TenantId,
            cancellationToken
        );
        if (updated is null)
            return Result<ItemDetailDto>.NotFound("Ítem no encontrado.");

        return Result<ItemDetailDto>.Success(
            await ItemMappingService.ToDetailDtoAsync(
                updated,
                _sri,
                _itemTypeRepo,
                _currentTenant.TenantId,
                cancellationToken
            )
        );
    }
}
