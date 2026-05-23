using ERP.Application.Access.DTOs;
using ERP.Application.Common;
using ERP.Application.Navigation;
using ERP.Application.Subscriptions;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Exceptions;
using ERP.Domain.Subscribers.Interfaces;
using MediatR;

namespace ERP.Application.Access.UseCases.RegisterSubscriberWithAdmin;

public class RegisterSubscriberWithAdminHandler : IRequestHandler<RegisterSubscriberWithAdminCommand, Result<SessionResponseDto>>
{
    private readonly ISubscriberRepository _subscriberRepository;
    private readonly IAccessRepository _accessRepository;
    private readonly IAccessTokenService _tokenService;
    private readonly IDeploymentFeatureFlags _deployment;
    private readonly ISessionModulesResolver _sessionModules;
    private readonly ISubscriberProvisioningOrchestrator _provisioning;

    public RegisterSubscriberWithAdminHandler(
        ISubscriberRepository subscriberRepository,
        IAccessRepository accessRepository,
        IAccessTokenService tokenService,
        IDeploymentFeatureFlags deployment,
        ISessionModulesResolver sessionModules,
        ISubscriberProvisioningOrchestrator provisioning)
    {
        _subscriberRepository = subscriberRepository;
        _accessRepository = accessRepository;
        _tokenService = tokenService;
        _deployment = deployment;
        _sessionModules = sessionModules;
        _provisioning = provisioning;
    }

    public Task<Result<SessionResponseDto>> HandleAsync(RegisterSubscriberWithAdminCommand command, CancellationToken ct = default)
        => Handle(command, ct);

    public async Task<Result<SessionResponseDto>> Handle(RegisterSubscriberWithAdminCommand command, CancellationToken ct)
    {
        var slug = command.SubscriberSlug.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(slug))
            return Result<SessionResponseDto>.Failure("Slug inválido.");

        if (await _subscriberRepository.GetBySlugAsync(slug, ct) is not null)
            return Result<SessionResponseDto>.Failure("El slug ya está en uso.");

        var tenantQuota = await DeploymentQuota.GetBlockingReasonIfAtActiveSubscriberCapAsync(_deployment, _subscriberRepository, ct);
        if (tenantQuota is not null)
            return Result<SessionResponseDto>.Failure(tenantQuota);

        var email = command.AdminEmail.Trim().ToLowerInvariant();
        if (await _accessRepository.AnyUserWithEmailAsync(email, ct))
            return Result<SessionResponseDto>.Failure("El email ya está registrado en el sistema.");

        var userQuota = await DeploymentQuota.GetBlockingReasonIfAtIdentityUserCapAsync(_deployment, _accessRepository, ct);
        if (userQuota is not null)
            return Result<SessionResponseDto>.Failure(userQuota);

        try
        {
            var provisioned = await _provisioning.ProvisionNewSubscriberWithAdminAsync(
                new SubscriberProvisioningRequest(
                    SubscriberName: command.SubscriberName,
                    SubscriberSlug: slug,
                    AdminFirstName: command.AdminFirstName,
                    AdminLastName: command.AdminLastName,
                    AdminEmail: email,
                    AdminPassword: command.AdminPassword,
                    ActorId: Guid.Empty,
                    PasswordResetMode: command.PasswordResetMode,
                    Ruc: command.Ruc,
                    ShortName: command.ShortName,
                    TradeName: command.TradeName,
                    Dinardap: command.Dinardap,
                    LogoUrl: command.LogoUrl,
                    DisplayOrder: command.DisplayOrder,
                    Priority: command.Priority),
                ct);

            var membershipCap = await DeploymentQuota.GetBlockingReasonIfAtTenantCompanyUserMembershipUserCapAsync(
                _deployment, _accessRepository, provisioned.Subscriber.Id, ct);
            if (membershipCap is not null)
                return Result<SessionResponseDto>.Failure(membershipCap);

            var sessionToken = _tokenService.GenerateSessionToken(
                provisioned.AdminUser,
                provisioned.Subscriber.Id,
                "Admin",
                provisioned.DefaultCompany.Id);
            var modules = await _sessionModules.GetEnabledModuleKeysAsync(provisioned.Subscriber.Id, ct);
            return Result<SessionResponseDto>.Success(new SessionResponseDto(
                UserId: provisioned.AdminUser.Id,
                FullName: provisioned.AdminUser.FullName,
                Email: provisioned.AdminUser.Email.Value,
                SubscriberId: provisioned.Subscriber.Id,
                Role: "Admin",
                Token: sessionToken,
                provisioned.Subscriber.PlanCode,
                modules)
            {
                CompanyId = provisioned.DefaultCompany.Id,
            });
        }
        catch (CompanyRucAlreadyExistsException ex)
        {
            return Result<SessionResponseDto>.Failure(ex.Message, CompanyRucAlreadyExistsException.ErrorCode);
        }
        catch (ArgumentException ex)
        {
            return Result<SessionResponseDto>.Failure(ex.Message);
        }
    }
}
