using ERP.Domain.Common;

namespace ERP.Domain.Modules.Accounting.Entities;

/// <summary>
/// Entrada de Cuentas por Cobrar generada a partir de una factura de venta.
/// Rastrea el monto original, pagado y pendiente. El cobro se registra
/// mediante <see cref="PaymentApplication"/>.
/// </summary>
public sealed class AccountsReceivableEntry : AuditableEntity, ISubscriberScopedEntity
{
    public const string StatusOpen          = "Open";
    public const string StatusPartiallyPaid = "PartiallyPaid";
    public const string StatusPaid          = "Paid";
    public const string StatusCancelled     = "Cancelled";

    public Guid     CompanyId          { get; private set; }
    public Guid     BusinessPartnerId  { get; private set; }
    public Guid?    SalesBillId        { get; private set; }
    public string   Reference          { get; private set; } = null!;
    public DateTime IssueDate          { get; private set; }
    public DateTime DueDate            { get; private set; }
    public decimal  OriginalAmount     { get; private set; }
    public decimal  PaidAmount         { get; private set; }
    public string   Status             { get; private set; } = StatusOpen;
    public string   Currency           { get; private set; } = "USD";

    public decimal  RemainingAmount    => OriginalAmount - PaidAmount;
    public bool     IsPaid             => PaidAmount >= OriginalAmount;
    public int      DaysOverdue(DateTime asOf) =>
        Status is StatusPaid or StatusCancelled ? 0 : Math.Max(0, (int)(asOf - DueDate).TotalDays);

    private AccountsReceivableEntry() { }

    public static AccountsReceivableEntry Create(
        Guid     subscriberId,
        Guid     companyId,
        Guid     businessPartnerId,
        string   reference,
        DateTime issueDate,
        DateTime dueDate,
        decimal  amount,
        Guid     createdBy,
        Guid?    salesBillId = null,
        string   currency    = "USD")
    {
        if (amount <= 0)
            throw new ArgumentException("El monto debe ser mayor a cero.", nameof(amount));
        if (dueDate < issueDate)
            throw new ArgumentException("La fecha de vencimiento no puede ser anterior a la fecha de emisión.", nameof(dueDate));
        if (string.IsNullOrWhiteSpace(reference))
            throw new ArgumentException("La referencia es requerida.", nameof(reference));

        var entry = new AccountsReceivableEntry
        {
            Id                = Guid.NewGuid(),
            SubscriberId      = subscriberId,
            CompanyId         = companyId,
            BusinessPartnerId = businessPartnerId,
            SalesBillId       = salesBillId,
            Reference         = reference.Trim(),
            IssueDate         = issueDate.Date,
            DueDate           = dueDate.Date,
            OriginalAmount    = amount,
            PaidAmount        = 0m,
            Status            = StatusOpen,
            Currency          = currency,
        };
        entry.SetCreated(createdBy);
        return entry;
    }

    public void ApplyPayment(decimal amount, Guid userId)
    {
        if (Status is StatusPaid or StatusCancelled)
            throw new InvalidOperationException($"No se puede aplicar un pago a una entrada en estado {Status}.");
        if (amount <= 0)
            throw new ArgumentException("El monto del pago debe ser mayor a cero.", nameof(amount));
        if (amount > RemainingAmount)
            throw new ArgumentException($"El monto del pago ({amount}) supera el saldo pendiente ({RemainingAmount}).", nameof(amount));

        PaidAmount += amount;
        Status = IsPaid ? StatusPaid : StatusPartiallyPaid;
        SetUpdated(userId);
    }

    public void Cancel(Guid userId)
    {
        if (Status == StatusCancelled)
            throw new InvalidOperationException("La entrada ya está cancelada.");
        if (Status == StatusPaid)
            throw new InvalidOperationException("No se puede cancelar una entrada totalmente cobrada.");

        Status = StatusCancelled;
        SetUpdated(userId);
    }
}
