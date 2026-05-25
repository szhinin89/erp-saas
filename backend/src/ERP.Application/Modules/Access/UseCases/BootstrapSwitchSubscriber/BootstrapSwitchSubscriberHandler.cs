using ERP.Application.Access.DTOs;
using ERP.Application.Admin;
using ERP.Application.Common;
using ERP.Application.Subscriptions;
using MediatR;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Subscribers.Interfaces;

namespace ERP.Application.Access.UseCases.BootstrapSwitchSubscriber;

/// <summary>
/// Paso 2 del login ERP: bootstrap token + subscriberId → session token.
/// Canónico: <c>POST /api/admin/iam/bootstrap-switch-subscriber</c>.
/// Impersonación platform: <see cref="ERP.Application.Auth.UseCases.SwitchSubscriber.SwitchSubscriberHandler"/>.
/// </summary>
public class BootstrapSwitchSubscriberHandler : IRequestHandler<BootstrapSwitchSubscriberCommand, Result<SessionResponseDto>>
{
    private readonly IAccessRepository _accessRepository;
    private readonly IAccessTokenService _tokenService;
    private readonly ICurrentUser _currentUser;
    private readonly ISubscriberRepository _subscriberRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly IConfigService _configService;
    private readonly ISessionModulesResolver _sessionModules;
    private readonly ICompanyProvisioningService _companyProvisioning;

    public BootstrapSwitchSubscriberHandler(
        IAccessRepository accessRepository,
        IAccessTokenService tokenService,
        ICurrentUser currentUser,
        ISubscriberRepository subscriberRepository,
        ICompanyRepository companyRepository,
        IConfigService configService,
        ISessionModulesResolver sessionModules,
        ICompanyProvisioningService companyProvisioning)
    {
        _accessRepository = accessRepository;
        _tokenService = tokenService;
        _currentUser = currentUser;
        _subscriberRepository = subscriberRepository;
        _companyRepository = companyRepository;
        _configService = configService;
        _sessionModules = sessionModules;
        _companyProvisioning = companyProvisioning;
    }

    public async Task<Result<SessionResponseDto>> Handle(BootstrapSwitchSubscriberCommand command, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated)
            return Result<SessionResponseDto>.Failure("Unauthorized");

        var userId = _currentUser.UserId;
        if (userId == Guid.Empty)
            return Result<SessionResponseDto>.Failure("Unauthorized");

        var user = await _accessRepository.GetUserByIdAsync(userId, ct);
        if (user is null || !user.IsActive)
            return Result<SessionResponseDto>.Failure("Unauthorized");

        var tenant = await _subscriberRepository.GetByIdAsync(command.SubscriberId, ct);
        if (tenant is null || !tenant.IsActive)
            return Result<SessionResponseDto>.Failure("No tienes acceso a este suscriptor.");

        var memberships = await _accessRepository.GetActiveCompanyUserMembershipsForUserSystemAsync(user.Id, ct);
        var companyIds = memberships.Select(m => m.CompanyId).Distinct().ToList();
        var companies = await _companyRepository.GetByIdsAsync(companyIds, ct);
        var companiesInSubscriber = companies.Where(c => c.SubscriberId == command.SubscriberId).ToList();

        if (companiesInSubscriber.Count == 0)
            return Result<SessionResponseDto>.Failure("No tienes acceso a empresas en este suscriptor.");

        var membershipByCompany = memberships.ToDictionary(m => m.CompanyId);

        if (companiesInSubscriber.Count > 1)
        {
            var first = companiesInSubscriber[0];
            var m0 = membershipByCompany[first.Id];
            var tokenPartial = _tokenService.GenerateSessionToken(user, command.SubscriberId, m0.Role);
            await _configService.WarmupTenantAsync(command.SubscriberId, ct);
            var modulesPartial = await _sessionModules.GetEnabledModuleKeysAsync(command.SubscriberId, ct);

            return Result<SessionResponseDto>.Success(new SessionResponseDto(
                UserId: user.Id,
                FullName: user.FullName,
                Email: user.Email.Value,
                SubscriberId: command.SubscriberId,
                Role: m0.Role,
                Token: tokenPartial,
                tenant.PlanCode,
                modulesPartial)
            {
                CompanyId = null,
            });
        }

        var company = companiesInSubscriber[0];
        if (!membershipByCompany.TryGetValue(company.Id, out var membership) || !membership.IsActive)
            return Result<SessionResponseDto>.Failure("Membresía no activa.");

        await _companyProvisioning.EnsureDefaultCompanyAsync(tenant, ct);

        var sessionToken = _tokenService.GenerateSessionToken(
            user, command.SubscriberId, membership.Role, company.Id);
        await _configService.WarmupTenantAsync(command.SubscriberId, ct);
        var modules = await _sessionModules.GetEnabledModuleKeysAsync(command.SubscriberId, ct);

        return Result<SessionResponseDto>.Success(new SessionResponseDto(
            UserId: user.Id,
            FullName: user.FullName,
            Email: user.Email.Value,
            SubscriberId: command.SubscriberId,
            Role: membership.Role,
            Token: sessionToken,
            tenant.PlanCode,
            modules)
        {
            CompanyId = company.Id,
        });
    }
}
