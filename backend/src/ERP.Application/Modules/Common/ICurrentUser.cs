namespace ERP.Application.Common;

/// <summary>
/// Expone el usuario autenticado en el request en curso.
/// Retorna Guid.Empty si el request no está autenticado.
/// Se inyecta en handlers que necesitan registrar quién realizó la acción (auditoría).
/// </summary>
public interface ICurrentUser
{
    Guid UserId { get; }
    bool IsAuthenticated { get; }
    string? Username { get; }
    string? Email { get; }
    string? FullName { get; }
    string? Role { get; }
}
