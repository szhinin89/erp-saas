namespace ERP.Application.Modules.Ventas;

internal static class CustomerInputValidation
{
    public static string? ValidateOptionalEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        var t = email.Trim();
        if (t.Length > 120)
            return "El correo no puede superar 120 caracteres.";

        if (t.IndexOf('@', StringComparison.Ordinal) < 1 || !t.Contains('.', StringComparison.Ordinal))
            return "El formato del correo no es válido.";

        return null;
    }
}
