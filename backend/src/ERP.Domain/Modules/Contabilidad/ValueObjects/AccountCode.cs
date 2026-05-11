namespace ERP.Domain.Modules.Contabilidad.ValueObjects;

public sealed record AccountCode
{
    public string Value { get; }

    public AccountCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("El codigo de cuenta no puede estar vacio.");
        if (value.Length > 20)
            throw new ArgumentException("El codigo no puede superar 20 caracteres.");
        Value = value.Trim();
    }

    public override string ToString() => Value;
}
