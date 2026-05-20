using ERP.Application.Common;
using ERP.Application.Products.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Products.Interfaces;

using MediatR;

namespace ERP.Application.Products.UseCases.UpdateProductLine;

public class UpdateProductLineHandler : IRequestHandler<UpdateProductLineCommand, Result<ProductLineDto>>
{
    private readonly IProductCatalogRepository _repo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentSubscriber _currentSubscriber;
    private readonly ICurrentUser _currentUser;

    public UpdateProductLineHandler(
        IProductCatalogRepository repo,
        IUserActivityRepository activity,
        ICurrentSubscriber currentSubscriber,
        ICurrentUser currentUser)
    {
        _repo          = repo;
        _activity      = activity;
        _currentSubscriber = currentSubscriber;
        _currentUser   = currentUser;
    }

    public async Task<Result<ProductLineDto>> Handle(
        UpdateProductLineCommand command,
        CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        var userId   = _currentUser.UserId;

        var entity = await _repo.GetProductLineByIdAsync(subscriberId, command.Id, ct);
        if (entity is null)
            return Result<ProductLineDto>.Failure("Línea de producto no encontrada.");

        if (string.IsNullOrWhiteSpace(command.Code) || string.IsNullOrWhiteSpace(command.Name))
            return Result<ProductLineDto>.Failure("Código y nombre son obligatorios.");

        if (await _repo.ProductLineCodeExistsAsync(subscriberId, command.Code.Trim(), command.Id, ct))
            return Result<ProductLineDto>.Failure("Ya existe otra línea con el mismo código en este tenant.");

        entity.Update(command.Code.Trim(), command.Name.Trim(), userId);
        await _activity.AddAsync(UserActivity.Create(
            subscriberId,
            userId,
            _currentUser.Email,
            _currentUser.FullName,
            module: "inventario",
            action: "productLine.update",
            entityType: "ProductLine",
            entityId: entity.Id,
            description: $"{entity.Code} — {entity.Name}"), ct);
        await _repo.SaveChangesAsync(ct);

        return Result<ProductLineDto>.Success(
            new ProductLineDto(entity.Id, entity.Code, entity.Name, entity.IsActive));
    }
}
