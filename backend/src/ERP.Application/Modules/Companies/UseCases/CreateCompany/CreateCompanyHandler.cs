using ERP.Application.Common;
using ERP.Application.Modules.Companies.DTOs;
using ERP.Domain.Kernel.Security;
using ERP.Domain.Tenants.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Companies.UseCases.CreateCompany;

public sealed class CreateCompanyHandler
    : IRequestHandler<CreateCompanyCommand, Result<CompanyDetailDto>>
{
    private readonly ICompanyProvisioningService _provisioning;
    private readonly ICurrentUser _currentUser;
    private readonly ITenantRepository _tenantRepository;

    public CreateCompanyHandler(
        ICompanyProvisioningService provisioning,
        ICurrentUser currentUser,
        ITenantRepository tenantRepository
    )
    {
        _provisioning = provisioning;
        _currentUser = currentUser;
        _tenantRepository = tenantRepository;
    }

    public async Task<Result<CompanyDetailDto>> Handle(
        CreateCompanyCommand command,
        CancellationToken cancellationToken
    )
    {
        if (!_currentUser.IsAuthenticated)
            return Result<CompanyDetailDto>.Failure("No autenticado.");

        if (command.TenantId == Guid.Empty)
            return Result<CompanyDetailDto>.Failure("El tenant destino es obligatorio.");

        var tenant = await _tenantRepository.GetByIdAsync(command.TenantId, cancellationToken);
        if (tenant is null || !tenant.IsActive)
            return Result<CompanyDetailDto>.Failure("Tenant no válido o inactivo.");

        try
        {
            var company = await _provisioning.CreateManagedCompanyAsync(
                command.TenantId,
                command.TaxId,
                command.LegalName,
                mainAddress: "—",
                _currentUser.UserId,
                creatorRole: SecurityRoles.Admin,
                command.TradeName,
                cancellationToken: cancellationToken
            );

            return Result<CompanyDetailDto>.Success(CompanyDetailDto.FromEntity(company));
        }
        catch (InvalidOperationException ex)
        {
            return Result<CompanyDetailDto>.Failure(ex.Message);
        }
    }
}
