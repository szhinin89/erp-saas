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
        ICurrentCompany currentCompany)
    {
        _accessRepository = accessRepository;
        _passwordHasher = passwordHasher;
        _revocationService = revocationService;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _currentTenant = currentTenant;
        _currentCompany = currentCompany;
    }

    public async Task<Result<string>> Handle(AssignTemporaryPasswordAdminCommand command, CancellationToken cancellationToken)
    {
        var username = command.Username.Trim().ToLowerInvariant();
        var targetUser = await _accessRepository.GetUserByUsernameAsync(username, cancellationToken);
        if (targetUser is null)
            return Result<string>.NotFound("Usuario no encontrado.");

        // Anti-IDOR: el admin solo puede actuar sobre usuarios con membership activa en su
        // empresa activa — mismo criterio que LookupUserByUsernameAdminHandler. Un usuario sin
        // membership en esta empresa (de otro tenant, de otra empresa, o sin acceso) queda fuera
        // sin distinguir el motivo, para no filtrar información de existencia entre tenants.
        var membership = await _accessRepository.GetCompanyUserMembershipAsync(
            _currentCompany.CompanyId, targetUser.Id, cancellationToken);
        if (membership is null || !membership.IsActive)
            return Result<string>.Forbidden("El usuario no pertenece a la empresa activa.");

        var hash = _passwordHasher.HashPassword(command.TemporaryPassword);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            targetUser.SetPasswordHash(hash, updatedBy: _currentUser.UserId);
            targetUser.MarkRequirePasswordReset(updatedBy: _currentUser.UserId);
            await _accessRepository.SaveChangesAsync(cancellationToken);

            await _revocationService.RevokeAllAccessAsync(
                targetUser.Id, _currentTenant.TenantId, _currentUser.UserId,
                "Contraseña temporal asignada por administrador", cancellationToken);

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
