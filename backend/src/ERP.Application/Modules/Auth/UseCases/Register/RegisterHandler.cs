using MediatR;
using ERP.Application.Auth.DTOs;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Subscriptions;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Subscribers.Interfaces;

namespace ERP.Application.Auth.UseCases.Register;

public class RegisterHandler : IRequestHandler<RegisterCommand, Result<AuthResponseDto>>
{
    private readonly IAccessRepository _accessRepository;
    private readonly ISubscriberRepository _subscriberRepository;
    private readonly IAccessTokenService _accessTokenService;
    private readonly ICompanyProvisioningService _companyProvisioning;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ISessionModulesResolver _sessionModules;

    public RegisterHandler(
        IAccessRepository accessRepository,
        ISubscriberRepository subscriberRepository,
        IAccessTokenService accessTokenService,
        ICompanyProvisioningService companyProvisioning,
        IPasswordHasher passwordHasher,
        ISessionModulesResolver sessionModules)
    {
        _accessRepository = accessRepository;
        _subscriberRepository = subscriberRepository;
        _accessTokenService = accessTokenService;
        _companyProvisioning = companyProvisioning;
        _passwordHasher = passwordHasher;
        _sessionModules = sessionModules;
    }

    public async Task<Result<AuthResponseDto>> Handle(
        RegisterCommand command,
        CancellationToken ct)
    {
        if (string.Equals(command.Role, PlatformAuthConstants.JwtPlatformOperatorRole, StringComparison.Ordinal))
            return Result<AuthResponseDto>.Failure("Use el flujo de setup platform para operador platform.");

        var tenant = await _subscriberRepository.GetByIdAsync(command.SubscriberId, ct);
        if (tenant is null || !tenant.IsActive)
            return Result<AuthResponseDto>.Failure("El tenant no existe.");

        if (await _accessRepository.AnyUserWithEmailAsync(command.Email, ct))
            return Result<AuthResponseDto>.Failure("Ya existe un usuario con ese email.");

        var passwordHash = _passwordHasher.HashPassword(command.Password);
        var newId = Guid.NewGuid();
        var user = IdentityUser.CreateCompanyUser(
            command.FirstName,
            command.LastName,
            command.Email,
            passwordHash,
            createdBy: newId,
            subscriberId: command.SubscriberId);

        var company = await _companyProvisioning.EnsureDefaultCompanyAsync(tenant, ct);
        var membership = CompanyUserMembership.Create(
            company.Id,
            user.Id,
            command.Role,
            profileId: null,
            createdBy: newId);

        await _accessRepository.AddUserAsync(user, ct);
        await _accessRepository.AddCompanyUserMembershipAsync(membership, ct);
        await _accessRepository.SaveChangesAsync(ct);

        var token = _accessTokenService.GenerateSessionToken(
            user, command.SubscriberId, command.Role, company.Id);

        var modules = await _sessionModules.GetEnabledModuleKeysAsync(command.SubscriberId, ct);

        return Result<AuthResponseDto>.Success(new AuthResponseDto(
            user.Id,
            user.FullName,
            user.Email.Value,
            command.Role,
            command.SubscriberId,
            token,
            tenant.PlanCode,
            modules)
        {
            CompanyId           = company.Id,
            OnboardingCompleted = company.OnboardingCompleted,
            OperationalStatus   = company.OperationalStatus,
        });
    }
}
