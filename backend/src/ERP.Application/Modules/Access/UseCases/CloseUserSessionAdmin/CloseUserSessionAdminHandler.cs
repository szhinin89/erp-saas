using ERP.Application.Common;
using ERP.Domain.Access.Interfaces;
using MediatR;

namespace ERP.Application.Access.UseCases.CloseUserSessionAdmin;

/// <summary>
/// Decisión de diseño (Fase 10): reutiliza UserSession.CloseManually — no se agrega un cuarto
/// método/estado de dominio "cerrada por admin". El Status resultante (ClosedManually) sigue
/// siendo semánticamente correcto: es un cierre humano explícito, no automático (a diferencia
/// de ClosedByNewLogin/Expired) — la distinción de QUIÉN lo ejecutó ya queda registrada
/// correctamente en UpdatedBy (el Guid del administrador, no el del dueño de la sesión), sin
/// necesidad de una transición nueva en Domain.
///
/// NO revoca el RefreshToken asociado — no existe flujo aprobado de revocación por Id.
/// </summary>
public sealed class CloseUserSessionAdminHandler
    : IRequestHandler<CloseUserSessionAdminCommand, Result<string>>
{
    private readonly IUserSessionRepository _repository;
    private readonly ICurrentUser _currentUser;

    public CloseUserSessionAdminHandler(IUserSessionRepository repository, ICurrentUser currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<Result<string>> Handle(
        CloseUserSessionAdminCommand command,
        CancellationToken cancellationToken
    )
    {
        // Sin IgnoreQueryFilters: el filtro automático de ICompanyOperationalEntity ya
        // garantiza que un admin solo alcance sesiones de su propia empresa activa — una
        // sesión de otra empresa simplemente no existe desde este query, nunca es un 403
        // silencioso ni una fuga de datos.
        var session = await _repository.GetByIdAsync(command.SessionId, cancellationToken);
        if (session is null)
            return Result<string>.NotFound("Sesión no encontrada.");

        session.CloseManually(_currentUser.UserId);
        await _repository.UpdateAsync(session, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return Result<string>.Success("Sesión cerrada correctamente.");
    }
}
