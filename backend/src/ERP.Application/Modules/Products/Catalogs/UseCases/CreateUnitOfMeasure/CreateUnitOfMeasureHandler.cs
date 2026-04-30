using ERP.Application.Common;
using ERP.Application.Products.Catalogs.DTOs;
using ERP.Domain.Products.Entities;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Products.Catalogs.UseCases.CreateUnitOfMeasure;

public class CreateUnitOfMeasureHandler
{
    private readonly IProductCatalogRepository _repo;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _currentUser;

    public CreateUnitOfMeasureHandler(IProductCatalogRepository repo, ICurrentTenant currentTenant, ICurrentUser currentUser)
    {
        _repo = repo;
        _currentTenant = currentTenant;
        _currentUser = currentUser;
    }

    public async Task<Result<UnitOfMeasureDto>> HandleAsync(CreateUnitOfMeasureCommand command, CancellationToken ct = default)
    {
        var tenantId = _currentTenant.TenantId;
        var userId = _currentUser.UserId;
        var entity = UnitOfMeasure.Create(tenantId, command.Code, command.Name, userId, command.Symbol);
        await _repo.AddUnitOfMeasureAsync(entity, ct);
        await _repo.SaveChangesAsync(ct);
        return Result<UnitOfMeasureDto>.Success(new UnitOfMeasureDto(entity.Id, entity.Code, entity.Name, entity.Symbol, entity.IsActive));
    }
}

