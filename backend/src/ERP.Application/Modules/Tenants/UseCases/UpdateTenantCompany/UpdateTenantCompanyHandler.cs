using ERP.Application.Common;
using ERP.Application.Tenants.DTOs;
using ERP.Domain.Tenants.Interfaces;

namespace ERP.Application.Tenants.UseCases.UpdateTenantCompany;

public sealed class UpdateTenantCompanyHandler
{
    private readonly ITenantRepository _repository;
    private readonly ICurrentUser _currentUser;

    public UpdateTenantCompanyHandler(ITenantRepository repository, ICurrentUser currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<Result<TenantDto>> HandleAsync(UpdateTenantCompanyCommand command, CancellationToken ct = default)
    {
        var tenant = await _repository.GetByIdAsync(command.TenantId, ct);
        if (tenant is null)
            return Result<TenantDto>.Failure("Empresa no encontrada.");

        var slug = command.Slug.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(slug))
            return Result<TenantDto>.Failure("Slug inválido.");

        if (!string.Equals(slug, tenant.Slug, StringComparison.Ordinal))
        {
            var other = await _repository.GetBySlugAsync(slug, ct);
            if (other is not null && other.Id != tenant.Id)
                return Result<TenantDto>.Failure("El slug ya está en uso.");
        }

        var name = command.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return Result<TenantDto>.Failure("El nombre de la empresa es obligatorio.");

        tenant.UpdateCompanyData(
            name,
            slug,
            command.Ruc,
            command.ShortName,
            command.TradeName,
            command.Dinardap,
            command.LogoUrl,
            command.DisplayOrder,
            command.Priority,
            _currentUser.UserId);

        await _repository.SaveChangesAsync(ct);

        return Result<TenantDto>.Success(TenantDto.FromTenant(tenant));
    }
}
