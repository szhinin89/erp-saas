using ERP.Application.Access.DTOs;
using ERP.Application.Access.UseCases.CreateSystemUser;
using ERP.Application.Common;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Tenants.Interfaces;
using MediatR;

namespace ERP.Application.Access.UseCases.CreateSystemUserAdmin;

/// <summary>
/// Único agregado propio: resolver TenantId/CompanyId del contexto autenticado y verificar que la
/// empresa activa exista dentro del tenant actual, mismo criterio que
/// <see cref="ERP.Application.Access.UseCases.UpsertCompanyUserMembershipAdmin.UpsertCompanyUserMembershipAdminHandler"/>.
/// Nunca reimplementa la creación de IdentityUser/CompanyUserMembership — todo se delega vía MediatR.
/// </summary>
public sealed class CreateSystemUserAdminHandler
    : IRequestHandler<CreateSystemUserAdminCommand, Result<CreateSystemUserResultDto>>
{
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;
    private readonly ITenantRepository _tenantRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly IMediator _mediator;

    public CreateSystemUserAdminHandler(
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

    public async Task<Result<CreateSystemUserResultDto>> Handle(
        CreateSystemUserAdminCommand command,
        CancellationToken cancellationToken
    )
    {
        var tenant = await _tenantRepository.GetByIdAsync(
            _currentTenant.TenantId,
            cancellationToken
        );
        if (tenant is null)
            return Result<CreateSystemUserResultDto>.NotFound("Tenant no encontrado.");

        var company = await _companyRepository.GetByIdForTenantAsync(
            _currentCompany.CompanyId,
            _currentTenant.TenantId,
            cancellationToken
        );
        if (company is null)
            return Result<CreateSystemUserResultDto>.NotFound(
                "Empresa activa no encontrada para el tenant."
            );

        return await _mediator.Send(
            new CreateSystemUserCommand(
                _currentTenant.TenantId,
                company.Id,
                command.Username,
                command.FirstName,
                command.LastName,
                command.Email,
                command.Password,
                command.Role,
                command.ProfileId
            ),
            cancellationToken
        );
    }
}
