using ERP.Application.Access.UseCases.UpsertCompanyUserMembership;
using ERP.Application.Common;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Tenants.Interfaces;
using MediatR;

namespace ERP.Application.Access.UseCases.UpsertCompanyUserMembershipAdmin;

/// <summary>
/// Fase I-A. Único agregado propio: resolver TenantId/CompanyId del contexto autenticado y
/// verificar que la empresa activa (<see cref="ICurrentCompany.CompanyId"/>) exista dentro del
/// tenant actual antes de delegar. Nunca reimplementa la lógica de membership/branch/preferences de
/// <see cref="UpsertCompanyUserMembershipHandler"/> — todo se delega vía MediatR.
/// </summary>
public sealed class UpsertCompanyUserMembershipAdminHandler
    : IRequestHandler<UpsertCompanyUserMembershipAdminCommand, Result<object>>
{
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;
    private readonly ITenantRepository _tenantRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly IMediator _mediator;

    public UpsertCompanyUserMembershipAdminHandler(
        ICurrentTenant currentTenant,
        ICurrentCompany currentCompany,
        ITenantRepository tenantRepository,
        ICompanyRepository companyRepository,
        IMediator mediator
    )
    {
        _currentTenant = currentTenant;
        _currentCompany = currentCompany;
        _tenantRepository = tenantRepository;
        _companyRepository = companyRepository;
        _mediator = mediator;
    }

    public async Task<Result<object>> Handle(
        UpsertCompanyUserMembershipAdminCommand command,
        CancellationToken cancellationToken
    )
    {
        var tenant = await _tenantRepository.GetByIdAsync(
            _currentTenant.TenantId,
            cancellationToken
        );
        if (tenant is null)
            return Result<object>.NotFound("Tenant no encontrado.");

        var company = await _companyRepository.GetByIdForTenantAsync(
            _currentCompany.CompanyId,
            _currentTenant.TenantId,
            cancellationToken
        );
        if (company is null)
            return Result<object>.NotFound("Empresa activa no encontrada para el tenant.");

        return await _mediator.Send(
            new UpsertCompanyUserMembershipCommand(
                _currentTenant.TenantId,
                company.Id,
                command.Username,
                command.Role,
                command.ProfileId,
                command.AuthorizedBranchIds,
                command.DefaultBranchId,
                command.LoginMode
            ),
            cancellationToken
        );
    }
}
