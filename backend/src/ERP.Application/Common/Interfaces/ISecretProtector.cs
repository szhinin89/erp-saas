namespace ERP.Application.Common.Interfaces;

/// <summary>
/// Cifrado reversible para secretos de configuración (p. ej. contraseña del certificado SRI).
/// Implementación: ASP.NET Core Data Protection en Infrastructure.
/// </summary>
public interface ISecretProtector
{
    string Protect(string plaintext);

    /// <summary>
    /// Descifra valores protegidos. Si el valor no está cifrado (legacy), lo devuelve tal cual.
    /// </summary>
    string UnprotectOrPlaintext(string storedValue);

    bool IsProtected(string storedValue);
}
