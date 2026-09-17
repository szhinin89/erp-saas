using ERP.Domain.Common;

namespace ERP.Domain.MasterData.Entities;

/// <summary>
/// BANK-CATALOG-01: catálogo maestro de bancos, seleccionable desde futuras cuentas
/// bancarias/destinos financieros en vez de texto libre. Tenant-wide (sin CompanyId, compartido
/// entre todas las Companies del tenant — mismo criterio que <see cref="PaymentTerm"/>), no
/// company-scoped. Unicidad por <see cref="CountryCode"/> + <see cref="Code"/> dentro del tenant,
/// no solo por Code, porque el catálogo está preparado para más de un país. Sin cuenta contable
/// ni relación con PaymentMethodAccount — eso pertenece a un módulo Tesorería/Bancos futuro, fuera
/// de este ticket.
/// </summary>
public sealed class Bank : MasterEntity, ITenantScopedEntity
{
    public const int MaxCountryCodeLength = 2;
    public const int MaxCodeLength = 30;
    public const int MaxNameLength = 150;
    public const int MaxShortNameLength = 50;
    public const string DefaultCountryCode = "EC";

    public string CountryCode { get; private set; } = null!;
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? ShortName { get; private set; }

    private Bank() { }

    public static Bank Create(
        Guid tenantId,
        string code,
        string name,
        string? shortName,
        Guid createdBy,
        string countryCode = DefaultCountryCode
    )
    {
        var (normalizedCountryCode, normalizedCode, normalizedName, normalizedShortName) =
            Validate(countryCode, code, name, shortName);

        var bank = new Bank
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CountryCode = normalizedCountryCode,
            Code = normalizedCode,
            Name = normalizedName,
            ShortName = normalizedShortName,
        };
        bank.SetCreated(createdBy);
        return bank;
    }

    /// <summary>Fábrica del seed mínimo Ecuador (BankCatalogSeeder) — idéntica a <see cref="Create"/> pero marca el registro como sembrado por el sistema. <see cref="Update"/> permanece abierto; Disable no está bloqueado (a diferencia de otros catálogos sembrados) porque el ticket pide activar/desactivar libremente, nunca borrado físico.</summary>
    public static Bank CreateSystemSeeded(
        Guid tenantId,
        string code,
        string name,
        string? shortName,
        Guid createdBy,
        string countryCode = DefaultCountryCode
    )
    {
        var bank = Create(tenantId, code, name, shortName, createdBy, countryCode);
        bank.MarkAsSystemSeeded();
        return bank;
    }

    public void Update(string name, string? shortName, Guid updatedBy)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre es obligatorio.", nameof(name));
        if (name.Trim().Length > MaxNameLength)
            throw new ArgumentException(
                $"El nombre no puede superar {MaxNameLength} caracteres.",
                nameof(name)
            );

        var trimmedShortName = string.IsNullOrWhiteSpace(shortName) ? null : shortName.Trim();
        if (trimmedShortName is { Length: > MaxShortNameLength })
            throw new ArgumentException(
                $"El nombre corto no puede superar {MaxShortNameLength} caracteres.",
                nameof(shortName)
            );

        Name = name.Trim();
        ShortName = trimmedShortName;
        SetUpdated(updatedBy);
    }

    private static (
        string CountryCode,
        string Code,
        string Name,
        string? ShortName
    ) Validate(string countryCode, string code, string name, string? shortName)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
            throw new ArgumentException("El país es obligatorio.", nameof(countryCode));
        var normalizedCountryCode = countryCode.Trim().ToUpperInvariant();
        if (normalizedCountryCode.Length != MaxCountryCodeLength)
            throw new ArgumentException(
                $"El código de país debe tener {MaxCountryCodeLength} caracteres (ISO-3166 alpha-2).",
                nameof(countryCode)
            );

        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("El código es obligatorio.", nameof(code));
        // BANK-CATALOG-01: código en mayúsculas y sin espacios (regla explícita del ticket) — no
        // solo trim, se elimina cualquier espacio interno para evitar variantes tipo "BANCO PICHINCHA".
        var normalizedCode = code.Trim().ToUpperInvariant().Replace(" ", string.Empty);
        if (normalizedCode.Length == 0)
            throw new ArgumentException("El código es obligatorio.", nameof(code));
        if (normalizedCode.Length > MaxCodeLength)
            throw new ArgumentException(
                $"El código no puede superar {MaxCodeLength} caracteres.",
                nameof(code)
            );

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre es obligatorio.", nameof(name));
        var normalizedName = name.Trim();
        if (normalizedName.Length > MaxNameLength)
            throw new ArgumentException(
                $"El nombre no puede superar {MaxNameLength} caracteres.",
                nameof(name)
            );

        var normalizedShortName = string.IsNullOrWhiteSpace(shortName) ? null : shortName.Trim();
        if (normalizedShortName is { Length: > MaxShortNameLength })
            throw new ArgumentException(
                $"El nombre corto no puede superar {MaxShortNameLength} caracteres.",
                nameof(shortName)
            );

        return (normalizedCountryCode, normalizedCode, normalizedName, normalizedShortName);
    }
}
