using ERP.Domain.Common;
using ERP.Domain.Modules.Payables.Enums;

namespace ERP.Domain.Modules.Payables.Entities;

/// <summary>
/// PAYABLES-GENERIC-FOUNDATION-09 / PAYABLES-PURCHASE-MIGRATION-10 — CxP genérica: la deuda viva
/// con el proveedor, desacoplada de su documento de origen (Compra o Gasto son documentos de
/// origen; esta entidad es la obligación resultante y la ÚNICA fuente de saldo — decisión
/// funcional: no hay dual-write con <c>PurchasePayable</c>, que fue eliminado).
/// <see cref="OriginType"/>/<see cref="OriginId"/> apuntan al documento que la generó — únicos por
/// (TenantId, CompanyId, OriginType, OriginId), ver <c>AccountsPayableConfiguration</c> para el
/// índice único real y <c>AccountsPayableService.CreateFromOriginAsync</c> para la idempotencia a
/// nivel de aplicación.
/// </summary>
/// <remarks>
/// Todos los montos (<see cref="TotalAmount"/>, <see cref="PaidAmount"/>, <see cref="RetainedAmount"/>,
/// <see cref="ReturnCreditAmount"/>, <see cref="SupplierCreditAmount"/>, <see cref="CreditNoteAmount"/>,
/// <see cref="OutstandingAmount"/>) se derivan sumando <see cref="Installments"/> — nunca son
/// columnas propias (decisión funcional del ticket: "AccountsPayableInstallment es la
/// cuota/saldo vivo"). Cada método <c>Apply*</c>/<c>Reverse*</c> distribuye el monto entre cuotas
/// mediante un motor FIFO genérico (<see cref="AllocateFifo"/>): la cuota con menor
/// <c>InstallmentNumber</c> con capacidad disponible se satura primero. Con una sola cuota (caso
/// más común, incluido todo Gastos) esto se reduce exactamente al comportamiento agregado que tenía
/// <c>PurchasePayable</c> (un único acumulador por cuenta por pagar).
/// </remarks>
public sealed class AccountsPayable : AuditableEntity, ITenantScopedEntity, ICompanyOperationalEntity
{
    public const int DocumentTypeMaxLen = 5;
    public const int DocumentNumberMaxLen = 30;

    public Guid CompanyId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid SupplierId { get; private set; }
    public AccountsPayableOriginType OriginType { get; private set; }
    public Guid OriginId { get; private set; }
    public string DocumentType { get; private set; } = null!;
    public string DocumentNumber { get; private set; } = null!;
    public DateOnly IssueDate { get; private set; }
    public DateOnly AccountingDate { get; private set; }
    public AccountsPayableStatus Status { get; private set; } = AccountsPayableStatus.Pending;

    private readonly List<AccountsPayableInstallment> _installments = new();
    public IReadOnlyList<AccountsPayableInstallment> Installments => _installments.AsReadOnly();

    public decimal TotalAmount => _installments.Sum(i => i.Amount);
    public decimal PaidAmount => _installments.Sum(i => i.PaidAmount);
    public decimal RetainedAmount => _installments.Sum(i => i.RetainedAmount);
    public decimal ReturnCreditAmount => _installments.Sum(i => i.ReturnCreditAmount);
    public decimal SupplierCreditAmount => _installments.Sum(i => i.SupplierCreditAmount);
    public decimal CreditNoteAmount => _installments.Sum(i => i.CreditNoteAmount);
    public decimal OutstandingAmount => _installments.Sum(i => i.OutstandingAmount);

    private AccountsPayable() { }

    public static AccountsPayable CreateFromOrigin(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid supplierId,
        AccountsPayableOriginType originType,
        Guid originId,
        string documentType,
        string documentNumber,
        DateOnly issueDate,
        DateOnly accountingDate,
        Guid createdBy
    )
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("El tenant es obligatorio.", nameof(tenantId));
        if (companyId == Guid.Empty)
            throw new ArgumentException("La empresa es obligatoria.", nameof(companyId));
        if (branchId == Guid.Empty)
            throw new ArgumentException("La sucursal es obligatoria.", nameof(branchId));
        if (supplierId == Guid.Empty)
            throw new ArgumentException("El proveedor es obligatorio.", nameof(supplierId));
        if (originId == Guid.Empty)
            throw new ArgumentException("El documento de origen es obligatorio.", nameof(originId));
        if (string.IsNullOrWhiteSpace(documentType))
            throw new ArgumentException("El tipo de documento es obligatorio.", nameof(documentType));
        if (string.IsNullOrWhiteSpace(documentNumber))
            throw new ArgumentException("El número de documento es obligatorio.", nameof(documentNumber));

        var payable = new AccountsPayable
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            SupplierId = supplierId,
            OriginType = originType,
            OriginId = originId,
            DocumentType = documentType.Trim(),
            DocumentNumber = documentNumber.Trim(),
            IssueDate = issueDate,
            AccountingDate = accountingDate,
            Status = AccountsPayableStatus.Pending,
        };
        payable.SetCreated(createdBy);
        return payable;
    }

    /// <summary>
    /// Agrega una cuota. La mayoría de orígenes generan una sola cuota por el total (caso común,
    /// incluido todo Gastos); Compras con condición de pago a plazos genera N cuotas — este método
    /// es genérico para ambos casos, sin cambios aquí.
    /// </summary>
    public AccountsPayableInstallment AddInstallment(int installmentNumber, DateOnly dueDate, decimal amount)
    {
        if (_installments.Any(i => i.InstallmentNumber == installmentNumber))
            throw new InvalidOperationException(
                $"Ya existe una cuota con el número {installmentNumber}."
            );

        var installment = AccountsPayableInstallment.Create(Id, TenantId, installmentNumber, dueDate, amount);
        _installments.Add(installment);
        RecalculateStatus();
        return installment;
    }

    /// <summary>Registra un pago aplicado contra esta CxP (reemplaza <c>PurchasePayable.RegisterPayment</c>).</summary>
    public void RegisterPayment(decimal amount, Guid updatedBy) =>
        Apply(AccountsPayableAdjustmentType.Payment, amount, updatedBy, "del pago");

    /// <summary>Reversa un pago previamente registrado (reemplaza <c>PurchasePayable.ReversePayment</c>).</summary>
    public void ReversePayment(decimal amount, Guid updatedBy) =>
        Reverse(AccountsPayableAdjustmentType.Payment, amount, updatedBy, "pagado");

    /// <summary>
    /// Reconoce una devolución de compra autorizada directamente contra esta CxP (reemplaza
    /// <c>PurchasePayable.ApplyReturnCredit</c>) — nunca reutiliza <see cref="RegisterPayment"/>, es
    /// un track independiente de <see cref="OutstandingAmount"/>.
    /// </summary>
    /// <returns><c>appliedAmount</c> (lo efectivamente aplicado contra el saldo) y el excedente que Application usará para crear un <c>SupplierCredit</c>.</returns>
    public (decimal AppliedAmount, decimal Excess) ApplyReturnCredit(decimal recognizedAmount, Guid updatedBy)
    {
        if (recognizedAmount <= 0)
            throw new ArgumentException(
                "El monto reconocido de la devolución debe ser mayor a cero.",
                nameof(recognizedAmount)
            );
        if (Status == AccountsPayableStatus.Cancelled)
            throw new InvalidOperationException(
                "No se puede aplicar una devolución sobre una cuenta por pagar anulada."
            );

        var appliedAmount = Math.Min(recognizedAmount, OutstandingAmount);
        if (appliedAmount > 0)
            AllocateFifo(AccountsPayableAdjustmentType.ReturnCredit, appliedAmount, forward: true);
        SetUpdated(updatedBy);

        return (appliedAmount, recognizedAmount - appliedAmount);
    }

    /// <summary>Reversa la devolución de compra aplicada (reemplaza <c>PurchasePayable.ReverseReturnCredit</c>).</summary>
    public void ReverseReturnCredit(decimal appliedAmount, Guid updatedBy) =>
        Reverse(AccountsPayableAdjustmentType.ReturnCredit, appliedAmount, updatedBy, "de devolución aplicado");

    /// <summary>Aplica un <c>SupplierCredit</c> externo contra esta CxP (reemplaza <c>PurchasePayable.ApplySupplierCredit</c>).</summary>
    public void ApplySupplierCredit(decimal amount, Guid updatedBy) =>
        Apply(AccountsPayableAdjustmentType.SupplierCredit, amount, updatedBy, "del crédito de proveedor");

    /// <summary>Reversa la aplicación de un <c>SupplierCredit</c> externo (reemplaza <c>PurchasePayable.ReverseSupplierCredit</c>).</summary>
    public void ReverseSupplierCredit(decimal amount, Guid updatedBy) =>
        Reverse(AccountsPayableAdjustmentType.SupplierCredit, amount, updatedBy, "de crédito de proveedor aplicado");

    /// <summary>
    /// Aplica una <c>PurchaseCreditNote</c> (descuento/promoción) contra esta CxP (reemplaza
    /// <c>PurchasePayable.ApplyCreditNote</c>). Nunca trunca al saldo disponible: el bloqueo por
    /// excedente ya ocurrió en <c>PurchaseCreditNote.Authorize()</c> — este método solo rechaza
    /// defensivamente si, pese a eso, el monto excede el saldo actual.
    /// </summary>
    public void ApplyCreditNote(decimal amount, Guid updatedBy) =>
        Apply(AccountsPayableAdjustmentType.CreditNote, amount, updatedBy, "de la nota de crédito a aplicar");

    /// <summary>Reversa una <c>PurchaseCreditNote</c> aplicada (reemplaza <c>PurchasePayable.ReverseCreditNote</c>).</summary>
    public void ReverseCreditNote(decimal amount, Guid updatedBy) =>
        Reverse(AccountsPayableAdjustmentType.CreditNote, amount, updatedBy, "de nota de crédito aplicado");

    /// <summary>
    /// Aplica una retención tributaria contra esta CxP (reemplaza <c>PurchasePayable.ApplyRetention</c>).
    /// A diferencia del modelo original (que reconstruía el cronograma de cuotas neto de la
    /// retención), aquí la retención es un track más entre las cuotas existentes, asignado por el
    /// mismo motor FIFO — el efecto neto sobre <see cref="OutstandingAmount"/> es idéntico.
    /// </summary>
    public void ApplyRetention(decimal amount, Guid updatedBy) =>
        Apply(AccountsPayableAdjustmentType.Retention, amount, updatedBy, "de la retención");

    /// <summary>Reversa la retención tributaria aplicada en su totalidad (reemplaza <c>PurchasePayable.ReverseRetention</c>).</summary>
    public void ReverseRetention(Guid updatedBy)
    {
        var amount = RetainedAmount;
        if (amount <= 0)
            return;
        Reverse(AccountsPayableAdjustmentType.Retention, amount, updatedBy, "de retención aplicado");
    }

    /// <summary>Anula la CxP (reemplaza <c>PurchasePayable.CancelPayable</c>) — bloquea si ya hay pagos registrados.</summary>
    public void Cancel(Guid updatedBy)
    {
        if (PaidAmount > 0)
            throw new InvalidOperationException(
                "No se puede anular una cuenta por pagar con pagos registrados."
            );

        foreach (var installment in _installments)
            installment.MarkCancelled();
        Status = AccountsPayableStatus.Cancelled;
        SetUpdated(updatedBy);
    }

    private void Apply(AccountsPayableAdjustmentType type, decimal amount, Guid updatedBy, string label)
    {
        if (amount <= 0)
            throw new ArgumentException($"El monto {label} debe ser mayor a cero.", nameof(amount));
        if (Status == AccountsPayableStatus.Cancelled)
            throw new InvalidOperationException(
                "No se puede aplicar un ajuste sobre una cuenta por pagar anulada."
            );
        if (amount > OutstandingAmount)
            throw new InvalidOperationException(
                $"El monto {label} excede el saldo pendiente de la cuenta por pagar."
            );

        AllocateFifo(type, amount, forward: true);
        RecalculateStatus();
        SetUpdated(updatedBy);
    }

    private void Reverse(AccountsPayableAdjustmentType type, decimal amount, Guid updatedBy, string label)
    {
        if (amount <= 0)
            throw new ArgumentException("El monto a reversar debe ser mayor a cero.", nameof(amount));

        var totalApplied = _installments.Sum(i => i.GetApplied(type));
        if (amount > totalApplied)
            throw new InvalidOperationException(
                $"El monto a reversar excede el monto {label} registrado en la cuenta por pagar."
            );

        AllocateFifo(type, amount, forward: false);
        RecalculateStatus();
        SetUpdated(updatedBy);
    }

    /// <summary>
    /// Recalcula <see cref="Status"/> agregando el estado de todas las cuotas — igual patrón que
    /// <c>AccountsPayableInstallment.RecalculateStatus</c>, pero a nivel cabecera. Nunca se invoca
    /// tras <see cref="Cancel"/> (que fija <c>Cancelled</c> de forma terminal) — <c>Apply</c>/
    /// <c>Reverse</c> ya rechazan mutaciones sobre una CxP anulada, por lo que esta guarda es
    /// defensiva.
    /// </summary>
    private void RecalculateStatus()
    {
        if (Status == AccountsPayableStatus.Cancelled)
            return;

        if (_installments.Count == 0)
        {
            Status = AccountsPayableStatus.Pending;
            return;
        }

        if (OutstandingAmount <= 0)
            Status = AccountsPayableStatus.Paid;
        else if (
            PaidAmount > 0
            || RetainedAmount > 0
            || ReturnCreditAmount > 0
            || SupplierCreditAmount > 0
            || CreditNoteAmount > 0
        )
            Status = AccountsPayableStatus.PartiallyPaid;
        else
            Status = AccountsPayableStatus.Pending;
    }

    /// <summary>
    /// Motor de asignación FIFO compartido por todos los <c>Apply*</c>/<c>Reverse*</c>: aplicar
    /// satura cuotas por orden ascendente de <see cref="AccountsPayableInstallment.InstallmentNumber"/>
    /// (la más antigua primero); reversar libera en orden descendente (lo más reciente primero).
    /// Con una sola cuota (caso común) esto es equivalente a un único acumulador agregado.
    /// </summary>
    private void AllocateFifo(AccountsPayableAdjustmentType type, decimal amount, bool forward)
    {
        var remaining = amount;
        var ordered = forward
            ? _installments.OrderBy(i => i.InstallmentNumber)
            : _installments.OrderByDescending(i => i.InstallmentNumber);

        foreach (var installment in ordered)
        {
            if (remaining <= 0)
                break;

            var capacity = forward ? installment.OutstandingAmount : installment.GetApplied(type);
            if (capacity <= 0)
                continue;

            var take = Math.Min(capacity, remaining);
            if (forward)
                installment.Apply(type, take);
            else
                installment.Reverse(type, take);
            remaining -= take;
        }
    }
}
