using ERP.Application.Auth.DTOs;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Subscriptions;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Subscribers.Interfaces;
using MediatR;

namespace ERP.Application.Auth.UseCases.SwitchCompany;

public sealed class SwitchCompanyHandler : IRequestHandler<SwitchCompanyCommand, Result<AuthResponseDto>>
{
    private readonly IAccessRepository _accessRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly IAccessTokenService _tokenService;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentSubscriber _currentSubscriber;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly ISessionModulesResolver _sessionModules;
    private readonly ISubscriberRepository _subscriberRepository;

    public SwitchCompanyHandler(
        IAccessRepository accessRepository,
        ICompanyRepository companyRepository,
        IAccessTokenService tokenService,
        ICurrentUser currentUser,
        ICurrentSubscriber currentSubscriber,
        IRefreshTokenService refreshTokenService,
        ISessionModulesResolver sessionModules,
        ISubscriberRepository subscriberRepository)
    {
        _accessRepository = accessRepository;
        _companyRepository = companyRepository;
        _tokenService = tokenService;
        _currentUser = currentUser;
        _currentSubscriber = currentSubscriber;
        _refreshTokenService = refreshTokenService;
        _sessionModules = sessionModules;
        _subscriberRepository = subscriberRepository;
    }

    public async Task<Result<AuthResponseDto>> Handle(SwitchCompanyCommand command, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated)
            return Result<AuthResponseDto>.Failure("No autenticado.");

        var subscriberId = _currentSubscriber.SubscriberId;
        if (subscriberId == Guid.Empty)
            return Result<AuthResponseDto>.Failure("Contexto de suscriptor no establecido. Seleccione un suscriptor primero.");

        var company = await _companyRepository.GetByIdForSubscriberAsync(command.CompanyId, subscriberId, ct);
        if (company is null)
            return Result<AuthResponseDto>.Failure("Empresa no encontrada o no pertenece al suscriptor activo.");

        var user = await _accessRepository.GetUserByIdAsync(_currentUser.UserId, ct);
        if (user is null || !user.IsActive)
            return Result<AuthResponseDto>.Failure("Usuario no válido.");

        var membership = await _accessRepository.GetCompanyUserMembershipAsync(company.Id, user.Id, ct);
        if (membership is null || !membership.IsActive)
            return Result<AuthResponseDto>.Failure("No tiene acceso a esta empresa.");

        var token = _tokenService.GenerateSessionToken(
            user, subscriberId, membership.Role, company.Id);

        var (refresh, refreshExpiry) = await _refreshTokenService.CreateAsync(
            user.Id, subscriberId, company.Id, RefreshUserType.Identity, ct);

        var tenant = await _subscriberRepository.GetByIdAsync(subscriberId, ct);
        var modules = await _sessionModules.GetEnabledModuleKeysAsync(subscriberId, ct);

        return Result<AuthResponseDto>.Success(new AuthResponseDto(
            user.Id,
            user.FullName,
            user.Email.Value,
            membership.Role,
            subscriberId,
            token,
            tenant?.PlanCode,
            modules)
        {
            CompanyId = company.Id,
            RefreshToken = refresh,
            RefreshTokenExpiry = refreshExpiry,
        });
    }
}
