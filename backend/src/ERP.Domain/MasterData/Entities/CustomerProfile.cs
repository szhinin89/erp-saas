using ERP.Domain.Common;

namespace ERP.Domain.MasterData.Entities;

/// <summary>
/// Rol "cliente" de un BusinessPartner dentro de un Subscriber.
///
/// SCOPE: ISubscriberScopedEntity.
/// El perfil de cliente es COMPARTIDO entre todas las Companies del Subscriber.
/// Las condiciones comerciales por empresa (CreditLimit, PaymentDays) van en
/// CompanyBusinessPartnerSettings.
///
/// Relación 1:1 con BusinessPartner. Un BP puede no tener CustomerProfile
/// (si solo es proveedor), o tener ambos (cliente y proveedor simultáneo).
///
/// Migración futura:
///   Customer (legacy) → BusinessPartner + CustomerProfile
///   Los datos de transacción (SalesInvoice) seguirán referenciando CompanyId,
///   y BusinessPartnerId reemplazará la referencia directa a datos fiscales del cliente.
/// </summary>
public sealed class CustomerProfile : AuditableEntity, ISubscriberScopedEntity
{
    public const int NotesMaxLen = 2000;

    public Guid SubscriberId      { get; private set; }
    public Guid BusinessPartnerId { get; private set; }
    public bool IsActive          { get; private set; } = true;
    public string? Notes          { get; private set; }

    // Navegación
    public BusinessPartner? BusinessPartner { get; private set; }

    private CustomerProfile() { }

    public static CustomerProfile Create(
        Guid   subscriberId,
        Guid   businessPartnerId,
        Guid   createdBy,
        string? notes = null)
    {
        if (subscriberId == Guid.Empty)
            throw new ArgumentException("SubscriberId es obligatorio.", nameof(subscriberId));
        if (businessPartnerId == Guid.Empty)
            throw new ArgumentException("BusinessPartnerId es obligatorio.", nameof(businessPartnerId));

        var p = new CustomerProfile
        {
            Id                = Guid.NewGuid(),
            SubscriberId      = subscriberId,
            BusinessPartnerId = businessPartnerId,
            IsActive          = true,
            Notes             = string.IsNullOrWhiteSpace(notes) ? null
                                : notes.Trim().Length > NotesMaxLen
                                    ? throw new ArgumentException($"Notes no puede superar {NotesMaxLen} caracteres.")
                                    : notes.Trim(),
        };
        p.SetCreated(createdBy);
        return p;
    }

    public void Deactivate(Guid updatedBy)
    {
        if (!IsActive)
            throw new InvalidOperationException("El CustomerProfile ya está inactivo.");
        IsActive = false;
        SetUpdated(updatedBy);
    }

    public void Activate(Guid updatedBy)
    {
        if (IsActive)
            throw new InvalidOperationException("El CustomerProfile ya está activo.");
        IsActive = true;
        SetUpdated(updatedBy);
    }

    public void UpdateNotes(string? notes, Guid updatedBy)
    {
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        SetUpdated(updatedBy);
    }
}
