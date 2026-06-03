using ERP.Domain.Common;

namespace ERP.Domain.MasterData.Entities;

/// <summary>
/// Ubicación física de un BusinessPartner (cliente/proveedor) en el contexto de una empresa.
///
/// Company-scoped: cada empresa registra las ubicaciones relevantes para sus operaciones.
/// Ejemplos: Matriz, Sucursal Norte, Bodega de Entrega, Domicilio Fiscal, Punto de Facturación.
///
/// Una ubicación marcada como IsPrimary es la dirección principal para documentos (factura, guía remisión).
/// </summary>
public sealed class BusinessPartnerLocation : AuditableEntity, ISubscriberScopedEntity, ICompanyScopedEntity
{
    public const int NameMaxLen    = 120;
    public const int AddressMaxLen = 500;
    public const int PhoneMaxLen   = 40;
    public const int EmailMaxLen   = 120;
    public const int GeoCodeMaxLen = 10;  // código INEC (provincia/cantón/parroquia)

    public Guid   BusinessPartnerId { get; private set; }
    public Guid   CompanyId         { get; private set; }

    public string           Name         { get; private set; } = null!;
    public LocationType     Type         { get; private set; }
    public string           Address      { get; private set; } = null!;
    public string?          ProvinceCode { get; private set; }  // global.geo_provinces.id
    public string?          CantonCode   { get; private set; }  // global.geo_cantons.id
    public string?          ParishCode   { get; private set; }  // global.geo_parishes.id
    public string?          Phone        { get; private set; }
    public string?          Email        { get; private set; }
    public bool             IsPrimary    { get; private set; }
    public bool             IsActive     { get; private set; } = true;

    private BusinessPartnerLocation() { }

    public static BusinessPartnerLocation Create(
        Guid         subscriberId,
        Guid         companyId,
        Guid         businessPartnerId,
        string       name,
        LocationType type,
        string       address,
        Guid         createdBy,
        string?      provinceCode = null,
        string?      cantonCode   = null,
        string?      parishCode   = null,
        string?      phone        = null,
        string?      email        = null,
        bool         isPrimary    = false)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre de la ubicación es obligatorio.", nameof(name));
        if (string.IsNullOrWhiteSpace(address))
            throw new ArgumentException("La dirección es obligatoria.", nameof(address));
        if (businessPartnerId == Guid.Empty)
            throw new ArgumentException("BusinessPartnerId es obligatorio.", nameof(businessPartnerId));
        if (companyId == Guid.Empty)
            throw new ArgumentException("CompanyId es obligatorio.", nameof(companyId));

        var loc = new BusinessPartnerLocation
        {
            Id                = Guid.NewGuid(),
            SubscriberId      = subscriberId,
            CompanyId         = companyId,
            BusinessPartnerId = businessPartnerId,
            Name              = name.Trim()[..Math.Min(name.Trim().Length, NameMaxLen)],
            Type              = type,
            Address           = address.Trim()[..Math.Min(address.Trim().Length, AddressMaxLen)],
            ProvinceCode      = string.IsNullOrWhiteSpace(provinceCode) ? null : provinceCode.Trim(),
            CantonCode        = string.IsNullOrWhiteSpace(cantonCode)   ? null : cantonCode.Trim(),
            ParishCode        = string.IsNullOrWhiteSpace(parishCode)   ? null : parishCode.Trim(),
            Phone             = string.IsNullOrWhiteSpace(phone)        ? null : phone.Trim()[..Math.Min(phone.Trim().Length, PhoneMaxLen)],
            Email             = string.IsNullOrWhiteSpace(email)        ? null : email.Trim()[..Math.Min(email.Trim().Length, EmailMaxLen)],
            IsPrimary         = isPrimary,
            IsActive          = true,
        };
        loc.SetCreated(createdBy);
        return loc;
    }

    public void Update(
        string       name,
        LocationType type,
        string       address,
        Guid         updatedBy,
        string?      provinceCode = null,
        string?      cantonCode   = null,
        string?      parishCode   = null,
        string?      phone        = null,
        string?      email        = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre de la ubicación es obligatorio.", nameof(name));
        if (string.IsNullOrWhiteSpace(address))
            throw new ArgumentException("La dirección es obligatoria.", nameof(address));

        Name         = name.Trim()[..Math.Min(name.Trim().Length, NameMaxLen)];
        Type         = type;
        Address      = address.Trim()[..Math.Min(address.Trim().Length, AddressMaxLen)];
        ProvinceCode = string.IsNullOrWhiteSpace(provinceCode) ? null : provinceCode.Trim();
        CantonCode   = string.IsNullOrWhiteSpace(cantonCode)   ? null : cantonCode.Trim();
        ParishCode   = string.IsNullOrWhiteSpace(parishCode)   ? null : parishCode.Trim();
        Phone        = string.IsNullOrWhiteSpace(phone)        ? null : phone.Trim()[..Math.Min(phone.Trim().Length, PhoneMaxLen)];
        Email        = string.IsNullOrWhiteSpace(email)        ? null : email.Trim()[..Math.Min(email.Trim().Length, EmailMaxLen)];
        SetUpdated(updatedBy);
    }

    /// <summary>Marca como ubicación principal. El repositorio garantiza unicidad de IsPrimary por (company, bp).</summary>
    public void SetPrimary(Guid updatedBy)
    {
        IsPrimary = true;
        SetUpdated(updatedBy);
    }

    public void UnsetPrimary(Guid updatedBy)
    {
        IsPrimary = false;
        SetUpdated(updatedBy);
    }

    public void Deactivate(Guid updatedBy)
    {
        if (!IsActive) throw new InvalidOperationException("La ubicación ya está inactiva.");
        if (IsPrimary)
            throw new InvalidOperationException(
                "No se puede desactivar la ubicación principal. Asigne otra ubicación como principal primero.");
        IsActive = false;
        SetUpdated(updatedBy);
    }

    public void Activate(Guid updatedBy)
    {
        if (IsActive) throw new InvalidOperationException("La ubicación ya está activa.");
        IsActive = true;
        SetUpdated(updatedBy);
    }
}

/// <summary>Tipo de ubicación del BusinessPartner.</summary>
public enum LocationType
{
    Matrix        = 1,   // Domicilio principal / Matriz
    Branch        = 2,   // Sucursal
    Office        = 3,   // Oficina administrativa
    Warehouse     = 4,   // Bodega / Almacén
    DeliveryPoint = 5,   // Punto de entrega (solo recibe mercadería)
    BillingAddr   = 6,   // Dirección de facturación / fiscal
    Other         = 99,
}
