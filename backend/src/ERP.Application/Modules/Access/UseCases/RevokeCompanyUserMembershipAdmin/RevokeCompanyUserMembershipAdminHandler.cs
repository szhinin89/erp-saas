using ERP.Application.Access.UseCases.RevokeCompanyUserMembership;
using ERP.Application.Common;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Tenants.Interfaces;
using MediatR;

namespace ERP.Application.Access.UseCases.RevokeCompanyUserMembershipAdmin;

/// <summary>
/// Fase I-A. Mismo agregado propio que <see cref="UpsertCompanyUserMembershipAdmin.UpsertCompanyUserMembershipAdminHandler"/>:
/// resolver TenantId/CompanyId del contexto autenticado y verificar que la empresa activa
/// exista dentro del tenant actual, antes de delegar en
/// <see cref="RevokeCompanyUserMembershipHandler"/> (Fase D) — nunca reimplementa su lógica.
/// </summary>
public sealed class RevokeCompanyUserMembershipAdminHandler
    : IRequestHandler<RevokeCompanyUserMembershipAdminCommand, Result<object>>
{
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;
    private readonly ITenantRepository _tenantRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly IMediator _mediator;

    public RevokeCompanyUserMembershipAdminHandler(
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
        RevokeCompanyUserMembershipAdminCommand command,
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
            new RevokeCompanyUserMembershipCommand(
                _currentTenant.TenantId,
                company.Id,
                command.Username
            ),
            cancellationToken
        );
    }
}
