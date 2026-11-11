using ERP.Application.Auth;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.Companies.DTOs;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Exceptions;
using ERP.Domain.Kernel.Security;
using ERP.Domain.Tenants.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Companies.UseCases.CreateCompany;

public sealed class CreateCompanyHandler
    : IRequestHandler<CreateCompanyCommand, Result<CompanyCreatedDto>>
{
    private readonly ICompanyProvisioningService _provisioning;
    private readonly ICurrentUser _currentUser;
    private readonly ITenantRepository _tenantRepository;
    private readonly IAccessRepository _access;
    private readonly IPasswordHasher _hasher;

    public CreateCompanyHandler(
        ICompanyProvisioningService provisioning,
        ICurrentUser currentUser,
        ITenantRepository tenantRepository,
        IAccessRepository access,
        IPasswordHasher hasher
    )
    {
        _provisioning = provisioning;
        _currentUser = currentUser;
        _tenantRepository = tenantRepository;
        _access = access;
        _hasher = hasher;
    }

    public async Task<Result<CompanyCreatedDto>> Handle(
        CreateCompanyCommand command,
        CancellationToken cancellationToken
    )
    {
        if (!_currentUser.IsAuthenticated)
            return Result<CompanyCreatedDto>.Failure("No autenticado.");

        if (command.TenantId == Guid.Empty)
            return Result<CompanyCreatedDto>.Failure("El tenant destino es obligatorio.");

        var tenant = await _tenantRepository.GetByIdAsync(command.TenantId, cancellationToken);
        if (tenant is null || !tenant.IsActive)
            return Result<CompanyCreatedDto>.Failure("Tenant no válido o inactivo.");

        InitialCompanyAdministrator? administrator = null;
        string? temporaryPassword = null;
        if (command.InitialAdministrator is { } request)
        {
            var username = request.Username.Trim().ToLowerInvariant();
            var email = request.Email.Trim().ToLowerInvariant();

            // Comprobación temprana (antes de crear nada). La definitiva ocurre dentro de la
            // transacción de aprovisionamiento y los índices únicos cubren la concurrencia.
            if (
                await _access.AnyUserWithUsernameAsync(username, cancellationToken)
                || await _access.AnyUserWithEmailAsync(email, cancellationToken)
            )
                return Result<CompanyCreatedDto>.Conflict(
                    InitialAdministratorIdentityUnavailableException.GenericMessage
                );

            temporaryPassword = TemporaryPasswordGenerator.Generate();
            administrator = new InitialCompanyAdministrator(
                username,
                request.FirstName.Trim(),
                request.LastName.Trim(),
                email,
                _hasher.HashPassword(temporaryPassword)
            );
        }

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
                cancellationToken: cancellationToken,
                initialAdministrator: administrator
            );

            var credentials =
                administrator is null
                    ? null
                    : new InitialAdministratorCredentialsDto(
                        await ResolveAdministratorIdAsync(administrator.Username, cancellationToken),
                        administrator.Username,
                        temporaryPassword!
                    );

            return Result<CompanyCreatedDto>.Success(
                CompanyCreatedDto.From(CompanyDetailDto.FromEntity(company), credentials)
            );
        }
        catch (InitialAdministratorIdentityUnavailableException)
        {
            return Result<CompanyCreatedDto>.Conflict(
                InitialAdministratorIdentityUnavailableException.GenericMessage
            );
        }
    }

    private async Task<Guid> ResolveAdministratorIdAsync(
        string username,
        CancellationToken cancellationToken
    ) =>
        (await _access.GetUserByUsernameAsync(username, cancellationToken))?.Id ?? Guid.Empty;
}
