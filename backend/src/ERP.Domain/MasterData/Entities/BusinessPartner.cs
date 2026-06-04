using ERP.Domain.Common;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Events;
using ERP.Domain.MasterData.ValueObjects;

namespace ERP.Domain.MasterData.Entities;

/// <summary>
/// Aggregate Root: identidad fiscal de un tercero dentro del tenant.
///
/// SCOPE: ISubscriberScopedEntity — compartido entre todas las Companies del subscriber.
///
/// CONTIENE: TaxIdentification, PersonName, PersonType, CountryCode, IsActive.
/// NO CONTIENE: Email, Phone, LegalRepresentativeName (→ BusinessPartnerContact),
///              roles (→ BusinessPartnerRole AR independiente),
///              condiciones comerciales (→ CompanyBpTradingSettings).
///
/// EXTENSIÓN DE ROLES: ver BusinessPartnerRole. PROHIBIDO agregar IsCustomer/IsSupplier aquí.
/// </summary>
public sealed class BusinessPartner : AuditableEntity, ISubscriberScopedEntity
{
    public const int CountryCodeLen = 2;

    public TaxIdentification Identification { get; private set; } = null!;
    public PersonName        Name           { get; private set; } = null!;
    public PersonType        PersonType     { get; private set; }
    public string?           CountryCode    { get; private set; }
    public bool              IsActive       { get; private set; } = true;

    private BusinessPartner() { }

    public static BusinessPartner Create(
        Guid       subscriberId,
        string     identificationType,
        string     identificationNumber,
        PersonType personType,
        string     legalName,
        Guid       createdBy,
        string?    tradeName   = null,
        string?    countryCode = null)
    {
        if (subscriberId == Guid.Empty)
            throw new ArgumentException("SubscriberId es obligatorio.", nameof(subscriberId));
        if (createdBy == Guid.Empty)
            throw new ArgumentException("CreatedBy es obligatorio.", nameof(createdBy));

        var bp = new BusinessPartner
        {
            Id             = Guid.NewGuid(),
            SubscriberId   = subscriberId,
            Identification = TaxIdentification.Create(identificationType, identificationNumber),
            Name           = PersonName.Create(legalName, tradeName),
            PersonType     = personType,
            CountryCode    = NormalizeCountryCode(countryCode),
            IsActive       = true,
        };
        bp.SetCreated(createdBy);
        bp.RaiseDomainEvent(new BusinessPartnerCreatedEvent
        {
            SubscriberId           = subscriberId,
            BusinessPartnerId      = bp.Id,
            IdentificationType     = bp.Identification.Type,
            IdentificationNumber   = bp.Identification.Number,
            LegalName              = bp.Name.LegalName,
            CreatedBy              = createdBy,
        });
        return bp;
    }

    /// <summary>
    /// Actualiza nombre legal/comercial, tipo de persona y país.
    /// No modifica la identificación fiscal — use UpdateIdentification() para eso.
    /// </summary>
    public void UpdateProfile(
        string     legalName,
        PersonType personType,
        Guid       updatedBy,
        string?    tradeName   = null,
        string?    countryCode = null)
    {
        if (!IsActive)
            throw new InvalidOperationException("No se puede actualizar un BusinessPartner inactivo.");

        Name        = PersonName.Create(legalName, tradeName);
        PersonType  = personType;
        CountryCode = NormalizeCountryCode(countryCode);
        SetUpdated(updatedBy);
        RaiseDomainEvent(new BusinessPartnerProfileUpdatedEvent
        {
            SubscriberId      = SubscriberId,
            BusinessPartnerId = Id,
            LegalName         = Name.LegalName,
            UpdatedBy         = updatedBy,
        });
    }

    /// <summary>
    /// Cambia la identificación fiscal. Operación de alto impacto: emite evento de auditoría.
    /// ATENCIÓN: cuando el módulo de documentos exista, verificar que no haya documentos
    /// en estados no-finales antes de permitir este cambio. Ver ADR-BP-14.
    /// </summary>
    public void UpdateIdentification(string type, string number, Guid updatedBy)
    {
        if (!IsActive)
            throw new InvalidOperationException("No se puede modificar la identificación de un BusinessPartner inactivo.");

        var oldType   = Identification.Type;
        var oldNumber = Identification.Number;

        Identification = TaxIdentification.Create(type, number);
        SetUpdated(updatedBy);
        RaiseDomainEvent(new BusinessPartnerIdentificationChangedEvent
        {
            SubscriberId       = SubscriberId,
            BusinessPartnerId  = Id,
            OldType            = oldType,
            OldNumber          = oldNumber,
            NewType            = Identification.Type,
            NewNumber          = Identification.Number,
            ChangedBy          = updatedBy,
        });
    }

    public void Deactivate(Guid updatedBy)
    {
        if (!IsActive)
            throw new InvalidOperationException("El BusinessPartner ya está inactivo.");
        IsActive = false;
        SetUpdated(updatedBy);
        RaiseDomainEvent(new BusinessPartnerDeactivatedEvent
        {
            SubscriberId      = SubscriberId,
            BusinessPartnerId = Id,
            DeactivatedBy     = updatedBy,
        });
    }

    public void Activate(Guid updatedBy)
    {
        if (IsActive)
            throw new InvalidOperationException("El BusinessPartner ya está activo.");
        IsActive = true;
        SetUpdated(updatedBy);
        RaiseDomainEvent(new BusinessPartnerActivatedEvent
        {
            SubscriberId      = SubscriberId,
            BusinessPartnerId = Id,
            ActivatedBy       = updatedBy,
        });
    }

    private static string? NormalizeCountryCode(string? code)
    {
        var c = code?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(c)) return null;
        if (c.Length != CountryCodeLen)
            throw new ArgumentException($"CountryCode debe ser un código ISO 3166-1 alpha-2 de {CountryCodeLen} caracteres.", nameof(code));
        return c;
    }
}
