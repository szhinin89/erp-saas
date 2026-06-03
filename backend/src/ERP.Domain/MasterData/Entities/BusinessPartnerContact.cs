using ERP.Domain.Common;

namespace ERP.Domain.MasterData.Entities;

/// <summary>
/// Contacto de un BusinessPartner (cliente/proveedor) en el contexto de una empresa.
///
/// Company-scoped: cada empresa registra los contactos relevantes para sus operaciones.
/// Un contacto puede estar asociado a una ubicación específica (LocationId) o ser general (null).
///
/// Ejemplos: Gerente de Compras, Contador, Recepcionista, Responsable de Despacho.
/// </summary>
public sealed class BusinessPartnerContact : AuditableEntity, ISubscriberScopedEntity, ICompanyScopedEntity
{
    public const int FirstNameMaxLen = 100;
    public const int LastNameMaxLen  = 100;
    public const int PositionMaxLen  = 100;
    public const int PhoneMaxLen     = 40;
    public const int MobileMaxLen    = 40;
    public const int EmailMaxLen     = 120;
    public const int NotesMaxLen     = 500;

    public Guid  BusinessPartnerId { get; private set; }
    public Guid  CompanyId         { get; private set; }
    public Guid? LocationId        { get; private set; }  // null = contacto general del BP

    public string      FirstName  { get; private set; } = null!;
    public string?     LastName   { get; private set; }
    public string?     Position   { get; private set; }  // Cargo / Rol en la empresa del BP
    public ContactRole Role       { get; private set; }
    public string?     Phone      { get; private set; }
    public string?     Mobile     { get; private set; }
    public string?     Email      { get; private set; }
    public string?     Notes      { get; private set; }
    public bool        IsPrimary  { get; private set; }
    public bool        IsActive   { get; private set; } = true;

    private BusinessPartnerContact() { }

    public static BusinessPartnerContact Create(
        Guid        subscriberId,
        Guid        companyId,
        Guid        businessPartnerId,
        string      firstName,
        ContactRole role,
        Guid        createdBy,
        Guid?       locationId = null,
        string?     lastName   = null,
        string?     position   = null,
        string?     phone      = null,
        string?     mobile     = null,
        string?     email      = null,
        string?     notes      = null,
        bool        isPrimary  = false)
    {
        if (string.IsNullOrWhiteSpace(firstName))
            throw new ArgumentException("El nombre del contacto es obligatorio.", nameof(firstName));
        if (businessPartnerId == Guid.Empty)
            throw new ArgumentException("BusinessPartnerId es obligatorio.", nameof(businessPartnerId));
        if (companyId == Guid.Empty)
            throw new ArgumentException("CompanyId es obligatorio.", nameof(companyId));

        static string? Trim(string? s, int max)
            => string.IsNullOrWhiteSpace(s) ? null : s.Trim()[..Math.Min(s.Trim().Length, max)];

        var contact = new BusinessPartnerContact
        {
            Id                = Guid.NewGuid(),
            SubscriberId      = subscriberId,
            CompanyId         = companyId,
            BusinessPartnerId = businessPartnerId,
            LocationId        = locationId,
            FirstName         = firstName.Trim()[..Math.Min(firstName.Trim().Length, FirstNameMaxLen)],
            LastName          = Trim(lastName, LastNameMaxLen),
            Position          = Trim(position, PositionMaxLen),
            Role              = role,
            Phone             = Trim(phone,  PhoneMaxLen),
            Mobile            = Trim(mobile, MobileMaxLen),
            Email             = Trim(email,  EmailMaxLen),
            Notes             = Trim(notes,  NotesMaxLen),
            IsPrimary         = isPrimary,
            IsActive          = true,
        };
        contact.SetCreated(createdBy);
        return contact;
    }

    public void Update(
        string      firstName,
        ContactRole role,
        Guid        updatedBy,
        Guid?       locationId = null,
        string?     lastName   = null,
        string?     position   = null,
        string?     phone      = null,
        string?     mobile     = null,
        string?     email      = null,
        string?     notes      = null)
    {
        if (string.IsNullOrWhiteSpace(firstName))
            throw new ArgumentException("El nombre del contacto es obligatorio.", nameof(firstName));

        static string? Trim(string? s, int max)
            => string.IsNullOrWhiteSpace(s) ? null : s.Trim()[..Math.Min(s.Trim().Length, max)];

        LocationId = locationId;
        FirstName  = firstName.Trim()[..Math.Min(firstName.Trim().Length, FirstNameMaxLen)];
        LastName   = Trim(lastName, LastNameMaxLen);
        Position   = Trim(position, PositionMaxLen);
        Role       = role;
        Phone      = Trim(phone,  PhoneMaxLen);
        Mobile     = Trim(mobile, MobileMaxLen);
        Email      = Trim(email,  EmailMaxLen);
        Notes      = Trim(notes,  NotesMaxLen);
        SetUpdated(updatedBy);
    }

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
        if (!IsActive) throw new InvalidOperationException("El contacto ya está inactivo.");
        IsActive = false;
        SetUpdated(updatedBy);
    }

    public void Activate(Guid updatedBy)
    {
        if (IsActive) throw new InvalidOperationException("El contacto ya está activo.");
        IsActive = true;
        SetUpdated(updatedBy);
    }

    /// <summary>Nombre completo del contacto.</summary>
    public string FullName => string.IsNullOrWhiteSpace(LastName)
        ? FirstName
        : $"{FirstName} {LastName}";
}

/// <summary>Rol del contacto en la empresa del BusinessPartner.</summary>
public enum ContactRole
{
    Commercial   = 1,   // Ejecutivo de ventas / Responsable comercial
    Accounting   = 2,   // Contador / Responsable contable
    Management   = 3,   // Gerente / Director
    Reception    = 4,   // Recepción / Secretaria
    Dispatch     = 5,   // Responsable de despacho / bodega
    Billing      = 6,   // Responsable de facturación / cuentas por pagar
    Technical    = 7,   // Soporte técnico / Ingeniería
    Purchasing   = 8,   // Jefe de compras / Aprovisionamiento
    Legal        = 9,   // Asesor legal / Abogado
    Other        = 99,
}
