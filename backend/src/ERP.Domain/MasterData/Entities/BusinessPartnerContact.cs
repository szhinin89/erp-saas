using ERP.Domain.Common;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Events;
using ERP.Domain.MasterData.ValueObjects;

namespace ERP.Domain.MasterData.Entities;

/// <summary>
/// Aggregate Root: persona de contacto de un BusinessPartner.
///
/// SCOPE: ITenantScopedEntity ÚNICAMENTE — eliminado CompanyId.
/// Los contactos son datos maestros del tercero compartidos entre todas las
/// Companies del tenant. Ver ADR-BP-02 (Fase 4).
///
/// LocationId: referencia opcional a una BusinessPartnerLocation del mismo BP.
/// Si la ubicación referenciada se desactiva, el handler de DeactivateLocation
/// debe verificar contactos activos antes de proceder (ver Problema 7, Fase 4).
///
/// IsPrimary: contacto principal del BP. Solo uno puede ser primario por
/// (tenantId, BusinessPartnerId). Garantizado por índice UNIQUE parcial en BD.
/// </summary>
public sealed class BusinessPartnerContact : AuditableEntity, ITenantScopedEntity
{
    public const int FirstNameMaxLen        = 100;
    public const int LastNameMaxLen         = 100;
    public const int PositionMaxLen         = 150;
    public const int NotesMaxLen            = 2000;
    public const int OtherDescriptionMaxLen = 100;

    public Guid        BusinessPartnerId { get; private set; }
    public Guid?       LocationId        { get; private set; }
    public string      FirstName         { get; private set; } = null!;
    public string?     LastName          { get; private set; }
    public string?     Position          { get; private set; }
    public ContactRole Role              { get; private set; }
    public string?     OtherDescription  { get; private set; }
    public ContactInfo Contact           { get; private set; } = null!;
    public string?     Notes             { get; private set; }
    public bool        IsPrimary         { get; private set; }
    public bool        IsActive          { get; private set; } = true;

    public string FullName => LastName is not null ? $"{FirstName} {LastName}" : FirstName;

    private BusinessPartnerContact() { }

    public static BusinessPartnerContact Create(
        Guid        tenantId,
        Guid        businessPartnerId,
        string      firstName,
        ContactRole role,
        Guid        createdBy,
        Guid?       locationId       = null,
        string?     lastName         = null,
        string?     position         = null,
        string?     phone            = null,
        string?     mobile           = null,
        string?     email            = null,
        string?     notes            = null,
        bool        isPrimary        = false,
        string?     otherDescription = null)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("tenantId es obligatorio.", nameof(tenantId));
        if (businessPartnerId == Guid.Empty)
            throw new ArgumentException("BusinessPartnerId es obligatorio.", nameof(businessPartnerId));

        var contact = new BusinessPartnerContact
        {
            Id                = Guid.NewGuid(),
            TenantId      = tenantId,
            BusinessPartnerId = businessPartnerId,
            LocationId        = locationId,
            FirstName         = NormalizeName(firstName, FirstNameMaxLen, nameof(firstName)),
            LastName          = NormalizeOptional(lastName, LastNameMaxLen, nameof(lastName)),
            Position          = NormalizeOptional(position, PositionMaxLen, nameof(position)),
            Role              = role,
            OtherDescription  = ValidateOtherDescription(role, otherDescription),
            Contact           = ContactInfo.Create(phone, mobile, email),
            Notes             = NormalizeOptional(notes, NotesMaxLen, nameof(notes)),
            IsPrimary         = isPrimary,
            IsActive          = true,
        };
        contact.SetCreated(createdBy);
        contact.RaiseDomainEvent(new BusinessPartnerContactCreatedEvent
        {
            TenantId      = tenantId,
            ContactId         = contact.Id,
            BusinessPartnerId = businessPartnerId,
            ContactRole       = role,
            CreatedBy         = createdBy,
        });
        return contact;
    }

    public void Update(
        string      firstName,
        ContactRole role,
        Guid        updatedBy,
        Guid?       locationId       = null,
        string?     lastName         = null,
        string?     position         = null,
        string?     phone            = null,
        string?     mobile           = null,
        string?     email            = null,
        string?     notes            = null,
        string?     otherDescription = null)
    {
        if (!IsActive)
            throw new InvalidOperationException("No se puede actualizar un contacto inactivo.");

        LocationId       = locationId;
        FirstName        = NormalizeName(firstName, FirstNameMaxLen, nameof(firstName));
        LastName         = NormalizeOptional(lastName, LastNameMaxLen, nameof(lastName));
        Position         = NormalizeOptional(position, PositionMaxLen, nameof(position));
        Role             = role;
        OtherDescription = ValidateOtherDescription(role, otherDescription);
        Contact          = ContactInfo.Create(phone, mobile, email);
        Notes            = NormalizeOptional(notes, NotesMaxLen, nameof(notes));
        SetUpdated(updatedBy);
        RaiseDomainEvent(new BusinessPartnerContactUpdatedEvent
        {
            TenantId = TenantId,
            ContactId    = Id,
            UpdatedBy    = updatedBy,
        });
    }

    /// <summary>
    /// Marca este contacto como primario.
    /// PRECONDICIÓN (en handler): llamar ClearPrimaryAsync antes de SetPrimary.
    /// </summary>
    public void SetPrimary(Guid updatedBy)
    {
        if (!IsActive)
            throw new InvalidOperationException("No se puede marcar como principal un contacto inactivo.");

        IsPrimary = true;
        SetUpdated(updatedBy);
        RaiseDomainEvent(new BusinessPartnerPrimaryContactChangedEvent
        {
            TenantId = TenantId,
            NewPrimaryContactId = Id,
            BusinessPartnerId   = BusinessPartnerId,
            ChangedBy           = updatedBy,
        });
    }

    internal void ClearPrimary(Guid updatedBy)
    {
        IsPrimary = false;
        SetUpdated(updatedBy);
    }

    public void Deactivate(Guid updatedBy)
    {
        if (!IsActive)
            throw new InvalidOperationException("El contacto ya está inactivo.");
        IsActive = false;
        SetUpdated(updatedBy);
        RaiseDomainEvent(new BusinessPartnerContactDeactivatedEvent
        {
            TenantId = TenantId,
            ContactId         = Id,
            BusinessPartnerId = BusinessPartnerId,
            DeactivatedBy     = updatedBy,
        });
    }

    public void Activate(Guid updatedBy)
    {
        if (IsActive)
            throw new InvalidOperationException("El contacto ya está activo.");
        IsActive = true;
        SetUpdated(updatedBy);
    }

    private static string NormalizeName(string value, int maxLen, string paramName)
    {
        var v = (value ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(v))
            throw new ArgumentException($"{paramName} es obligatorio.", paramName);
        if (v.Length > maxLen)
            throw new ArgumentException($"{paramName} no puede superar {maxLen} caracteres.", paramName);
        return v;
    }

    private static string? NormalizeOptional(string? value, int maxLen, string paramName)
    {
        var v = value?.Trim();
        if (string.IsNullOrEmpty(v)) return null;
        if (v.Length > maxLen)
            throw new ArgumentException($"{paramName} no puede superar {maxLen} caracteres.", paramName);
        return v;
    }

    private static string? ValidateOtherDescription(ContactRole role, string? description)
    {
        var d = description?.Trim();
        if (d is { Length: 0 }) d = null;

        if (role == ContactRole.Other && string.IsNullOrEmpty(d))
            throw new ArgumentException(
                "OtherDescription es obligatorio cuando ContactRole = Other.", nameof(description));

        if (d?.Length > OtherDescriptionMaxLen)
            throw new ArgumentException(
                $"OtherDescription no puede superar {OtherDescriptionMaxLen} caracteres.", nameof(description));

        return role == ContactRole.Other ? d : null;
    }
}
