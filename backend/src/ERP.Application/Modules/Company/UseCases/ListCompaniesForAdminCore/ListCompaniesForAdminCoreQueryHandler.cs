using ERP.Application.Common;
using ERP.Application.Modules.Company.DTOs;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Tenants.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Company.UseCases.ListCompaniesForAdminCore;

public sealed class ListCompaniesForAdminCoreQueryHandler
    : IRequestHandler<ListCompaniesForAdminCoreQuery, Result<IReadOnlyList<AdminCoreCompanyDto>>>
{
    private readonly ICompanyRepository _companyRepository;
    private readonly ITenantRepository _tenantRepository;

    public ListCompaniesForAdminCoreQueryHandler(
        ICompanyRepository companyRepository,
        ITenantRepository tenantRepository
    )
    {
        _companyRepository = companyRepository;
        _tenantRepository = tenantRepository;
    }

    public async Task<Result<IReadOnlyList<AdminCoreCompanyDto>>> Handle(
        ListCompaniesForAdminCoreQuery request,
        CancellationToken cancellationToken
    )
    {
        var companies = await _companyRepository.GetAllForAdminCoreAsync(cancellationToken);
        var tenants = await _tenantRepository.GetAllAsync(cancellationToken);
        var tenantsById = tenants.ToDictionary(t => t.Id);

        var dtos = companies
            .Select(c =>
            {
                tenantsById.TryGetValue(c.TenantId, out var tenant);
                return new AdminCoreCompanyDto(
                    c.TenantId,
                    tenant?.Name ?? "(tenant desconocido)",
                    tenant?.IsActive ?? false,
                    c.Id,
                    c.TaxIdentificationNumber,
                    c.LegalName,
                    c.TradeName,
                    c.IsActive
                );
            })
            .ToList();

        return Result<IReadOnlyList<AdminCoreCompanyDto>>.Success(dtos);
    }
}
