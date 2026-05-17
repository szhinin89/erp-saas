using ERP.Application.Common;
using ERP.Application.Products.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Products.Entities;
using ERP.Domain.Products.Interfaces;

using MediatR;

namespace ERP.Application.Products.UseCases.CreateProductLine;

public class CreateProductLineHandler : IRequestHandler<CreateProductLineCommand, Result<ProductLineDto>>
{
    private readonly IProductCatalogRepository _repo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _currentUser;

    public CreateProductLineHandler(
        IProductCatalogRepository repo,
        IUserActivityRepository activity,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser)
    {
        _repo = repo;
        _activity = activity;
        _currentTenant = currentTenant;
        _currentUser = currentUser;
    }

    public async Task<Result<ProductLineDto>> Handle(CreateProductLineCommand command, CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;
        var userId = _currentUser.UserId;

        if (string.IsNullOrWhiteSpace(command.Code) || string.IsNullOrWhiteSpace(command.Name))
            return Result<ProductLineDto>.Failure("Código y nombre son obligatorios.");

        if (await _repo.ProductLineCodeExistsAsync(tenantId, command.Code.Trim(), null, ct))
            return Result<ProductLineDto>.Failure("Ya existe una línea con el mismo código en este tenant.");

        var entity = ProductLine.Create(tenantId, command.Code.Trim(), command.Name.Trim(), userId);
        await _repo.AddProductLineAsync(entity, ct);
        await _activity.AddAsync(UserActivity.Create(
            tenantId,
            userId,
            _currentUser.Email,
            _currentUser.FullName,
            module: "inventario",
            action: "productLine.create",
            entityType: "ProductLine",
            entityId: entity.Id,
            description: $"{entity.Code} — {entity.Name}"), ct);
        await _repo.SaveChangesAsync(ct);
        return Result<ProductLineDto>.Success(new ProductLineDto(entity.Id, entity.Code, entity.Name, entity.IsActive));
    }
}

