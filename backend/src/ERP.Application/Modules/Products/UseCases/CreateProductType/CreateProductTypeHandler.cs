using ERP.Application.Common;
using ERP.Application.Products.DTOs;
using MediatR;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Products.Entities;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Products.UseCases.CreateProductType;

public class CreateProductTypeHandler : IRequestHandler<CreateProductTypeCommand, Result<ProductTypeDto>>
{
    private readonly IProductCatalogRepository _repo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentSubscriber _currentSubscriber;
    private readonly ICurrentUser _currentUser;

    public CreateProductTypeHandler(
        IProductCatalogRepository repo,
        IUserActivityRepository activity,
        ICurrentSubscriber currentSubscriber,
        ICurrentUser currentUser)
    {
        _repo = repo;
        _activity = activity;
        _currentSubscriber = currentSubscriber;
        _currentUser = currentUser;
    }

    public Task<Result<ProductTypeDto>> HandleAsync(CreateProductTypeCommand command, CancellationToken ct = default)
        => Handle(command, ct);

    public async Task<Result<ProductTypeDto>> Handle(CreateProductTypeCommand command, CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        var userId = _currentUser.UserId;
        var entity = ProductType.Create(subscriberId, command.Code, command.Name, userId);
        await _repo.AddProductTypeAsync(entity, ct);
        await _activity.AddAsync(UserActivity.Create(
            subscriberId,
            userId,
            _currentUser.Email,
            _currentUser.FullName,
            module: "inventario",
            action: "productType.create",
            entityType: "ProductType",
            entityId: entity.Id,
            description: $"{entity.Code} — {entity.Name}"), ct);
        await _repo.SaveChangesAsync(ct);
        return Result<ProductTypeDto>.Success(new ProductTypeDto(entity.Id, entity.Code, entity.Name, entity.IsActive));
    }
}

