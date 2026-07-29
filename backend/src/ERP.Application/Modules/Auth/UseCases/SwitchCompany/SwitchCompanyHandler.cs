using ERP.Application.Access.UseCases.CreateAuthenticatedSession;
using ERP.Application.Auth.DTOs;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Branches.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Tenants.Interfaces;
using MediatR;

namespace ERP.Application.Auth.UseCases.SwitchCompany;

/// <summary>
/// SwitchCompany SÍ crea una sesión de sistema nueva (no solo cambia claims): ya emitía un
/// RefreshToken fresco vía IRefreshTokenService.CreateAsync antes de esta fase — cambiar de
/// empresa siempre significó "nueva sesión" en términos de refresh token, nunca fue una simple
/// reescritura de claims sobre el token existente. Por eso usa CreateAuthenticatedSessionCommand
/// igual que LoginHandler, con la misma resolución interina de sucursal principal.
/// </summary>
public sealed class SwitchCompanyHandler
    : IRequestHandler<SwitchCompanyCommand, Result<AuthResponseDto>>
{
    private const string UnresolvedTerminalId = "terminal-unresolved";

    private readonly IAccessRepository _accessRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly IAccessTokenService _tokenService;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentTenant _currentTenant;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly ITenantRepository _tenantRepository;
    private readonly IBranchRepository _branchRepository;
    private readonly IMediator _mediator;

    public SwitchCompanyHandler(
        IAccessRepository accessRepository,
        ICompanyRepository companyRepository,
        IAccessTokenService tokenService,
        ICurrentUser currentUser,
        ICurrentTenant currentTenant,
        IRefreshTokenService refreshTokenService,
        ITenantRepository TenantRepository,
        IBranchRepository branchRepository,
        IMediator mediator
    )
    {
        _accessRepository = accessRepository;
        _companyRepository = companyRepository;
        _tokenService = tokenService;
        _currentUser = currentUser;
        _currentTenant = currentTenant;
        _refreshTokenService = refreshTokenService;
        _tenantRepository = TenantRepository;
        _branchRepository = branchRepository;
        _mediator = mediator;
    }

    public async Task<Result<AuthResponseDto>> Handle(
        SwitchCompanyCommand command,
        CancellationToken cancellationToken
    )
    {
        if (!_currentUser.IsAuthenticated)
            return Result<AuthResponseDto>.Failure("No autenticado.");

        var tenantId = _currentTenant.TenantId;
        if (tenantId == Guid.Empty)
            return Result<AuthResponseDto>.Failure(
                "Contexto de tenant no establecido. Seleccione un suscriptor primero."
            );

        var company = await _companyRepository.GetByIdForTenantAsync(
            command.CompanyId,
            tenantId,
            cancellationToken
        );
        if (company is null)
            return Result<AuthResponseDto>.Failure(
                "Empresa no encontrada o no pertenece al tenant activo."
            );

        var user = await _accessRepository.GetUserByIdAsync(_currentUser.UserId, cancellationToken);
        if (user is null || !user.IsActive)
            return Result<AuthResponseDto>.Failure("Usuario no válido.");

        var membership = await _accessRepository.GetCompanyUserMembershipAsync(
            company.Id,
            user.Id,
            cancellationToken
        );
        if (membership is null || !membership.IsActive)
            return Result<AuthResponseDto>.Failure("No tiene acceso a esta empresa.");

        var token = _tokenService.GenerateSessionToken(user, tenantId, membership.Role);

        string refresh;
        DateTime refreshExpiry;

        var (branchId, preferencesFailure) = await CompanyUserPreferencesLoginResolver.ResolveAsync(
            _mediator,
            membership.Id,
            () => ResolveMainBranchIdAsync(tenantId, company.Id, cancellationToken),
            cancellationToken
        );
        if (preferencesFailure is not null)
            return preferencesFailure;

        if (branchId is Guid resolvedBranchId)
        {
            var sessionResult = await _mediator.Send(
                new CreateAuthenticatedSessionCommand(
                    tenantId,
                    company.Id,
                    user.Id,
                    resolvedBranchId,
                    command.TerminalId ?? UnresolvedTerminalId
                ),
                cancellationToken
            );

            if (!sessionResult.IsSuccess)
                return sessionResult.Code == ApiResponseCodes.Common.Conflict
                    ? Result<AuthResponseDto>.Conflict(
                        sessionResult.Error ?? "No se pudo cambiar de empresa. Intenta nuevamente."
                    )
                    : Result<AuthResponseDto>.Failure(
                        sessionResult.Error ?? "No se pudo cambiar de empresa."
                    );

            refresh = sessionResult.Value!.RefreshToken;
            refreshExpiry = sessionResult.Value.RefreshTokenExpiry;
        }
        else
        {
            // Misma limitación interina que LoginHandler: sin sucursal principal resoluble no
            // hay UserSession todavía — se preserva el comportamiento anterior exactamente.
            (refresh, refreshExpiry) = await _refreshTokenService.CreateAsync(
                user.Id,
                tenantId,
                company.Id,
                RefreshUserType.Identity,
                cancellationToken
            );
        }

        // Preservado del comportamiento original (el valor no se usa en la respuesta, igual
        // que antes de esta fase) — no se elimina para no alterar comportamiento no relacionado.
        var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken);
        _ = tenant;

        return Result<AuthResponseDto>.Success(
            new AuthResponseDto(
                user.Id,
                user.FullName,
                user.Username,
                user.Email?.Value,
                membership.Role,
                tenantId,
                token
            )
            {
                CompanyId = company.Id,
                RequiresCompanySelection = false,
                OnboardingCompleted = company.OnboardingCompleted,
                OperationalStatus = company.OperationalStatus,
                RefreshToken = refresh,
                RefreshTokenExpiry = refreshExpiry,
            }
        );
    }

    private async Task<Guid?> ResolveMainBranchIdAsync(
        Guid tenantId,
        Guid companyId,
        CancellationToken cancellationToken
    )
    {
        var branches = await _branchRepository.GetAsync(
            tenantId,
            activeFilter: true,
            search: null,
            cancellationToken
        );
        var mainBranches = branches.Where(b => b.CompanyId == companyId && b.IsMainBranch).ToList();
        return mainBranches.Count == 1 ? mainBranches[0].Id : null;
    }
}
