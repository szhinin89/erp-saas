using ERP.Domain.Common;
using static ERP.Domain.Common.FiscalPrecision;

namespace ERP.Domain.Modules.Sales.Entities;

public sealed class SalesReceivable : AuditableEntity, ITenantScopedEntity, ICompanyOperationalEntity
{
    public const int StatusMaxLen = 20;

    public Guid CompanyId { get; private set; }
    public Guid InvoiceId { get; private set; }
    public Guid CustomerId { get; private set; }
    public decimal OriginalAmount { get; private set; }
    public decimal PaidAmount { get; private set; }
    public string Status { get; private set; } = "pending";

    public decimal BalanceDue => OriginalAmount - PaidAmount;

    private readonly List<SalesReceivableInstallment> _installments = new();
    public IReadOnlyList<SalesReceivableInstallment> Installments => _installments.AsReadOnly();

    private SalesReceivable() { }

    public static SalesReceivable Create(
        Guid tenantId, Guid companyId, Guid invoiceId,
        Guid customerId, decimal originalAmount, Guid createdBy)
    {
        if (invoiceId == Guid.Empty)
            throw new ArgumentException("La factura es obligatoria.", nameof(invoiceId));
        if (customerId == Guid.Empty)
            throw new ArgumentException("El cliente es obligatorio.", nameof(customerId));
        if (originalAmount <= 0)
            throw new ArgumentException("El monto original debe ser mayor a cero.", nameof(originalAmount));

        var r = new SalesReceivable
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            InvoiceId = invoiceId,
            CustomerId = customerId,
            OriginalAmount = originalAmount,
            PaidAmount = 0,
            Status = "pending",
        };
        r.SetCreated(createdBy);
        return r;
    }

    public void GenerateInstallments(DateOnly baseDate, int creditTermDays, int installmentCount)
    {
        _installments.Clear();

        if (installmentCount <= 0) installmentCount = 1;
        var intervalDays = installmentCount > 1 ? creditTermDays / installmentCount : creditTermDays;

        var baseAmount = Math.Round(OriginalAmount / installmentCount, TaxAmount, MidpointRounding.AwayFromZero);
        var accumulated = 0m;

        for (var i = 1; i <= installmentCount; i++)
        {
            var isLast = i == installmentCount;
            var amount = isLast ? OriginalAmount - accumulated : baseAmount;
            var dueDate = baseDate.AddDays(intervalDays * i);

            _installments.Add(SalesReceivableInstallment.Create(
                Id, TenantId, i, dueDate, amount));

            accumulated += amount;
        }
    }

    public void Cancel(Guid updatedBy)
    {
        if (PaidAmount > 0)
            throw new InvalidOperationException(
                "No se puede cancelar una cuenta por cobrar con pagos registrados.");

        Status = "cancelled";
        _installments.Clear();
        SetUpdated(updatedBy);
    }

    /// <summary>
    /// Registra un cobro aplicado contra esta CxC (Fase 5.5.5.2) — invocado por el caso de uso de
    /// Application (fase futura) que coordina un <c>Payment</c> (dirección Collection) ya
    /// <c>Applied</c> contra una o más <c>SalesReceivable</c>. No levanta ningún evento propio:
    /// el hecho de negocio "cobro aplicado" ya lo publica <c>Payment.Apply()</c> — este método
    /// solo mantiene el saldo de la CxC consistente con lo que ese pago registró.
    /// </summary>
    public void RegisterCollection(decimal amount, Guid updatedBy)
    {
        if (amount <= 0)
            throw new ArgumentException("El monto del cobro debe ser mayor a cero.", nameof(amount));
        if (Status == "cancelled")
            throw new InvalidOperationException("No se puede registrar un cobro sobre una cuenta por cobrar cancelada.");
        if (amount > BalanceDue)
            throw new InvalidOperationException("El monto del cobro excede el saldo pendiente de la cuenta por cobrar.");

        PaidAmount += amount;
        SetUpdated(updatedBy);
    }

    /// <summary>
    /// Reversa un cobro previamente registrado (Fase 5.5.5.2) — invocado cuando el caso de uso de
    /// Application procesa un <c>Payment.Reverse()</c> ya ejecutado. Sin evento propio, mismo
    /// criterio que <see cref="RegisterCollection"/>.
    /// </summary>
    public void ReverseCollection(decimal amount, Guid updatedBy)
    {
        if (amount <= 0)
            throw new ArgumentException("El monto a reversar debe ser mayor a cero.", nameof(amount));
        if (amount > PaidAmount)
            throw new InvalidOperationException("El monto a reversar excede el monto cobrado registrado en la cuenta por cobrar.");

        PaidAmount -= amount;
        SetUpdated(updatedBy);
    }
}
