using ERP.Application.Common;
using ERP.Application.Products.Catalogs.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Products.Catalogs.UseCases.UpdateProductLine;

public class UpdateProductLineHandler
{
    private readonly IProductCatalogRepository _repo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _currentUser;

    public UpdateProductLineHandler(
        IProductCatalogRepository repo,
        IUserActivityRepository activity,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser)
    {
        _repo          = repo;
        _activity      = activity;
        _currentTenant = currentTenant;
        _currentUser   = currentUser;
    }

    public async Task<Result<ProductLineDto>> HandleAsync(
        UpdateProductLineCommand command,
        CancellationToken ct = default)
    {
        var tenantId = _currentTenant.TenantId;
        var userId   = _currentUser.UserId;

        var entity = await _repo.GetProductLineByIdAsync(tenantId, command.Id, ct);
        if (entity is null)
            return Result<ProductLineDto>.Failure("Línea de producto no encontrada.");

        if (string.IsNullOrWhiteSpace(command.Code) || string.IsNullOrWhiteSpace(command.Name))
            return Result<ProductLineDto>.Failure("Código y nombre son obligatorios.");

        if (await _repo.ProductLineCodeExistsAsync(tenantId, command.Code.Trim(), command.Id, ct))
            return Result<ProductLineDto>.Failure("Ya existe otra línea con el mismo código en este tenant.");

        entity.Update(command.Code.Trim(), command.Name.Trim(), userId);
        await _activity.AddAsync(UserActivity.Create(
            tenantId,
            userId,
            _currentUser.Email,
            _currentUser.FullName,
            module: "catalog",
            action: "productLine.update",
            entityType: "ProductLine",
            entityId: entity.Id,
            description: $"{entity.Code} — {entity.Name}"), ct);
        await _repo.SaveChangesAsync(ct);

        return Result<ProductLineDto>.Success(
            new ProductLineDto(entity.Id, entity.Code, entity.Name, entity.IsActive));
    }
}
