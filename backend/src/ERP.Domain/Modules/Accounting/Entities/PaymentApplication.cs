using ERP.Domain.Common;

namespace ERP.Domain.Modules.Accounting.Entities;

/// <summary>
/// Registro de aplicaciÃ³n de pago a una entrada de CxC o CxP.
/// Una aplicaciÃ³n apunta exactamente a una entrada (AR o AP).
/// </summary>
public sealed class PaymentApplication : AuditableEntity, ISubscriberScopedEntity
{
    public Guid     CompanyId         { get; private set; }
    public Guid?    ArEntryId         { get; private set; }
    public Guid?    ApEntryId         { get; private set; }
    public decimal  Amount            { get; private set; }
    public DateTime ApplicationDate   { get; private set; }
    public string?  PaymentReference  { get; private set; }
    public string?  Notes             { get; private set; }

    private PaymentApplication() { }

    public static PaymentApplication CreateForAr(
        Guid     subscriberId,
        Guid     companyId,
        Guid     arEntryId,
        decimal  amount,
        DateTime applicationDate,
        Guid     createdBy,
        string?  paymentReference = null,
        string?  notes            = null)
    {
        if (amount <= 0)
            throw new ArgumentException("El monto debe ser mayor a cero.", nameof(amount));

        var app = new PaymentApplication
        {
            Id               = Guid.NewGuid(),
            SubscriberId     = subscriberId,
            CompanyId = companyId,
            ArEntryId        = arEntryId,
            ApEntryId        = null,
            Amount           = amount,
            ApplicationDate  = applicationDate.Date,
            PaymentReference = string.IsNullOrWhiteSpace(paymentReference) ? null : paymentReference.Trim(),
            Notes            = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
        };
        app.SetCreated(createdBy);
        return app;
    }

    public static PaymentApplication CreateForAp(
        Guid     subscriberId,
        Guid     companyId,
        Guid     apEntryId,
        decimal  amount,
        DateTime applicationDate,
        Guid     createdBy,
        string?  paymentReference = null,
        string?  notes            = null)
    {
        if (amount <= 0)
            throw new ArgumentException("El monto debe ser mayor a cero.", nameof(amount));

        var app = new PaymentApplication
        {
            Id               = Guid.NewGuid(),
            SubscriberId     = subscriberId,
            CompanyId = companyId,
            ArEntryId        = null,
            ApEntryId        = apEntryId,
            Amount           = amount,
            ApplicationDate  = applicationDate.Date,
            PaymentReference = string.IsNullOrWhiteSpace(paymentReference) ? null : paymentReference.Trim(),
            Notes            = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
        };
        app.SetCreated(createdBy);
        return app;
    }
}
