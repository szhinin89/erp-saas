using ERP.Application.Access.DTOs;
using ERP.Application.Access.UseCases.CreateSystemUser;
using ERP.Application.Common;
using ERP.Domain.Tenants.Interfaces;
using MediatR;

namespace ERP.Application.Access.UseCases.CreateSystemUserAdmin;

/// <summary>
/// Único agregado propio: resolver TenantId/CompanyId del contexto autenticado y verificar que la
/// empresa activa coincida con la empresa por defecto del tenant, mismo criterio que
/// <see cref="ERP.Application.Access.UseCases.UpsertCompanyUserMembershipAdmin.UpsertCompanyUserMembershipAdminHandler"/>.
/// Nunca reimplementa la creación de IdentityUser/CompanyUserMembership — todo se delega vía MediatR.
/// </summary>
public sealed class CreateSystemUserAdminHandler
    : IRequestHandler<CreateSystemUserAdminCommand, Result<CreateSystemUserResultDto>>
{
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;
    private readonly ITenantRepository _tenantRepository;
    private readonly ICompanyProvisioningService _companyProvisioning;
    private readonly IMediator _mediator;

    public CreateSystemUserAdminHandler(
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

    public async Task<Result<CreateSystemUserResultDto>> Handle(CreateSystemUserAdminCommand command, CancellationToken cancellationToken)
    {
        var tenant = await _tenantRepository.GetByIdAsync(_currentTenant.TenantId, cancellationToken);
        if (tenant is null)
            return Result<CreateSystemUserResultDto>.NotFound("Tenant no encontrado.");

        var company = await _companyProvisioning.EnsureDefaultCompanyAsync(tenant, cancellationToken);
        if (company.Id != _currentCompany.CompanyId)
            return Result<CreateSystemUserResultDto>.Forbidden("La empresa activa no coincide con el contexto administrado.");

        return await _mediator.Send(
            new CreateSystemUserCommand(
                _currentTenant.TenantId,
                command.Username,
                command.FirstName,
                command.LastName,
                command.Email,
                command.Password,
                command.Role,
                command.ProfileId),
            cancellationToken);
    }
}
