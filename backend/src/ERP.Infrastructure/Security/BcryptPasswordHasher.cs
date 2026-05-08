using ERP.Application.Common.Interfaces;

namespace ERP.Infrastructure.Security;

/// <summary>
/// Implementación de hashing de contraseñas usando BCrypt.Net-Next.
/// Adaptador: Infrastructure -> Puerto IPasswordHasher (Application)
/// </summary>
public class BcryptPasswordHasher : IPasswordHasher
{
    /// <summary>
    /// Hashea una contraseña usando BCrypt con factor de costo 12.
    /// </summary>
    public string HashPassword(string plainPassword)
    {
        if (string.IsNullOrWhiteSpace(plainPassword))
            throw new ArgumentException("Password cannot be null or empty.", nameof(plainPassword));

        return BCrypt.Net.BCrypt.HashPassword(plainPassword, workFactor: 12);
    }

    /// <summary>
    /// Verifica si una contraseña coincide con su hash BCrypt.
    /// </summary>
    public bool VerifyPassword(string plainPassword, string hash)
    {
        if (string.IsNullOrWhiteSpace(plainPassword))
            return false;

        if (string.IsNullOrWhiteSpace(hash))
            return false;

        try
        {
            return BCrypt.Net.BCrypt.Verify(plainPassword, hash);
        }
        catch (ArgumentException)
        {
            // Hash inválido
            return false;
        }
    }
}
