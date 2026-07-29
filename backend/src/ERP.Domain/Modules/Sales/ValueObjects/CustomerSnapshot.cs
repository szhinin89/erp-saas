namespace ERP.Domain.Modules.Sales.ValueObjects;

public sealed record CustomerSnapshot
{
    public const int NameMaxLen = 200;
    public const int TaxIdMaxLen = 20;
    public const int IdTypeMaxLen = 5;
    public const int EmailMaxLen = 254;
    public const int AddressMaxLen = 300;

    public string Name { get; }
    public string TaxId { get; }
    public string IdentificationType { get; }
    public string? Email { get; }
    public string? Address { get; }

    private CustomerSnapshot(
        string name,
        string taxId,
        string identificationType,
        string? email,
        string? address
    )
    {
        Name = name;
        TaxId = taxId;
        IdentificationType = identificationType;
        Email = email;
        Address = address;
    }

    public static CustomerSnapshot Create(
        string name,
        string taxId,
        string identificationType,
        string? email = null,
        string? address = null
    )
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre del cliente es obligatorio.", nameof(name));
        if (string.IsNullOrWhiteSpace(taxId))
            throw new ArgumentException("El RUC/CI del cliente es obligatorio.", nameof(taxId));
        if (string.IsNullOrWhiteSpace(identificationType))
            throw new ArgumentException(
                "El tipo de identificación es obligatorio.",
                nameof(identificationType)
            );

        return new CustomerSnapshot(
            name.Trim(),
            taxId.Trim(),
            identificationType.Trim(),
            NullIfEmpty(email?.Trim(), EmailMaxLen, nameof(email)),
            NullIfEmpty(address?.Trim(), AddressMaxLen, nameof(address))
        );
    }

    private static string? NullIfEmpty(string? v, int max, string param)
    {
        if (string.IsNullOrEmpty(v))
            return null;
        if (v.Length > max)
            throw new ArgumentException($"{param} no puede superar {max} caracteres.", param);
        return v;
    }
}
