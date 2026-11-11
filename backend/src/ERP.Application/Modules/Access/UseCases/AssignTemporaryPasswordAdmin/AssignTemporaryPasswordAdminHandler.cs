using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Interfaces;
using MediatR;

namespace ERP.Application.Access.UseCases.AssignTemporaryPasswordAdmin;

/// <summary>
/// Compone únicamente piezas de dominio ya existentes y ya probadas — no reimplementa hashing,
/// revocación de tokens ni cierre de sesiones:
/// IdentityUser.SetPasswordHash + IdentityUser.MarkRequirePasswordReset (dominio, Fase de
/// refactor previa) + IUserAccessRevocationService (nuevo, compone IRefreshTokenService +
/// IUserSessionRepository, ver comentario en la interfaz).
///
/// Scoping de empresa: IdentityUser es una entidad global sin filtro automático de
/// ICompanyOperationalEntity, así que el anti-IDOR se valida explícitamente contra
/// IAccessRepository.GetCompanyUserMembershipAsync — mismo patrón que LookupUserByUsernameAdminHandler.
/// </summary>
public sealed class AssignTemporaryPasswordAdminHandler
    : IRequestHandler<AssignTemporaryPasswordAdminCommand, Result<string>>
{
    private readonly IAccessRepository _accessRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUserAccessRevocationService _revocationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;

    public AssignTemporaryPasswordAdminHandler(
        IAccessRepository accessRepository,
        IPasswordHasher passwordHasher,
        IUserAccessRevocationService revocationService,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        ICurrentTenant currentTenant,
        ICurrentCompany currentCompany
    )
    {
        _accessRepository = accessRepository;
        _passwordHasher = passwordHasher;
        _revocationService = revocationService;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _currentTenant = currentTenant;
        _currentCompany = currentCompany;
    }

    public async Task<Result<string>> Handle(
        AssignTemporaryPasswordAdminCommand command,
        CancellationToken cancellationToken
    )
    {
        var username = command.Username.Trim().ToLowerInvariant();
        var targetUser = await _accessRepository.GetUserByUsernameAsync(
            username,
            cancellationToken
        );
        // Anti-IDOR / IAM-V1-F1: solo usuarios con membership activa en la empresa activa y sin
        // GlobalUserRole activo. Inexistente, global, de otro tenant/empresa o sin acceso responden
        // exactamente igual (404 uniforme), para no filtrar existencia de cuentas.
        var membership = targetUser is null
            ? null
            : await IamIdentityScope.GetAdministrableMembershipAsync(
                _accessRepository,
                _currentCompany.CompanyId,
                targetUser.Id,
                cancellationToken
            );
        if (targetUser is null || membership is null || !membership.IsActive)
            return Result<string>.NotFound(IamIdentityScope.UserNotFoundMessage);

        var hash = _passwordHasher.HashPassword(command.TemporaryPassword);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            targetUser.SetPasswordHash(hash, updatedBy: _currentUser.UserId);
            targetUser.MarkRequirePasswordReset(updatedBy: _currentUser.UserId);
            await _accessRepository.SaveChangesAsync(cancellationToken);

            await _revocationService.RevokeAllAccessAsync(
                targetUser.Id,
                _currentTenant.TenantId,
                _currentUser.UserId,
                "Contraseña temporal asignada por administrador",
                cancellationToken
            );

            await _unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<string>.Success("Contraseña temporal asignada correctamente.");
    }
}
