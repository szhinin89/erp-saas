namespace ERP.Domain.Common;

/// <summary>
/// Único punto de normalización de números de documento para detectar duplicados entre saldos
/// iniciales y documentos existentes (CxC IL-5, CxP IL-6): mayúsculas invariantes y solo
/// letras/dígitos ("001-001-000000123" = "001001000000123").
/// </summary>
public static class DocumentNumberKey
{
    public static string Normalize(string? documentNumber) =>
        documentNumber is null
            ? string.Empty
            : new string(
                documentNumber.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray()
            );
}
