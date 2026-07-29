namespace ERP.Domain.Modules.Ride.ValueObjects;

/// <summary>Campo adicional libre del comprobante (<c>campoAdicional</c> en el XML autorizado).</summary>
public sealed record RideAdditionalInfo
{
    public string Name { get; }
    public string Value { get; }

    private RideAdditionalInfo(string name, string value)
    {
        Name = name;
        Value = value;
    }

    public static RideAdditionalInfo Create(string name, string value)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "El nombre del campo adicional es obligatorio.",
                nameof(name)
            );
        ArgumentNullException.ThrowIfNull(value);

        return new RideAdditionalInfo(name.Trim(), value);
    }
}
