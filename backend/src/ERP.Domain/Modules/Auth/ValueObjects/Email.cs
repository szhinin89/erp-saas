namespace ERP.Domain.Auth.ValueObjects;

public sealed record Email
{
    public string Value { get; }

    public Email(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("El email no puede estar vacio.");
        if (!value.Contains('@'))
            throw new ArgumentException("El email no es valido.");
        Value = value.Trim().ToLowerInvariant();
    }

    public override string ToString() => Value;
}
