using ERP.Application.Access.DTOs;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Persistence;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using MediatR;

namespace ERP.Application.Access.UseCases.CreateAuthenticatedSession;

/// <summary>
/// Cierra las sesiones Active previas de la misma empresa y crea la nueva + su RefreshToken en
/// una única unidad de trabajo (un solo SaveChangesAsync, compartido por ambos repositorios
/// sobre el mismo ErpDbContext). No implementa advisory lock — la carrera de dos logins
/// verdaderamente simultáneos se resuelve dejando que el índice único parcial de BD decida
/// (ux_user_sessions_active_per_company) y traduciendo la violación a Result.Conflict con
/// IDatabaseExceptionTranslator (Fase 7), mismo patrón ya usado por CreateItemCommandHandler —
/// no se introduce un mecanismo de sincronización nuevo.
/// </summary>
public sealed class CreateAuthenticatedSessionHandler
    : IRequestHandler<CreateAuthenticatedSessionCommand, Result<AuthenticatedSessionDto>>
{
    private readonly IUserSessionRepository _userSessionRepository;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IDatabaseExceptionTranslator _dbEx;

    public CreateAuthenticatedSessionHandler(
        IUserSessionRepository userSessionRepository,
        IRefreshTokenService refreshTokenService,
        IDatabaseExceptionTranslator dbEx)
    {
        _userSessionRepository = userSessionRepository;
        _refreshTokenService = refreshTokenService;
        _dbEx = dbEx;
    }

    public async Task<Result<AuthenticatedSessionDto>> Handle(
        CreateAuthenticatedSessionCommand command, CancellationToken cancellationToken)
    {
        var existingActive = await _userSessionRepository.GetActiveSessionsAsync(
            command.IdentityUserId, command.TenantId, cancellationToken);

        // Regla de negocio: una sesión activa por EMPRESA, no por tenant — solo se cierran
        // las sesiones activas de la misma empresa que el login en curso.
        foreach (var previous in existingActive.Where(s => s.CompanyId == command.CompanyId))
        {
            previous.CloseByNewLogin(command.IdentityUserId);
            await _userSessionRepository.UpdateAsync(previous, cancellationToken);
        }

        // No persiste todavía — se combina con la creación de UserSession en un único
        // SaveChangesAsync (mismo ErpDbContext, mismo ChangeTracker).
        var (refreshToken, rawRefreshToken) = await _refreshTokenService.CreateWithoutSaveAsync(
            command.IdentityUserId, command.TenantId, command.CompanyId,
            RefreshUserType.Identity, cancellationToken);

        var session = UserSession.Create(
            command.TenantId, command.CompanyId, command.IdentityUserId,
            command.BranchId, command.TerminalId, refreshToken.Id);

        await _userSessionRepository.AddAsync(session, cancellationToken);

        // Única unidad de trabajo: cierre(s) de sesión previa + RefreshToken + nueva UserSession.
        try
        {
            await _userSessionRepository.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (_dbEx.TryGetUniqueViolation(ex, out _))
        {
            // Código por defecto (ApiResponseCodes.Common.Conflict) a propósito: LoginHandler/
            // SwitchCompanyHandler lo comparan explícitamente para decidir cómo propagar el
            // fallo — cambiarlo aquí sin ajustar ambos handlers rompería esa traducción a 409.
            return Result<AuthenticatedSessionDto>.Conflict(
                "Ya existe una sesión activa para este usuario en esta empresa. Intenta nuevamente.");
        }

        var dto = new AuthenticatedSessionDto(
            AccessSessionMapper.ToDto(session), rawRefreshToken, refreshToken.ExpiresAt);

        return Result<AuthenticatedSessionDto>.Success(dto);
    }
}
