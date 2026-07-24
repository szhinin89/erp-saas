using ERP.Domain.Common;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Events;
using ERP.Domain.MasterData.ValueObjects;

namespace ERP.Domain.MasterData.Entities;

/// <summary>
/// Aggregate Root: ubicación física de un BusinessPartner.
///
/// SCOPE: ITenantScopedEntity ÚNICAMENTE — eliminado CompanyId.
/// Las ubicaciones son datos maestros del tercero compartidos entre todas las
/// Companies del tenant. Ver ADR-BP-02 (Fase 4).
///
/// Una ubicación puede tener múltiples propósitos simultáneos via LocationPurpose flags.
/// Ejemplo: Matriz = Billing | Fiscal | Correspondence.
///
/// IsPrimary: dirección principal para documentos SRI. Solo una puede ser primaria
/// por (tenantId, BusinessPartnerId). Garantizado por índice UNIQUE parcial en BD.
/// </summary>
public sealed class BusinessPartnerLocation : AuditableEntity, ITenantScopedEntity
{
    public const int NameMaxLen             = 150;
    public const int OtherDescriptionMaxLen = 100;

    public Guid            BusinessPartnerId    { get; private set; }
    public string          Name                 { get; private set; } = null!;
    public LocationType    Type                 { get; private set; }
    public LocationPurpose Purpose              { get; private set; }
    public PhysicalAddress Address              { get; private set; } = null!;
    public string?         Phone                { get; private set; }
    public string?         Email                { get; private set; }
    public string?         OtherDescription     { get; private set; }
    public bool            IsPrimary            { get; private set; }
    public bool            IsActive             { get; private set; } = true;

    private BusinessPartnerLocation() { }

    public static BusinessPartnerLocation Create(
        Guid            tenantId,
        Guid            businessPartnerId,
        string          name,
        LocationType    type,
        LocationPurpose purpose,
        string          addressLine,
        Guid            createdBy,
        string?         provinceCode      = null,
        string?         cantonCode        = null,
        string?         parishCode        = null,
        string?         phone             = null,
        string?         email             = null,
        bool            isPrimary         = false,
        string?         otherDescription  = null)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("tenantId es obligatorio.", nameof(tenantId));
        if (businessPartnerId == Guid.Empty)
            throw new ArgumentException("BusinessPartnerId es obligatorio.", nameof(businessPartnerId));

        var loc = new BusinessPartnerLocation
        {
            Id                = Guid.NewGuid(),
            TenantId      = tenantId,
            BusinessPartnerId = businessPartnerId,
            Name              = NormalizeName(name),
            Type              = type,
            Purpose           = purpose,
            Address           = PhysicalAddress.Create(addressLine, provinceCode, cantonCode, parishCode),
            Phone             = NormalizeOptional(phone, 20, nameof(phone)),
            Email             = NormalizeEmail(email),
            OtherDescription  = ValidateOtherDescription(type, otherDescription),
            IsPrimary         = isPrimary,
            IsActive          = true,
        };
        loc.SetCreated(createdBy);
        loc.RaiseDomainEvent(new BusinessPartnerLocationCreatedEvent
        {
            TenantId      = tenantId,
            LocationId        = loc.Id,
            BusinessPartnerId = businessPartnerId,
            LocationType      = type,
            CreatedBy         = createdBy,
        });
        return loc;
    }

    public void Update(
        string          name,
        LocationType    type,
        LocationPurpose purpose,
        string          addressLine,
        Guid            updatedBy,
        string?         provinceCode     = null,
        string?         cantonCode       = null,
        string?         parishCode       = null,
        string?         phone            = null,
        string?         email            = null,
        string?         otherDescription = null)
    {
        if (!IsActive)
            throw new InvalidOperationException("No se puede actualizar una ubicación inactiva.");

        Name             = NormalizeName(name);
        Type             = type;
        Purpose          = purpose;
        Address          = PhysicalAddress.Create(addressLine, provinceCode, cantonCode, parishCode);
        Phone            = NormalizeOptional(phone, 20, nameof(phone));
        Email            = NormalizeEmail(email);
        OtherDescription = ValidateOtherDescription(type, otherDescription);
        SetUpdated(updatedBy);
        RaiseDomainEvent(new BusinessPartnerLocationUpdatedEvent
        {
            TenantId = TenantId,
            LocationId   = Id,
            UpdatedBy    = updatedBy,
        });
    }

    /// <summary>
    /// Marca esta ubicación como primaria.
    /// PRECONDICIÓN (en handler): llamar ClearPrimaryAsync antes de SetPrimary para
    /// mantener la invariante de unicidad. Ver ADR concurrencia (Fase 4).
    /// </summary>
    public void SetPrimary(Guid updatedBy)
    {
        if (!IsActive)
            throw new InvalidOperationException("No se puede marcar como principal una ubicación inactiva.");

        IsPrimary = true;
        SetUpdated(updatedBy);
        RaiseDomainEvent(new BusinessPartnerPrimaryLocationChangedEvent
        {
            TenantId = TenantId,
            NewPrimaryLocationId = Id,
            BusinessPartnerId    = BusinessPartnerId,
            ChangedBy            = updatedBy,
        });
    }

    internal void ClearPrimary(Guid updatedBy)
    {
        IsPrimary = false;
        SetUpdated(updatedBy);
    }

    /// <summary>
    /// Desactiva la ubicación (soft delete).
    /// INVARIANTE: no se puede desactivar la ubicación primaria.
    /// El handler debe verificar si hay contactos activos referenciando esta ubicación.
    /// </summary>
    public void Deactivate(Guid updatedBy)
    {
        if (!IsActive)
            throw new InvalidOperationException("La ubicación ya está inactiva.");
        if (IsPrimary)
            throw new InvalidOperationException(
                "No se puede desactivar la ubicación principal. Asigne otra ubicación como principal primero.");

        IsActive = false;
        SetUpdated(updatedBy);
        RaiseDomainEvent(new BusinessPartnerLocationDeactivatedEvent
        {
            TenantId = TenantId,
            LocationId        = Id,
            BusinessPartnerId = BusinessPartnerId,
            DeactivatedBy     = updatedBy,
        });
    }

    public void Activate(Guid updatedBy)
    {
        if (IsActive)
            throw new InvalidOperationException("La ubicación ya está activa.");
        IsActive = true;
        SetUpdated(updatedBy);
    }

    private static string NormalizeName(string name)
    {
        var n = (name ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(n))
            throw new ArgumentException("El nombre de la ubicación es obligatorio.", nameof(name));
        if (n.Length > NameMaxLen)
            throw new ArgumentException($"El nombre no puede superar {NameMaxLen} caracteres.", nameof(name));
        return n;
    }

    private static string? NormalizeOptional(string? value, int maxLen, string paramName)
    {
        var v = value?.Trim();
        if (string.IsNullOrEmpty(v)) return null;
        if (v.Length > maxLen)
            throw new ArgumentException($"{paramName} no puede superar {maxLen} caracteres.", paramName);
        return v;
    }

    private static string? NormalizeEmail(string? email)
    {
        var e = email?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(e)) return null;
        if (e.Length > 254)
            throw new ArgumentException("El email no puede superar 254 caracteres.", nameof(email));
        if (!e.Contains('@'))
            throw new ArgumentException("Formato de email inválido.", nameof(email));
        return e;
    }

    // Cuando LocationType = Other, OtherDescription es obligatorio. Ver Problema 8 (Fase 4).
    private static string? ValidateOtherDescription(LocationType type, string? description)
    {
        var d = description?.Trim();
        if (d is { Length: 0 }) d = null;

        if (type == LocationType.Other && string.IsNullOrEmpty(d))
            throw new ArgumentException(
                "OtherDescription es obligatorio cuando LocationType = Other.", nameof(description));

        if (d?.Length > OtherDescriptionMaxLen)
            throw new ArgumentException(
                $"OtherDescription no puede superar {OtherDescriptionMaxLen} caracteres.", nameof(description));

        return type == LocationType.Other ? d : null;
    }
}
