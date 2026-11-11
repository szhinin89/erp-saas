namespace ERP.Domain.Exceptions;

/// <summary>
/// El usuario o el correo del administrador inicial de una empresa ya existen (índices únicos
/// <c>ux_identity_users_*</c>). Mensaje deliberadamente genérico: no indica cuál de los dos ni
/// dónde (IAM-V1-F1, D5).
/// </summary>
public sealed class InitialAdministratorIdentityUnavailableException : Exception
{
    public const string GenericMessage = "El nombre de usuario o el correo no están disponibles.";

    public InitialAdministratorIdentityUnavailableException()
        : base(GenericMessage) { }
}
