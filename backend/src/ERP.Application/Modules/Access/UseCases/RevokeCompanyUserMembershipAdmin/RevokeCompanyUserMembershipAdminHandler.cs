using ERP.Application.Access.UseCases.RevokeCompanyUserMembership;
using ERP.Application.Common;
using ERP.Domain.Tenants.Interfaces;
using MediatR;

namespace ERP.Application.Access.UseCases.RevokeCompanyUserMembershipAdmin;

/// <summary>
/// Fase I-A. Mismo agregado propio que <see cref="UpsertCompanyUserMembershipAdmin.UpsertCompanyUserMembershipAdminHandler"/>:
/// resolver TenantId/CompanyId del contexto autenticado y verificar que la empresa activa
/// coincida con la empresa que <see cref="ICompanyProvisioningService.EnsureDefaultCompanyAsync"/>
/// resolvería para el tenant actual, antes de delegar en
/// <see cref="RevokeCompanyUserMembershipHandler"/> (Fase D) — nunca reimplementa su lógica.
/// </summary>
public sealed class RevokeCompanyUserMembershipAdminHandler
    : IRequestHandler<RevokeCompanyUserMembershipAdminCommand, Result<object>>
{
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;
    private readonly ITenantRepository _tenantRepository;
    private readonly ICompanyProvisioningService _companyProvisioning;
    private readonly IMediator _mediator;

    public RevokeCompanyUserMembershipAdminHandler(
        ICurrentTenant currentTenant,
        ICurrentCompany currentCompany,
        ITenantRepository tenantRepository,
        ICompanyProvisioningService companyProvisioning,
        IMediator mediator)
    {
        _currentTenant = currentTenant;
        _currentCompany = currentCompany;
        _tenantRepository = tenantRepository;
        _companyProvisioning = companyProvisioning;
        _mediator = mediator;
    }

    public async Task<Result<object>> Handle(RevokeCompanyUserMembershipAdminCommand command, CancellationToken cancellationToken)
    {
        var tenant = await _tenantRepository.GetByIdAsync(_currentTenant.TenantId, cancellationToken);
        if (tenant is null)
            return Result<object>.NotFound("Tenant no encontrado.");

        var company = await _companyProvisioning.EnsureDefaultCompanyAsync(tenant, cancellationToken);
        if (company.Id != _currentCompany.CompanyId)
            return Result<object>.Forbidden("La empresa activa no coincide con el contexto administrado.");

        return await _mediator.Send(
            new RevokeCompanyUserMembershipCommand(_currentTenant.TenantId, command.Username),
            cancellationToken);
    }
}
