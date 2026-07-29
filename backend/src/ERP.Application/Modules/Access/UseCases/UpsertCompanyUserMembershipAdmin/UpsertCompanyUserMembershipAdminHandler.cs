using ERP.Application.Access.UseCases.UpsertCompanyUserMembership;
using ERP.Application.Common;
using ERP.Domain.Tenants.Interfaces;
using MediatR;

namespace ERP.Application.Access.UseCases.UpsertCompanyUserMembershipAdmin;

/// <summary>
/// Fase I-A. Único agregado propio: resolver TenantId/CompanyId del contexto autenticado y
/// verificar que la empresa activa (<see cref="ICurrentCompany.CompanyId"/>) coincida con la
/// empresa que <see cref="ICompanyProvisioningService.EnsureDefaultCompanyAsync"/> resolvería para
/// el tenant actual antes de delegar — ese método devuelve la primera empresa activa del tenant
/// (ver <c>CompanyProvisioningService.EnsureDefaultCompanyAsync</c>), que en un tenant con más de
/// una empresa administrada no necesariamente es la empresa activa del request. Sin esta
/// verificación explícita, un administrador con la empresa B seleccionada podría terminar
/// escribiendo sobre la empresa A (la "default" del tenant) sin darse cuenta. Nunca reimplementa
/// la lógica de membership/branch/preferences de
/// <see cref="UpsertCompanyUserMembershipHandler"/> — todo se delega vía MediatR.
/// </summary>
public sealed class UpsertCompanyUserMembershipAdminHandler
    : IRequestHandler<UpsertCompanyUserMembershipAdminCommand, Result<object>>
{
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;
    private readonly ITenantRepository _tenantRepository;
    private readonly ICompanyProvisioningService _companyProvisioning;
    private readonly IMediator _mediator;

    public UpsertCompanyUserMembershipAdminHandler(
        ICurrentTenant currentTenant,
        ICurrentCompany currentCompany,
        ITenantRepository tenantRepository,
        ICompanyProvisioningService companyProvisioning,
        IMediator mediator
    )
    {
        _currentTenant = currentTenant;
        _currentCompany = currentCompany;
        _tenantRepository = tenantRepository;
        _companyProvisioning = companyProvisioning;
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

        var company = await _companyProvisioning.EnsureDefaultCompanyAsync(
            tenant,
            cancellationToken
        );
        if (company.Id != _currentCompany.CompanyId)
            return Result<object>.Forbidden(
                "La empresa activa no coincide con el contexto administrado."
            );

        return await _mediator.Send(
            new UpsertCompanyUserMembershipCommand(
                _currentTenant.TenantId,
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
