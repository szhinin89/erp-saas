namespace ERP.Application.Common;

/// <summary>
/// AdminGlobalCore: expone si la sesión operativa actual proviene de un admin global que entró a
/// operar una empresa (<c>POST /auth/global/operate-company</c>), leyendo los claims
/// <c>operator_mode</c>/<c>global_admin_user_id</c> del JWT. Interfaz separada de
/// <see cref="ICurrentUser"/> a propósito: agregar estos miembros a <see cref="ICurrentUser"/>
/// forzaría actualizar todos los fakes de test que la implementan en el backend.
/// </summary>
public interface ICurrentOperatorContext
{
    bool IsOperatorMode { get; }
    Guid? GlobalAdminUserId { get; }
}
