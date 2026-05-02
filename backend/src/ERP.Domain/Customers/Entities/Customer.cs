using ERP.Domain.Common;

namespace ERP.Domain.Customers.Entities;

/// <summary>Cliente maestro del tenant (persona natural o jurídica). Soft delete vía <see cref="MasterEntity"/>.</summary>
public sealed class Customer : MasterEntity
{
    public const int IdentificationTypeMaxLen = 20;
    public const int IdentificationNumberMaxLen = 32;
    public const int LegalNameMaxLen = 200;
    public const int TradeNameMaxLen = 200;
    public const int AddressLineMaxLen = 500;
    public const int PhoneMaxLen = 40;
    public const int EmailMaxLen = 120;
    public const int NotesMaxLen = 2000;

    /// <summary>RUC, CI, PASSPORT, OTHER (valores estables para API/UI).</summary>
    public string IdentificationType { get; private set; } = null!;
    public string IdentificationNumber { get; private set; } = null!;
    public string LegalName { get; private set; } = null!;
    public string? TradeName { get; private set; }
    public string? AddressLine { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public string? Notes { get; private set; }

    private Customer() { }

    public static Customer Create(
        Guid tenantId,
        string identificationType,
        string identificationNumber,
        string legalName,
        string? tradeName,
        string? addressLine,
        string? phone,
        string? email,
        string? notes,
        Guid createdBy)
    {
        var type = NormalizeIdentificationType(identificationType);
        var number = NormalizeIdentificationNumber(identificationNumber);
        var name = legalName.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("La razón social o nombre es obligatoria.", nameof(legalName));

        var c = new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            IdentificationType = type,
            IdentificationNumber = number,
            LegalName = name,
            TradeName = NullIfWhiteSpace(tradeName),
            AddressLine = NullIfWhiteSpace(addressLine),
            Phone = NullIfWhiteSpace(phone),
            Email = NullIfWhiteSpace(email),
            Notes = NullIfWhiteSpace(notes),
        };
        c.SetCreated(createdBy);
        return c;
    }

    public void Update(
        string identificationType,
        string identificationNumber,
        string legalName,
        string? tradeName,
        string? addressLine,
        string? phone,
        string? email,
        string? notes,
        Guid updatedBy)
    {
        IdentificationType = NormalizeIdentificationType(identificationType);
        IdentificationNumber = NormalizeIdentificationNumber(identificationNumber);
        LegalName = legalName.Trim();
        if (string.IsNullOrWhiteSpace(LegalName))
            throw new ArgumentException("La razón social o nombre es obligatoria.", nameof(legalName));
        TradeName = NullIfWhiteSpace(tradeName);
        AddressLine = NullIfWhiteSpace(addressLine);
        Phone = NullIfWhiteSpace(phone);
        Email = NullIfWhiteSpace(email);
        Notes = NullIfWhiteSpace(notes);
        SetUpdated(updatedBy);
    }

    public static string NormalizeIdentificationType(string identificationType)
    {
        var t = (identificationType ?? string.Empty).Trim().ToUpperInvariant();
        return t switch
        {
            "RUC" or "CI" or "PASSPORT" or "OTHER" => t,
            "CEDULA" or "CÉDULA" => "CI",
            "PASAPORTE" => "PASSPORT",
            _ => throw new ArgumentException("Tipo de identificación no válido (use RUC, CI, PASSPORT u OTHER).", nameof(identificationType)),
        };
    }

    public static string NormalizeIdentificationNumber(string identificationNumber)
    {
        var n = (identificationNumber ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(n))
            throw new ArgumentException("El número de identificación es obligatorio.", nameof(identificationNumber));
        if (n.Length > IdentificationNumberMaxLen)
            throw new ArgumentException($"El documento no puede superar {IdentificationNumberMaxLen} caracteres.", nameof(identificationNumber));
        return n;
    }

    private static string? NullIfWhiteSpace(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var t = s.Trim();
        return t.Length == 0 ? null : t;
    }
}
