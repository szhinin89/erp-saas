using ERP.Domain.Common;
using static ERP.Domain.Common.FiscalPrecision;

namespace ERP.Domain.Modules.Purchases.Entities;

public sealed class PurchasePayable : AuditableEntity, ITenantScopedEntity, ICompanyOperationalEntity
{
    public Guid CompanyId { get; private set; }
    public Guid PurchaseId { get; private set; }
    public Guid SupplierId { get; private set; }
    public decimal TotalAmount { get; private set; }
    public decimal PaidAmount { get; private set; }
    public decimal TotalRetained { get; private set; }
    public decimal BalanceDue => TotalAmount - PaidAmount - TotalRetained;
    public string Status { get; private set; } = "pending";

    private readonly List<PurchasePayableInstallment> _installments = new();
    public IReadOnlyList<PurchasePayableInstallment> Installments => _installments.AsReadOnly();

    private PurchasePayable() { }

    public static PurchasePayable Create(
        Guid tenantId, Guid companyId, Guid purchaseId, Guid supplierId,
        decimal totalAmount, Guid createdBy)
    {
        if (totalAmount <= 0)
            throw new ArgumentException("El monto total debe ser mayor a cero.", nameof(totalAmount));

        var p = new PurchasePayable
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            PurchaseId = purchaseId,
            SupplierId = supplierId,
            TotalAmount = totalAmount,
            PaidAmount = 0,
            Status = "pending",
        };
        p.SetCreated(createdBy);
        return p;
    }

    public void AddInstallment(int number, DateOnly dueDate, decimal amount)
    {
        if (amount <= 0) throw new ArgumentException("El monto de la cuota debe ser mayor a cero.");
        _installments.Add(PurchasePayableInstallment.Create(Id, TenantId, number, dueDate, amount));
    }

    public void CancelPayable()
    {
        if (PaidAmount > 0)
            throw new InvalidOperationException("No se puede anular una cuenta por pagar con pagos registrados.");
        Status = "cancelled";
        TotalRetained = 0;
        _installments.Clear();
    }

    /// <summary>
    /// Registra un pago aplicado contra esta CxP (Fase 5.5.5.2) — invocado por el caso de uso de
    /// Application (fase futura) que coordina un <c>Payment</c> (dirección Payment) ya
    /// <c>Applied</c> contra una o más <c>PurchasePayable</c>. No levanta ningún evento propio:
    /// el hecho de negocio "pago aplicado" ya lo publica <c>Payment.Apply()</c> — este método
    /// solo mantiene el saldo de la CxP consistente con lo que ese pago registró.
    /// </summary>
    public void RegisterPayment(decimal amount, Guid updatedBy)
    {
        if (amount <= 0)
            throw new ArgumentException("El monto del pago debe ser mayor a cero.", nameof(amount));
        if (Status == "cancelled")
            throw new InvalidOperationException("No se puede registrar un pago sobre una cuenta por pagar anulada.");
        if (amount > BalanceDue)
            throw new InvalidOperationException("El monto del pago excede el saldo pendiente de la cuenta por pagar.");

        PaidAmount += amount;
        SetUpdated(updatedBy);
    }

    /// <summary>
    /// Reversa un pago previamente registrado (Fase 5.5.5.2) — invocado cuando el caso de uso de
    /// Application procesa un <c>Payment.Reverse()</c> ya ejecutado. Sin evento propio, mismo
    /// criterio que <see cref="RegisterPayment"/>. No confundir con <see cref="ReverseRetention"/>,
    /// que reversa una retención tributaria, no un pago.
    /// </summary>
    public void ReversePayment(decimal amount, Guid updatedBy)
    {
        if (amount <= 0)
            throw new ArgumentException("El monto a reversar debe ser mayor a cero.", nameof(amount));
        if (amount > PaidAmount)
            throw new InvalidOperationException("El monto a reversar excede el monto pagado registrado en la cuenta por pagar.");

        PaidAmount -= amount;
        SetUpdated(updatedBy);
    }

    /// <summary>
    /// Genera las cuotas de la CxP a partir del cronograma de pago ya confirmado en
    /// <see cref="PurchaseInvoice.PaymentSchedules"/> — misma fuente que ve y personaliza el
    /// usuario al confirmar, para que la CxP nunca diverja del cronograma mostrado.
    /// </summary>
    public void GenerateInstallments(IReadOnlyList<PurchasePaymentSchedule> schedule)
        => RebuildInstallments(schedule);

    public void ApplyRetention(decimal retainedAmount, IReadOnlyList<PurchasePaymentSchedule> schedule)
    {
        if (retainedAmount < 0) throw new ArgumentException("El monto retenido no puede ser negativo.");
        TotalRetained = retainedAmount;
        RebuildInstallments(schedule);
    }

    public void ReverseRetention(IReadOnlyList<PurchasePaymentSchedule> schedule)
    {
        TotalRetained = 0;
        RebuildInstallments(schedule);
    }

    /// <summary>
    /// Reconstruye las cuotas conservando siempre el número de cuotas y las fechas de
    /// vencimiento del cronograma confirmado — solo reprorratea <see cref="BalanceDue"/> entre
    /// ellas según el peso relativo de cada cuota original. Nunca colapsa a una sola cuota.
    /// </summary>
    private void RebuildInstallments(IReadOnlyList<PurchasePaymentSchedule> schedule)
    {
        _installments.Clear();

        var netAmount = BalanceDue;
        var scheduleTotal = schedule.Sum(s => s.Amount);
        if (netAmount <= 0 || schedule.Count == 0 || scheduleTotal <= 0)
            return;

        var ordered = schedule.OrderBy(s => s.InstallmentNumber).ToList();
        decimal allocated = 0;
        for (var i = 0; i < ordered.Count; i++)
        {
            var s = ordered[i];
            var amount = i == ordered.Count - 1
                ? netAmount - allocated
                : Math.Round(netAmount * s.Amount / scheduleTotal, TaxAmount);
            _installments.Add(PurchasePayableInstallment.Create(Id, TenantId, s.InstallmentNumber, s.DueDate, amount));
            allocated += amount;
        }
    }
}
