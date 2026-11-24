using System.Globalization;
using System.Text;

namespace ERP.Domain.Modules.Employees.Services;

/// <summary>
/// Única regla de normalización de nombres de los tres catálogos laborales (EMP-T-09, EMP-BR-16):
/// recorta, colapsa espacios internos y compara sin distinguir mayúsculas ni tildes. El resultado
/// alimenta <c>normalized_name</c> y su unicidad por empresa (UX-CAT-01).
/// </summary>
public static class EmployeeNameNormalizer
{
    public static string Normalize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var decomposed = name.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        var pendingSpace = false;

        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsWhiteSpace(ch))
            {
                pendingSpace = sb.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                sb.Append(' ');
                pendingSpace = false;
            }

            sb.Append(char.ToLowerInvariant(ch));
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
