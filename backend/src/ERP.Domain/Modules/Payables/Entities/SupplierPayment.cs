using ERP.Domain.Common;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Payables.Events;

namespace ERP.Domain.Modules.Payables.Entities;

/// <summary>
/// SUPPLIER-PAYMENTS-FOUNDATION-15B — agregado raíz independiente para el registro de pagos a
/// proveedores, aprobado por SUPPLIER-PAYMENTS-AUDIT-15A. Deliberadamente NO reutiliza
/// <c>Payment</c>/<c>PaymentApplicationLine</c> (Finance): esos sostienen Collections/CxC en vivo y
/// su forma es plana (un único <c>PaymentMethodId</c>/<c>CompanyBankAccountId</c> de cabecera, una
/// línea de aplicación = un documento) — no admite varios medios por pago, una cuota pagada con
/// varios medios, ni un medio repartido entre varias cuotas sin reestructurar esos campos de
/// cabecera y arriesgar el flujo de Collections ya probado.
/// </summary>
/// <remarks>
/// Sin <c>Draft</c> visible (regla del proyecto: procesos simples de un paso van con confirmación
/// directa + modal de resumen, no Draft) — <see cref="Create"/> valida TODO el agregado (medios,
/// aplicaciones, matriz de distribución, balance) en una sola llamada y devuelve una instancia ya
/// <see cref="SupplierPaymentStatus.Confirmed"/>; nunca existe un estado intermedio persistible. El
/// caso de uso de Application (fase posterior a esta) es responsable de: reservar
/// <c>SystemNumber</c> vía <c>ISupplierPaymentSequenceRepository.CaptureNextAsync</c> ANTES de
/// llamar a <see cref="Create"/>, invocar <c>AccountsPayable.RegisterPayment</c>/
/// <c>AccountsPayableInstallment</c> por cada aplicación, y persistir todo en una única transacción
/// (si algo falla, nada debe quedar parcial).
/// </remarks>
public sealed class SupplierPayment : AuditableEntity, ITenantScopedEntity, ICompanyOperationalEntity
{
    public const int SystemNumberMaxLen = 30;
    public const int ReceiptNumberMaxLen = 30;

    public Guid CompanyId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid SupplierId { get; private set; }
    public DateOnly PaymentDate { get; private set; }
    public decimal TotalAmount { get; private set; }

    /// <summary>Generado por <c>SupplierPaymentSequence</c> — obligatorio, único por (TenantId, CompanyId).</summary>
    public string SystemNumber { get; private set; } = null!;

    /// <summary>Número físico del comprobante (cheque, papeleta, etc.) — opcional, digitado por el usuario.</summary>
    public string? ReceiptNumber { get; private set; }

    public SupplierPaymentStatus Status { get; private set; }
    public DateTime? ReversedAtUtc { get; private set; }
    public Guid? ReversedBy { get; private set; }
    public string? ReverseReason { get; private set; }

    /// <summary>
    /// 02B-FINAL — motivo estructurado de la reversa documental de las fuentes bancarias (null si
    /// el pago no tiene fuentes bancarias o no está reversado).
    /// </summary>
    public SupplierPaymentBankReversalReason? ReversalBankReason { get; private set; }

    /// <summary>
    /// 02B-FINAL — afirmación explícita, registrada al reversar, de que el efectivo NO fue entregado
    /// al proveedor y permanece en la misma sesión de caja (null si el pago no tiene fuentes de caja
    /// o no está reversado).
    /// </summary>
    public bool? ReversalCashNotDeliveredConfirmed { get; private set; }

    /// <summary>Número visible en pantallas/reportes: <see cref="ReceiptNumber"/> si existe, si no <see cref="SystemNumber"/>.</summary>
    public string DisplayNumber => string.IsNullOrWhiteSpace(ReceiptNumber) ? SystemNumber : ReceiptNumber;

    private readonly List<SupplierPaymentMethodLine> _methodLines = new();
    public IReadOnlyList<SupplierPaymentMethodLine> MethodLines => _methodLines.AsReadOnly();

    private readonly List<SupplierPaymentApplicationLine> _applicationLines = new();
    public IReadOnlyList<SupplierPaymentApplicationLine> ApplicationLines => _applicationLines.AsReadOnly();

    private readonly List<SupplierPaymentAllocationLine> _allocationLines = new();
    public IReadOnlyList<SupplierPaymentAllocationLine> AllocationLines => _allocationLines.AsReadOnly();

    private SupplierPayment() { }

    /// <summary>
    /// Construye y confirma un pago a proveedor completo en una sola llamada. Valida, en orden:
    /// campos obligatorios de cabecera; al menos un medio, una aplicación y una allocation; que la
    /// suma de medios, la suma de aplicaciones y la suma de allocations sean todas exactamente
    /// <paramref name="totalAmount"/>; que cada medio quede distribuido al 100% entre allocations; y
    /// que cada aplicación quede cubierta al 100% entre allocations. Cualquier violación lanza antes
    /// de construir el agregado — nunca devuelve un <see cref="SupplierPayment"/> a medias.
    /// </summary>
    public static SupplierPayment Create(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid supplierId,
        DateOnly paymentDate,
        decimal totalAmount,
        string systemNumber,
        string? receiptNumber,
        IReadOnlyList<SupplierPaymentMethodLineInput> methodLines,
        IReadOnlyList<SupplierPaymentApplicationLineInput> applicationLines,
        IReadOnlyList<SupplierPaymentAllocationInput> allocations,
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
        if (totalAmount <= 0)
            throw new ArgumentException("El monto total del pago debe ser mayor a cero.", nameof(totalAmount));
        if (string.IsNullOrWhiteSpace(systemNumber))
            throw new ArgumentException("El número de sistema es obligatorio.", nameof(systemNumber));
        if (methodLines is null || methodLines.Count == 0)
            throw new ArgumentException(
                "El pago debe tener al menos un medio de pago.",
                nameof(methodLines)
            );
        if (applicationLines is null || applicationLines.Count == 0)
            throw new ArgumentException(
                "El pago debe tener al menos una aplicación a cuota.",
                nameof(applicationLines)
            );
        if (allocations is null || allocations.Count == 0)
            throw new ArgumentException(
                "El pago debe tener al menos una distribución medio↔cuota.",
                nameof(allocations)
            );

        var payment = new SupplierPayment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            SupplierId = supplierId,
            PaymentDate = paymentDate,
            TotalAmount = totalAmount,
            SystemNumber = systemNumber.Trim(),
            ReceiptNumber = string.IsNullOrWhiteSpace(receiptNumber) ? null : receiptNumber.Trim(),
            Status = SupplierPaymentStatus.Confirmed,
        };

        foreach (var input in methodLines)
            payment._methodLines.Add(
                SupplierPaymentMethodLine.Create(
                    payment.Id,
                    tenantId,
                    input.PaymentMethodId,
                    input.CompanyBankAccountId,
                    input.CashRegisterId,
                    input.Amount,
                    input.ReferenceNumber,
                    input.CheckNumber,
                    input.CheckDate,
                    input.Notes,
                    // 02A-FINAL: la fecha efectiva de una fuente bancaria es un dato real del
                    // extracto — obligatoria y explícita, nunca completada con PaymentDate (base
                    // confiable de la futura conciliación). Fuente de caja → siempre null.
                    input.TransactionDate
                )
            );

        foreach (var input in applicationLines)
            payment._applicationLines.Add(
                SupplierPaymentApplicationLine.Create(
                    payment.Id,
                    tenantId,
                    input.AccountsPayableInstallmentId,
                    input.AmountApplied
                )
            );

        foreach (var input in allocations)
        {
            if (input.MethodLineIndex < 0 || input.MethodLineIndex >= payment._methodLines.Count)
                throw new ArgumentException(
                    $"La distribución referencia un medio de pago inexistente (índice {input.MethodLineIndex}).",
                    nameof(allocations)
                );
            if (input.ApplicationLineIndex < 0 || input.ApplicationLineIndex >= payment._applicationLines.Count)
                throw new ArgumentException(
                    $"La distribución referencia una aplicación inexistente (índice {input.ApplicationLineIndex}).",
                    nameof(allocations)
                );

            payment._allocationLines.Add(
                SupplierPaymentAllocationLine.Create(
                    payment.Id,
                    tenantId,
                    payment._methodLines[input.MethodLineIndex].Id,
                    payment._applicationLines[input.ApplicationLineIndex].Id,
                    input.Amount
                )
            );
        }

        payment.EnsureBalanced();
        payment.SetCreated(createdBy);

        payment.RaiseDomainEvent(
            new SupplierPaymentConfirmedEvent(
                tenantId,
                payment.Id,
                companyId,
                supplierId,
                totalAmount,
                paymentDate,
                payment._methodLines
                    .Select(l => new SupplierPaymentConfirmedMethodLine(
                        l.CompanyBankAccountId,
                        l.CashRegisterId,
                        l.Amount
                    ))
                    .ToList()
            )
        );

        return payment;
    }

    /// <summary>
    /// ZH-SUPPLIER-PAYMENT-CASH-TRANSFER-HARDENING-02A — vincula una fuente de caja con el
    /// <c>CashMovement</c> operativo registrado en la <c>CashSession</c> abierta. El agregado no
    /// conoce Caja (mismo principio que con <c>AccountsPayable</c>): Application registra el
    /// movimiento y solo fija aquí la trazabilidad. Únicamente sobre un pago Confirmed.
    /// </summary>
    public void LinkCashMovement(Guid methodLineId, Guid cashSessionId, Guid cashMovementId)
    {
        if (Status != SupplierPaymentStatus.Confirmed)
            throw new InvalidOperationException(
                "Solo un pago Confirmed puede vincularse a un movimiento de caja."
            );
        var line =
            _methodLines.FirstOrDefault(l => l.Id == methodLineId)
            ?? throw new InvalidOperationException("El medio de pago indicado no pertenece a este pago.");
        line.LinkCashMovement(cashSessionId, cashMovementId);
    }

    /// <summary>
    /// SUPPLIER-PAYMENTS-REVERSE-16 — reversa un pago ya confirmado: única transición de estado
    /// válida es Confirmed → Reversed (nunca Reversed → Reversed, "bloquear doble reversa"). No
    /// muta ninguna línea (medios/aplicaciones/allocations quedan intactas como registro histórico
    /// de lo que se pagó) — el efecto de la reversa sobre <c>AccountsPayableInstallment</c> es
    /// responsabilidad del caso de uso de Application (mismo principio que <see cref="Create"/>: el
    /// agregado no conoce <c>AccountsPayable</c>, solo publica el evento para que Application y
    /// Accounting reaccionen).
    /// </summary>
    /// <remarks>
    /// ZH-SUPPLIER-PAYMENT-REVERSAL-SEMANTICS-02B-FINAL — la reversa es SIEMPRE total y SOLO una
    /// corrección documental de una operación que no llegó a ejecutarse: el efectivo nunca se
    /// entregó (sigue en la misma sesión de caja) y la transferencia nunca se debitó. Dinero que sí
    /// salió y luego regresó NO se revierte aquí — será un futuro <c>SupplierPaymentRefund</c>.
    /// Cada fuente debe calificar; si una sola no califica, se rechaza la reversa completa:
    /// fuente de caja ⇒ <paramref name="cashNotDeliveredConfirmed"/> = true y trazabilidad de la
    /// sesión original (<c>CashSessionId</c>/<c>CashMovementId</c>); fuente bancaria ⇒
    /// <paramref name="bankReversalReason"/> obligatorio. Que la sesión original siga abierta y la
    /// controle el usuario lo valida Application (el agregado no conoce Caja).
    /// </remarks>
    public void Reverse(
        string reason,
        Guid reversedBy,
        DateTime reversedAtUtc,
        bool cashNotDeliveredConfirmed = false,
        SupplierPaymentBankReversalReason? bankReversalReason = null
    )
    {
        if (Status != SupplierPaymentStatus.Confirmed)
            throw new InvalidOperationException(
                $"Solo un pago Confirmed puede reversarse (estado actual: {Status})."
            );
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("El motivo del reverso es obligatorio.", nameof(reason));

        var cashLines = _methodLines.Where(l => l.CashRegisterId is not null).ToList();
        var hasBankLines = _methodLines.Any(l => l.CompanyBankAccountId is not null);
        if (cashLines.Any(l => l.CashSessionId is null || l.CashMovementId is null))
            throw new InvalidOperationException(
                "El pago tiene una fuente de efectivo sin sesión de caja registrada: no puede revertirse documentalmente."
            );
        if (cashLines.Count > 0 && !cashNotDeliveredConfirmed)
            throw new InvalidOperationException(
                "Debe confirmar que el efectivo no fue entregado al proveedor y permanece en la misma caja."
            );
        if (hasBankLines && bankReversalReason is null)
            throw new InvalidOperationException(
                "Debe indicar el motivo de la reversa bancaria: la transferencia no se ejecutó, fue rechazada por el banco o fue un error de registro."
            );
        if (bankReversalReason is { } bankReason && !Enum.IsDefined(bankReason))
            throw new ArgumentException("El motivo de la reversa bancaria no es válido.", nameof(bankReversalReason));

        var trimmedReason = reason.Trim();

        Status = SupplierPaymentStatus.Reversed;
        ReversalBankReason = hasBankLines ? bankReversalReason : null;
        ReversalCashNotDeliveredConfirmed = cashLines.Count > 0 ? true : null;
        ReversedAtUtc = reversedAtUtc;
        ReversedBy = reversedBy;
        ReverseReason = trimmedReason;
        SetUpdated(reversedBy);

        RaiseDomainEvent(
            new SupplierPaymentReversedEvent(
                TenantId,
                Id,
                CompanyId,
                SupplierId,
                TotalAmount,
                PaymentDate,
                trimmedReason,
                _methodLines
                    .Select(l => new SupplierPaymentConfirmedMethodLine(
                        l.CompanyBankAccountId,
                        l.CashRegisterId,
                        l.Amount
                    ))
                    .ToList(),
                _applicationLines
                    .Select(l => new SupplierPaymentReversedApplicationLine(
                        l.AccountsPayableInstallmentId,
                        l.AmountApplied
                    ))
                    .ToList()
            )
        );
    }

    /// <summary>
    /// Invariante de agregado completo: suma de medios, suma de aplicaciones y suma de allocations
    /// deben ser todas exactamente <see cref="TotalAmount"/>; cada medio debe quedar distribuido al
    /// 100% entre allocations; cada aplicación debe quedar cubierta al 100% entre allocations.
    /// </summary>
    private void EnsureBalanced()
    {
        var totalMethods = _methodLines.Sum(l => l.Amount);
        if (totalMethods != TotalAmount)
            throw new InvalidOperationException(
                $"La suma de los medios de pago ({totalMethods:F2}) no coincide con el total del pago ({TotalAmount:F2})."
            );

        var totalApplications = _applicationLines.Sum(l => l.AmountApplied);
        if (totalApplications != TotalAmount)
            throw new InvalidOperationException(
                $"La suma de las aplicaciones a cuota ({totalApplications:F2}) no coincide con el total del pago ({TotalAmount:F2})."
            );

        var totalAllocations = _allocationLines.Sum(l => l.Amount);
        if (totalAllocations != TotalAmount)
            throw new InvalidOperationException(
                $"La suma de las distribuciones medio↔cuota ({totalAllocations:F2}) no coincide con el total del pago ({TotalAmount:F2})."
            );

        foreach (var methodLine in _methodLines)
        {
            var distributed = _allocationLines
                .Where(a => a.SupplierPaymentMethodLineId == methodLine.Id)
                .Sum(a => a.Amount);
            if (distributed != methodLine.Amount)
                throw new InvalidOperationException(
                    $"El medio de pago {methodLine.Id} no está distribuido al 100% entre las cuotas aplicadas."
                );
        }

        foreach (var applicationLine in _applicationLines)
        {
            var covered = _allocationLines
                .Where(a => a.SupplierPaymentApplicationLineId == applicationLine.Id)
                .Sum(a => a.Amount);
            if (covered != applicationLine.AmountApplied)
                throw new InvalidOperationException(
                    $"La aplicación {applicationLine.Id} no está cubierta al 100% entre los medios de pago."
                );
        }
    }
}
