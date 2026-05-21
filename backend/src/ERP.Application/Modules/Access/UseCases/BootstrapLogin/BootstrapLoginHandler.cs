using ERP.Application.Access.DTOs;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Subscribers.Interfaces;
using MediatR;

namespace ERP.Application.Access.UseCases.BootstrapLogin;

public class BootstrapLoginHandler : IRequestHandler<BootstrapLoginCommand, Result<BootstrapLoginResponseDto>>
{
    private readonly IAccessRepository _accessRepository;
    private readonly ISubscriberRepository _tenantRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly IAccessTokenService _tokenService;
    private readonly IPasswordHasher _passwordHasher;

    public BootstrapLoginHandler(
        IAccessRepository accessRepository,
        ISubscriberRepository tenantRepository,
        ICompanyRepository companyRepository,
        IAccessTokenService tokenService,
        IPasswordHasher passwordHasher)
    {
        _accessRepository = accessRepository;
        _tenantRepository = tenantRepository;
        _companyRepository = companyRepository;
        _tokenService = tokenService;
        _passwordHasher = passwordHasher;
    }

    public Task<Result<BootstrapLoginResponseDto>> HandleAsync(BootstrapLoginCommand command, CancellationToken ct = default)
        => Handle(command, ct);

    public async Task<Result<BootstrapLoginResponseDto>> Handle(BootstrapLoginCommand command, CancellationToken ct)
    {
        var email = command.Email.Trim();
        var user = await _accessRepository.GetUserByEmailAsync(email, ct);
        if (user is null)
            return Result<BootstrapLoginResponseDto>.Failure("Credenciales inválidas. Si olvidaste tus datos, comunícate con el administrador.");

        if (!user.IsActive)
            return Result<BootstrapLoginResponseDto>.Failure("Credenciales inválidas. Si olvidaste tus datos, comunícate con el administrador.");

        var valid = _passwordHasher.VerifyPassword(command.Password, user.PasswordHash);
        if (!valid)
            return Result<BootstrapLoginResponseDto>.Failure("Credenciales inválidas. Si olvidaste tus datos, comunícate con el administrador.");

        if (user.IsPlatformSuperAdmin)
        {
            var subscribersAll = await _tenantRepository.GetAllAsync(ct);
            var activeTenants = subscribersAll.Where(t => t.IsActive).ToList();
            var superSubscriberIds = activeTenants.Select(t => t.Id).ToList();

            var superBootstrapToken = _tokenService.GenerateBootstrapToken(user, superSubscriberIds);
            var superAccessible = activeTenants
                .OrderBy(t => t.Name)
                .Select(t => new AccessibleSubscriberDto(t.Id, t.Name, t.Slug, "SuperAdmin"))
                .ToList();

            return Result<BootstrapLoginResponseDto>.Success(new BootstrapLoginResponseDto(
                UserId: user.Id,
                FullName: user.FullName,
                Email: user.Email.Value,
                BootstrapToken: superBootstrapToken,
                Subscribers: superAccessible));
        }

        var memberships = await _accessRepository.GetActiveCompanyUserMembershipsForUserSystemAsync(user.Id, ct);
        if (memberships.Count == 0)
            return Result<BootstrapLoginResponseDto>.Failure("No estás registrado a una empresa. Comunícate con el administrador.");

        var companyIds = memberships.Select(m => m.CompanyId).Distinct().ToList();
        var companies = await _companyRepository.GetByIdsAsync(companyIds, ct);
        var membershipByCompany = memberships.ToDictionary(m => m.CompanyId);
        var subscriberIds = companies.Select(c => c.SubscriberId).Distinct().ToList();
        var subscribers = await _tenantRepository.GetAllAsync(ct);
        var subscriberById = subscribers.ToDictionary(t => t.Id);

        var membershipAccessible = companies
            .Where(c => subscriberById.TryGetValue(c.SubscriberId, out var t) && t.IsActive)
            .GroupBy(c => c.SubscriberId)
            .Select(g =>
            {
                var t = subscriberById[g.Key];
                var company = g.First();
                var m = membershipByCompany[company.Id];
                return new AccessibleSubscriberDto(t.Id, t.Name, t.Slug, m.Role);
            })
            .OrderBy(x => x.Name)
            .ToList();

        var membershipBootstrapToken = _tokenService.GenerateBootstrapToken(user, subscriberIds);

        return Result<BootstrapLoginResponseDto>.Success(new BootstrapLoginResponseDto(
            UserId: user.Id,
            FullName: user.FullName,
            Email: user.Email.Value,
            BootstrapToken: membershipBootstrapToken,
            Subscribers: membershipAccessible));
    }
}
