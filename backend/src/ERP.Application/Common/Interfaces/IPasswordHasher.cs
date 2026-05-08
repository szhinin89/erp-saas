namespace ERP.Application.Common.Interfaces;

/// <summary>
/// Puerto de abstracción para hashing de contraseñas.
/// Implementación: Infrastructure.Security.BCryptPasswordHasher
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Hashea una contraseña en texto plano.
    /// </summary>
    /// <param name="plainPassword">Contraseña en texto plano.</param>
    /// <returns>Hash de la contraseña.</returns>
    string HashPassword(string plainPassword);

    /// <summary>
    /// Verifica si una contraseña coincide con su hash.
    /// </summary>
    /// <param name="plainPassword">Contraseña en texto plano.</param>
    /// <param name="hash">Hash almacenado.</param>
    /// <returns>true si la contraseña es válida; false en caso contrario.</returns>
    bool VerifyPassword(string plainPassword, string hash);
}
