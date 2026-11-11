namespace ERP.Application.Common;

/// <summary>
/// Administrador inicial que <c>ICompanyProvisioningService.CreateManagedCompanyAsync</c> crea en
/// la misma transacción que la empresa (IAM-V1-F3). Lleva únicamente el hash de la contraseña
/// temporal: el texto plano solo existe en el handler que la generó y en su respuesta única.
/// </summary>
public sealed record InitialCompanyAdministrator(
    string Username,
    string FirstName,
    string LastName,
    string Email,
    string PasswordHash
);
