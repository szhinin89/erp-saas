using ERP.Application.Common;
using ERP.Application.Products.Catalogs.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Products.Entities;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Products.Catalogs.UseCases.CreateTaxRate;

public class CreateTaxRateHandler
{
    private readonly IProductCatalogRepository _repo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _currentUser;

    public CreateTaxRateHandler(
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

    public async Task<Result<TaxRateDto>> HandleAsync(CreateTaxRateCommand command, CancellationToken ct = default)
    {
        var tenantId = _currentTenant.TenantId;
        var userId = _currentUser.UserId;

        var entity = TaxRate.Create(tenantId, command.Code, command.Name, command.Type, command.Percentage, userId);
        await _repo.AddTaxRateAsync(entity, ct);
        await _activity.AddAsync(UserActivity.Create(
            tenantId,
            userId,
            _currentUser.Email,
            _currentUser.FullName,
            module: "catalog",
            action: "taxRate.create",
            entityType: "TaxRate",
            entityId: entity.Id,
            description: $"{entity.Code} — {entity.Name} ({entity.Type} {entity.Percentage}%)"), ct);
        await _repo.SaveChangesAsync(ct);

        return Result<TaxRateDto>.Success(new TaxRateDto(entity.Id, entity.Code, entity.Name, entity.Type, entity.Percentage, entity.IsActive));
    }
}

