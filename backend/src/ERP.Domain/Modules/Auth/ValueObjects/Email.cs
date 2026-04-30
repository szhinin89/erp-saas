using System.Text.RegularExpressions;

namespace ERP.Domain.Auth.ValueObjects;

public sealed record Email
{
    private static readonly Regex _pattern =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string Value { get; }

    public Email(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("El email no puede estar vacío.");

        var trimmed = value.Trim();

        if (!_pattern.IsMatch(trimmed))
            throw new ArgumentException($"El email '{trimmed}' no tiene un formato válido.");

        Value = trimmed.ToLowerInvariant();
    }

    public override string ToString() => Value;
}
