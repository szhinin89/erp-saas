using ERP.Domain.Common;
using ERP.Domain.Customers.ValueObjects;

namespace ERP.Domain.Customers.Entities;

/// <summary>Cliente maestro del tenant (persona natural o jurídica). Soft delete vía <see cref="MasterEntity"/>.</summary>
public sealed class Customer : MasterEntity, ITenantEntity
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
        var identification = CustomerIdentification.Create(identificationType, identificationNumber);
        var name = legalName.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("La razón social o nombre es obligatoria.", nameof(legalName));
        var validEmail = CustomerEmail.CreateOptional(email);

        var c = new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            IdentificationType = identification.Type,
            IdentificationNumber = identification.Number,
            LegalName = name,
            TradeName = NullIfWhiteSpace(tradeName),
            AddressLine = NullIfWhiteSpace(addressLine),
            Phone = NullIfWhiteSpace(phone),
            Email = validEmail?.Value,
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
        var identification = CustomerIdentification.Create(identificationType, identificationNumber);
        IdentificationType = identification.Type;
        IdentificationNumber = identification.Number;
        LegalName = legalName.Trim();
        if (string.IsNullOrWhiteSpace(LegalName))
            throw new ArgumentException("La razón social o nombre es obligatoria.", nameof(legalName));
        TradeName = NullIfWhiteSpace(tradeName);
        AddressLine = NullIfWhiteSpace(addressLine);
        Phone = NullIfWhiteSpace(phone);
        Email = CustomerEmail.CreateOptional(email)?.Value;
        Notes = NullIfWhiteSpace(notes);
        SetUpdated(updatedBy);
    }

    public static string NormalizeIdentificationType(string identificationType)
    {
        return CustomerIdentification.NormalizeType(identificationType);
    }

    public static string NormalizeIdentificationNumber(string identificationNumber)
    {
        return CustomerIdentification.NormalizeNumber(identificationNumber);
    }

    private static string? NullIfWhiteSpace(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var t = s.Trim();
        return t.Length == 0 ? null : t;
    }
}
