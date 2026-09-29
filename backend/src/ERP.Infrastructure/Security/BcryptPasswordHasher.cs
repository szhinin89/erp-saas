using ERP.Application.Common.Interfaces;

namespace ERP.Infrastructure.Security;

/// <summary>
/// Implementación de hashing de contraseñas usando BCrypt.Net-Next.
/// Adaptador: Infrastructure -> Puerto IPasswordHasher (Application)
/// </summary>
public class BcryptPasswordHasher : IPasswordHasher
{
    /// <summary>Factor de costo de producción — único para hashes reales y para el hash ficticio.</summary>
    public const int WorkFactor = 12;

    /// <summary>
    /// Hash BCrypt ficticio y constante para <see cref="SimulatePasswordVerification"/>: mismo
    /// algoritmo y costo que un hash real (<c>$2a$12$</c>, <see cref="WorkFactor"/>), precomputado a
    /// partir de un secreto aleatorio descartado — ninguna contraseña coincide. No es un secreto, no
    /// representa a ningún usuario y no existe en la BD. Constante: ningún request genera un hash.
    /// </summary>
    public const string DummyVerificationHash =
        "$2a$12$w9tPX8lDpwWS6suQzOayC.cihkePy9JeLKtkEbiyjiG8nUUziKheO";

    /// <summary>
    /// Hashea una contraseña usando BCrypt con factor de costo 12.
    /// </summary>
    public string HashPassword(string plainPassword)
    {
        if (string.IsNullOrWhiteSpace(plainPassword))
            throw new ArgumentException("Password cannot be null or empty.", nameof(plainPassword));

        return BCrypt.Net.BCrypt.HashPassword(plainPassword, workFactor: WorkFactor);
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
        catch (Exception ex) when (ex is ArgumentException or BCrypt.Net.SaltParseException)
        {
            // Hash inválido o formato de salt no reconocido
            return false;
        }
    }

    /// <summary>
    /// Exactamente una <see cref="VerifyPassword"/> contra <see cref="DummyVerificationHash"/> (nunca
    /// HashPassword); el resultado se descarta (siempre es un fallo). Respeta el mismo corto circuito
    /// de contraseña vacía para que ambos caminos sigan siendo equivalentes.
    /// </summary>
    public void SimulatePasswordVerification(string plainPassword) =>
        _ = VerifyPassword(plainPassword, DummyVerificationHash);
}
