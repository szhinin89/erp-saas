using System.Net.Mail;

namespace ERP.Domain.Modules.Sales.ValueObjects;

/// <summary>Correo electrónico de cliente opcional con validación de formato.</summary>
public sealed record CustomerEmail
{
    public const int MaxLen = 120;

    public string Value { get; }

    private CustomerEmail(string value)
    {
        Value = value;
    }

    public static CustomerEmail? CreateOptional(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        var normalized = email.Trim();
        if (normalized.Length > MaxLen)
            throw new ArgumentException($"El correo no puede superar {MaxLen} caracteres.", nameof(email));

        try
        {
            _ = new MailAddress(normalized);
        }
        catch (FormatException)
        {
            throw new ArgumentException("El formato del correo no es válido.", nameof(email));
        }

        return new CustomerEmail(normalized);
    }
}
