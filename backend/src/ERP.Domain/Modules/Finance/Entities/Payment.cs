using ERP.Domain.Common;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Finance.Events;

namespace ERP.Domain.Modules.Finance.Entities;

/// <summary>
/// Liquidación de AR/AP (Fase 5.5.5.2) — agregado raíz independiente de <c>SalesReceivable</c> y
/// <c>PurchasePayable</c> (decisión de la auditoría Fase 5.5.5.1): un mismo cobro/pago puede
/// aplicarse contra varios documentos (facturas/cuotas) de forma parcial, lo cual no tiene lugar
/// natural dentro de la propia CxC/CxP sin convertirlas en aggregate roots sobrecargados. Mismo
/// patrón de ciclo de vida que <c>JournalEntry</c> (ADR-026 Fase 5.2/5.4): Draft mientras se
/// agregan líneas vía <see cref="AddApplicationLine"/>, <see cref="Apply"/> valida el invariante
/// de balance y publica el hecho de negocio, <see cref="Reverse"/> es la única vía para invalidar
/// un pago ya aplicado.
/// </summary>
/// <remarks>
/// Este aggregate NUNCA referencia instancias de <c>SalesReceivable</c>/<c>PurchasePayable</c> —
/// solo sus Id, vía <see cref="PaymentApplicationLine"/>. Incrementar/decrementar
/// <c>PaidAmount</c> en esos aggregates (<c>RegisterCollection</c>/<c>RegisterPayment</c>/
/// <c>ReverseCollection</c>/<c>ReversePayment</c>) es responsabilidad del caso de uso de
/// Application que coordina ambos aggregates reaccionando a los eventos de este — Application no
/// existe todavía en esta fase (Domain-only, Fase 5.5.5.2).
/// </remarks>
public sealed class Payment : AuditableEntity, ITenantScopedEntity, ICompanyOperationalEntity
{
    public Guid CompanyId { get; private set; }
    public PaymentDirection Direction { get; private set; }

    /// <summary>BusinessPartner aplicable: cliente si <see cref="Direction"/> es Collection, proveedor si es Payment.</summary>
    public Guid PartnerId { get; private set; }

    public decimal Amount { get; private set; }
    public DateOnly PaymentDate { get; private set; }

    /// <summary>Catálogo <c>PaymentMethod</c> (Sales) — referencia por Id, sin navegación de dominio (mismo criterio que AccountingPeriodId en JournalEntry).</summary>
    public Guid? PaymentMethodId { get; private set; }

    /// <summary>
    /// ACCOUNTING-PAYMENT-METHOD-ACCOUNT-MAPPING-14 — destino financiero real (caja/banco,
    /// <c>CompanyFinancialDestination</c>) que recibió/originó el efectivo de este cobro/pago,
    /// referencia por Id sin navegación de dominio (mismo criterio que <see cref="PaymentMethodId"/>).
    /// Opcional: un pago sin destino específico sigue contabilizando con la cuenta fija de la
    /// <c>PostingRule</c> (comportamiento previo, sin cambios) — ver <see cref="Events.CollectionAppliedEvent"/>/
    /// <see cref="Events.SupplierPaymentAppliedEvent"/> para cómo Accounting lo consume.
    /// </summary>
    public Guid? FinancialDestinationId { get; private set; }

    public string? Reference { get; private set; }
    public PaymentStatus Status { get; private set; }
    public DateTime? AppliedAtUtc { get; private set; }
    public DateTime? ReversedAtUtc { get; private set; }
    public string? ReverseReason { get; private set; }

    private readonly List<PaymentApplicationLine> _lines = new();
    public IReadOnlyCollection<PaymentApplicationLine> Lines => _lines.AsReadOnly();

    private Payment() { }

    public static Payment Create(
        Guid tenantId,
        Guid companyId,
        PaymentDirection direction,
        Guid partnerId,
        decimal amount,
        DateOnly paymentDate,
        Guid? paymentMethodId,
        string? reference,
        Guid createdBy,
        Guid? financialDestinationId = null
    )
    {
        if (partnerId == Guid.Empty)
            throw new ArgumentException(
                "El cliente o proveedor es obligatorio.",
                nameof(partnerId)
            );
        if (amount <= 0)
            throw new ArgumentException("El monto del pago debe ser mayor a cero.", nameof(amount));

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            Direction = direction,
            PartnerId = partnerId,
            Amount = amount,
            PaymentDate = paymentDate,
            PaymentMethodId = paymentMethodId,
            FinancialDestinationId = financialDestinationId,
            Reference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim(),
            Status = PaymentStatus.Draft,
        };
        payment.SetCreated(createdBy);
        return payment;
    }

    /// <summary>
    /// Agrega una línea de aplicación contra un documento de AR/AP. El documento referenciado
    /// (<paramref name="documentId"/>) debe corresponder a <c>SalesReceivable.Id</c> si
    /// <see cref="Direction"/> es Collection, o a <c>PurchasePayable.Id</c> si es Payment — este
    /// método decide cuál de los dos IDs de <see cref="PaymentApplicationLine"/> asignar según la
    /// dirección propia, para que no exista forma de mezclar ambos tipos de documento bajo un
    /// mismo pago. Solo permitido mientras el pago está en <see cref="PaymentStatus.Draft"/> — no
    /// valida balance en cada llamada (ver <see cref="Apply"/> para el invariante de agregado).
    /// </summary>
    public void AddApplicationLine(Guid documentId, Guid? installmentId, decimal appliedAmount)
    {
        if (Status != PaymentStatus.Draft)
            throw new InvalidOperationException(
                $"Solo se pueden agregar líneas de aplicación mientras el pago está en Draft (estado actual: {Status})."
            );

        var line =
            Direction == PaymentDirection.Collection
                ? PaymentApplicationLine.CreateForReceivable(
                    Id,
                    TenantId,
                    documentId,
                    installmentId,
                    appliedAmount,
                    (short)_lines.Count
                )
                : PaymentApplicationLine.CreateForPayable(
                    Id,
                    TenantId,
                    documentId,
                    installmentId,
                    appliedAmount,
                    (short)_lines.Count
                );

        _lines.Add(line);
    }

    /// <summary>
    /// Invariante de balance del agregado: Σ AppliedAmount de las líneas == <see cref="Amount"/>.
    /// Mismo criterio que <c>JournalEntry.EnsureBalanced()</c> — un pago sin líneas cumple
    /// trivialmente solo si <see cref="Amount"/> también es 0, lo cual <c>Create()</c> ya impide
    /// (<c>amount &gt; 0</c> obligatorio), por lo que un pago Draft sin líneas nunca pasa este
    /// chequeo — <see cref="Apply"/> exige además al menos una línea.
    /// </summary>
    public void EnsureBalanced()
    {
        var totalApplied = _lines.Sum(l => l.AppliedAmount);
        if (totalApplied != Amount)
            throw new InvalidOperationException(
                $"El pago no está balanceado: aplicado ({totalApplied:F2}) distinto del monto del pago ({Amount:F2})."
            );
    }

    /// <summary>
    /// Aplicación definitiva del pago (Draft -&gt; Applied): exige al menos una línea, valida el
    /// balance y publica <see cref="CollectionAppliedEvent"/>/<see cref="SupplierPaymentAppliedEvent"/>
    /// según <see cref="Direction"/>. Un pago desbalanceado o sin líneas nunca llega a Applied.
    /// </summary>
    public void Apply(Guid appliedBy)
    {
        if (Status != PaymentStatus.Draft)
            throw new InvalidOperationException(
                $"Solo un pago en estado Draft puede aplicarse (estado actual: {Status})."
            );
        if (_lines.Count == 0)
            throw new InvalidOperationException(
                "El pago debe tener al menos una línea de aplicación."
            );

        EnsureBalanced();

        Status = PaymentStatus.Applied;
        AppliedAtUtc = DateTime.UtcNow;
        SetUpdated(appliedBy);

        RaiseDomainEvent(
            Direction == PaymentDirection.Collection
                ? new CollectionAppliedEvent(
                    TenantId,
                    Id,
                    CompanyId,
                    PartnerId,
                    Amount,
                    PaymentDate,
                    FinancialDestinationId
                )
                : new SupplierPaymentAppliedEvent(
                    TenantId,
                    Id,
                    CompanyId,
                    PartnerId,
                    Amount,
                    PaymentDate,
                    FinancialDestinationId
                )
        );
    }

    /// <summary>
    /// Reverso del pago (Fase 5.5.5.2): solo un pago <see cref="PaymentStatus.Applied"/> puede
    /// reversarse — impide tanto reversar un Draft (nunca tuvo efecto real) como reversar dos
    /// veces (el mismo chequeo de estado lo bloquea, sin bandera adicional). A diferencia de
    /// <c>JournalEntry.Reverse()</c>, no crea un segundo <c>Payment</c> "espejo": el reverso de
    /// una liquidación no requiere partida doble, solo invalidar el efecto y notificarlo — el
    /// caso de uso de Application (fase futura) es quien decrementa <c>PaidAmount</c> en cada
    /// documento afectado vía <c>ReverseCollection</c>/<c>ReversePayment</c>.
    /// </summary>
    public void Reverse(Guid reversedBy, string reason)
    {
        if (Status != PaymentStatus.Applied)
            throw new InvalidOperationException(
                $"Solo un pago Applied puede reversarse (estado actual: {Status})."
            );
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("El motivo del reverso es obligatorio.", nameof(reason));

        var trimmedReason = reason.Trim();

        Status = PaymentStatus.Reversed;
        ReversedAtUtc = DateTime.UtcNow;
        ReverseReason = trimmedReason;
        SetUpdated(reversedBy);

        RaiseDomainEvent(
            Direction == PaymentDirection.Collection
                ? new CollectionReversedEvent(
                    TenantId,
                    Id,
                    CompanyId,
                    PartnerId,
                    Amount,
                    trimmedReason
                )
                : new SupplierPaymentReversedEvent(
                    TenantId,
                    Id,
                    CompanyId,
                    PartnerId,
                    Amount,
                    trimmedReason
                )
        );
    }
}
