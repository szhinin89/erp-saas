using ERP.Application.Common;
using ERP.Application.Products.Catalogs.DTOs;
using ERP.Domain.Products.Entities;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Products.Catalogs.UseCases.CreateTariff;

public class CreateTariffHandler
{
    private readonly IProductCatalogRepository _repo;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _currentUser;

    public CreateTariffHandler(IProductCatalogRepository repo, ICurrentTenant currentTenant, ICurrentUser currentUser)
    {
        _repo = repo;
        _currentTenant = currentTenant;
        _currentUser = currentUser;
    }

    public async Task<Result<TariffDto>> HandleAsync(CreateTariffCommand command, CancellationToken ct = default)
    {
        var tenantId = _currentTenant.TenantId;
        var userId = _currentUser.UserId;
        var entity = Tariff.Create(tenantId, command.Code, command.Description, userId);
        await _repo.AddTariffAsync(entity, ct);
        await _repo.SaveChangesAsync(ct);
        return Result<TariffDto>.Success(new TariffDto(entity.Id, entity.Code, entity.Description, entity.IsActive));
    }
}

