using ERP.Application.Common;
using ERP.Application.Tenants.DTOs;
using ERP.Domain.Tenants.Entities;
using ERP.Domain.Tenants.Interfaces;

namespace ERP.Application.Tenants.UseCases.CreateTenant;

public class CreateTenantHandler
{
    private readonly ITenantRepository _repository;

    public CreateTenantHandler(ITenantRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<TenantDto>> HandleAsync(
        CreateTenantCommand command,
        CancellationToken ct = default)
    {
        var exists = await _repository.GetBySlugAsync(command.Slug, ct);
        if (exists is not null)
            return Result<TenantDto>.Failure($"Ya existe un tenant con el slug '{command.Slug}'.");

        var tenant = Tenant.Create(
            command.Name,
            command.Slug,
            Guid.Empty);

        await _repository.AddAsync(tenant, ct);
        await _repository.SaveChangesAsync(ct);

        return Result<TenantDto>.Success(new TenantDto(
            tenant.Id,
            tenant.Name,
            tenant.Slug,
            tenant.IsActive,
            tenant.CreatedAt));
    }
}
