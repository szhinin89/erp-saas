using ERP.Domain.Common;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Domain.Modules.Sales.Events;
using ERP.Domain.Modules.Sales.ValueObjects;

namespace ERP.Domain.Modules.Sales.Entities;

public sealed class SalesInvoice : AuditableEntity, ITenantScopedEntity, ICompanyOperationalEntity
{
    public Guid CompanyId { get; private set; }

    /// <summary>
    /// Sucursal de origen de la operación (Branch Ownership) — asignada exclusivamente al crear
    /// el borrador desde <see cref="ICurrentBranch"/> del handler, nunca desde el cliente.
    /// Inmutable tras la creación: no existe setter público ni método ChangeBranch. Representa
    /// dónde ocurrió la operación históricamente, distinto del contexto de sesión activo del
    /// usuario (que puede cambiar después de creado el documento).
    /// </summary>
    public Guid BranchId { get; private set; }

    /// <summary>
    /// Caja operativa que originó la venta (ADR — Rediseño del módulo de Caja, Fase 4) —
    /// asignada exclusivamente al crear el borrador desde <c>ICurrentCashSession</c> del handler,
    /// nunca desde el cliente. Inmutable tras la creación: no existe setter público ni método
    /// ChangeCashSession. La autorización y el registro de movimientos de caja usan este valor
    /// directamente — ya no se busca la sesión abierta del usuario ad-hoc.
    /// </summary>
    public Guid CashSessionId { get; private set; }

    public const int InvoiceNumberMaxLen = 30;
    public const int DocTypeCodeMaxLen   = 5;
    public const int NotesMaxLen         = 500;
    public const int CurrencyCodeMaxLen  = 3;
    public const int CancelReasonMaxLen  = 500;

    // ── Documento ───────────────────────────────────────────────────
    // Default: SRI código "01" = Factura (fuente de verdad: tabla sri_doc_types)
    public string            DocTypeCode       { get; private set; } = "01";
    public string            InvoiceNumber     { get; private set; } = null!;
    public DateOnly          IssueDate         { get; private set; }
    public Guid?             EmissionPointId   { get; private set; }
    public EmissionType      EmissionType      { get; private set; }

    // ── Método de pago SRI (fiscal — fuente de verdad: tabla sri_payment_methods) ──
    public string?           SriPaymentMethodCode { get; private set; }

    // ── Cliente (snapshot fiscal — SRI exige datos al momento de emisión) ──
    public Guid              CustomerId        { get; private set; }
    public CustomerSnapshot  Customer          { get; private set; } = null!;

    // ── Moneda ──────────────────────────────────────────────────────
    public string            CurrencyCode      { get; private set; } = "USD";
    public decimal           ExchangeRate      { get; private set; } = 1m;

    // ── Condición de pago (snapshot) ────────────────────────────────
    public PaymentTermSnapshot PaymentTerm     { get; private set; } = null!;

    public DateOnly?         DueDate           { get; private set; }
    public string?           Notes             { get; private set; }

    // ── Estado interno (ciclo de vida del documento) ────────────────
    public SalesInvoiceStatus Status           { get; private set; } = SalesInvoiceStatus.Draft;

    // ── Totales snapshot (congelados al autorizar) ──────────────────
    public decimal?          AuthorizedSubtotal       { get; private set; }
    public decimal?          AuthorizedTotalTax       { get; private set; }
    public decimal?          AuthorizedTotalDiscount  { get; private set; }
    public decimal?          AuthorizedGrandTotal     { get; private set; }

    private readonly List<SalesInvoiceDetail> _lines = new();
    public IReadOnlyList<SalesInvoiceDetail> Lines => _lines.AsReadOnly();

    private readonly List<SalesInvoicePayment> _payments = new();
    public IReadOnlyList<SalesInvoicePayment> Payments => _payments.AsReadOnly();

    // ── Calculated (NOT persisted) ──────────────────────────────────
    public decimal Subtotal      => AuthorizedSubtotal ?? _lines.Sum(l => l.LineSubtotal);
    public decimal TotalDiscount => AuthorizedTotalDiscount ?? _lines.Sum(l => l.DiscountAmount);
    public decimal TotalIce      => _lines.Sum(l => l.IceAmount);
    public decimal TotalVat      => _lines.Sum(l => l.VatAmount);
    public decimal TotalTax      => AuthorizedTotalTax ?? (TotalIce + TotalVat);
    public decimal GrandTotal    => AuthorizedGrandTotal ?? _lines.Sum(l => l.TaxInclusiveTotal);
    public int     CreditTermDays => PaymentTerm.CreditTermDays;

    // ── Anulación ───────────────────────────────────────────────────
    public string?   CancelReason  { get; private set; }
    public DateTime? CancelledAt   { get; private set; }
    public Guid?     CancelledBy   { get; private set; }

    private SalesInvoice() { }

    // ── Factory ─────────────────────────────────────────────────────
    public static SalesInvoice CreateDraft(
        Guid               tenantId,
        Guid               companyId,
        Guid               branchId,
        Guid               customerId,
        CustomerSnapshot   customer,
        string             invoiceNumber,
        DateOnly           issueDate,
        Guid               createdBy,
        PaymentTermSnapshot paymentTerm,
        Guid               cashSessionId,
        string             docTypeCode     = "01",
        Guid?              emissionPointId = null,
        EmissionType       emissionType    = EmissionType.Electronic,
        DateOnly?          dueDate         = null,
        string?            notes           = null,
        string             currencyCode    = "USD",
        decimal            exchangeRate    = 1m,
        string?            sriPaymentMethodCode = null)
    {
        if (branchId == Guid.Empty)
            throw new ArgumentException("La sucursal es obligatoria.", nameof(branchId));
        if (customerId == Guid.Empty)
            throw new ArgumentException("El cliente es obligatorio.", nameof(customerId));
        if (string.IsNullOrWhiteSpace(invoiceNumber))
            throw new ArgumentException("El número de factura es obligatorio.", nameof(invoiceNumber));
        if (string.IsNullOrWhiteSpace(docTypeCode))
            throw new ArgumentException("El tipo de comprobante es obligatorio.", nameof(docTypeCode));
        if (exchangeRate <= 0)
            throw new ArgumentException("El tipo de cambio debe ser mayor a cero.", nameof(exchangeRate));
        if (cashSessionId == Guid.Empty)
            throw new ArgumentException("La caja abierta es obligatoria.", nameof(cashSessionId));

        var inv = new SalesInvoice
        {
            Id               = Guid.NewGuid(),
            TenantId         = tenantId,
            CompanyId        = companyId,
            BranchId         = branchId,
            CashSessionId    = cashSessionId,
            DocTypeCode      = docTypeCode.Trim(),
            InvoiceNumber    = invoiceNumber.Trim(),
            IssueDate        = issueDate,
            EmissionPointId  = emissionPointId,
            EmissionType     = emissionType,
            CustomerId       = customerId,
            Customer         = customer,
            CurrencyCode     = currencyCode.Trim().ToUpperInvariant(),
            ExchangeRate     = exchangeRate,
            PaymentTerm      = paymentTerm,
            SriPaymentMethodCode = OptionalCode.Normalize(sriPaymentMethodCode),
            DueDate          = dueDate,
            Notes            = notes?.Trim(),
            Status           = SalesInvoiceStatus.Draft,
        };
        inv.SetCreated(createdBy);
        return inv;
    }

    // ── Update Draft ────────────────────────────────────────────────
    public void UpdateDraft(
        Guid             customerId,
        CustomerSnapshot customer,
        DateOnly         issueDate,
        Guid             updatedBy,
        DateOnly?        dueDate      = null,
        string?          notes        = null,
        string           currencyCode = "USD",
        decimal          exchangeRate = 1m)
    {
        EnsureDraft();
        if (customerId == Guid.Empty)
            throw new ArgumentException("El cliente es obligatorio.", nameof(customerId));
        if (exchangeRate <= 0)
            throw new ArgumentException("El tipo de cambio debe ser mayor a cero.", nameof(exchangeRate));

        CustomerId   = customerId;
        Customer     = customer;
        IssueDate    = issueDate;
        CurrencyCode = currencyCode.Trim().ToUpperInvariant();
        ExchangeRate = exchangeRate;
        DueDate      = dueDate;
        Notes        = notes?.Trim();
        SetUpdated(updatedBy);
    }

    // ── Lines ───────────────────────────────────────────────────────
    public void ReplaceLines(IEnumerable<SalesInvoiceDetail> lines, Guid updatedBy)
    {
        EnsureDraft();
        _lines.Clear();
        short order = 1;
        foreach (var line in lines)
        {
            line.SetSortOrder(order++);
            _lines.Add(line);
        }
        SetUpdated(updatedBy);
    }

    public void ApplyGlobalDiscount(decimal pct, Guid updatedBy)
    {
        EnsureDraft();
        if (pct is < 0 or > 100)
            throw new ArgumentException("El descuento debe estar entre 0 y 100.", nameof(pct));
        foreach (var line in _lines)
            line.ApplyDiscount(pct);
        SetUpdated(updatedBy);
    }

    // ── Payment Term Update ─────────────────────────────────────────
    public void UpdatePaymentTerm(PaymentTermSnapshot paymentTerm)
    {
        EnsureDraft();
        PaymentTerm = paymentTerm ?? throw new ArgumentNullException(nameof(paymentTerm));
    }

    // ── Formas de cobro (N pagos por factura) ─────────────────────────
    public void ReplacePayments(IEnumerable<SalesInvoicePayment> payments, Guid updatedBy)
    {
        EnsureDraft();
        _payments.Clear();
        foreach (var p in payments)
            _payments.Add(p);
        SetUpdated(updatedBy);
    }

    // ── Authorize (final — immutable after this) ────────────────────
    public void Authorize(Guid updatedBy)
    {
        EnsureDraft();
        if (_lines.Count == 0)
            throw new InvalidOperationException(
                "No puedes emitir esta factura porque no tiene productos ni servicios agregados. Agrega al menos una línea antes de emitir.");
        if (_payments.Count == 0)
            throw new InvalidOperationException(
                "No puedes emitir esta factura porque todavía no has registrado una forma de pago. Ve a la sección 'Formas de pago' y agrega al menos un método (efectivo, tarjeta, transferencia o crédito) antes de emitir.");

        foreach (var line in _lines)
        {
            if (line.Quantity <= 0)
                throw new InvalidOperationException($"Línea '{line.Description}': cantidad debe ser mayor a cero.");
            if (string.IsNullOrWhiteSpace(line.VatCode))
                throw new InvalidOperationException(
                    $"El producto '{line.Description}' no tiene un código de IVA configurado. Ve al maestro de productos y asigna una tarifa de IVA antes de venderlo.");
        }

        foreach (var line in _lines)
            line.Freeze();

        AuthorizedSubtotal      = _lines.Sum(l => l.LineSubtotal);
        AuthorizedTotalDiscount = _lines.Sum(l => l.DiscountAmount);
        AuthorizedTotalTax      = _lines.Sum(l => l.IceAmount) + _lines.Sum(l => l.VatAmount);
        AuthorizedGrandTotal    = _lines.Sum(l => l.TaxInclusiveTotal);

        if (AuthorizedGrandTotal <= 0)
            throw new InvalidOperationException(
                "No puedes emitir esta factura porque su total es $0 o negativo. Revisa las cantidades, precios y descuentos de las líneas antes de emitir.");

        var paymentSum = _payments.Sum(p => p.Amount);
        if (Math.Abs(paymentSum - AuthorizedGrandTotal.Value) > 0.01m)
            throw new InvalidOperationException(
                $"El total de los pagos ingresados (${paymentSum:F2}) no coincide con el total de la factura (${AuthorizedGrandTotal.Value:F2}). " +
                "Ajusta los montos en 'Formas de pago' hasta que coincidan con el total, o agrega el valor faltante.");

        Status = SalesInvoiceStatus.Authorized;
        SetUpdated(updatedBy);

        RaiseDomainEvent(new SalesInvoiceAuthorizedEvent(
            Id, InvoiceNumber, AuthorizedGrandTotal!.Value, updatedBy, CashSessionId,
            TenantId, CompanyId, IssueDate,
            Subtotal, TotalVat, TotalIce, TotalDiscount));
    }

    public const string PendingNumberPlaceholder = "PENDING";
    private const string DraftNumberPrefix = "DRAFT-";

    // ── Set InvoiceNumber (one-time assignment — irreversible) ──────
    public void SetInvoiceNumber(string invoiceNumber)
    {
        if (!string.IsNullOrWhiteSpace(InvoiceNumber)
            && InvoiceNumber != PendingNumberPlaceholder
            && !InvoiceNumber.StartsWith(DraftNumberPrefix, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "El número de factura ya fue asignado y no puede modificarse.");
        if (string.IsNullOrWhiteSpace(invoiceNumber))
            throw new ArgumentException("El número de factura es obligatorio.", nameof(invoiceNumber));
        InvoiceNumber = invoiceNumber.Trim();
    }

    // ── Cancel ──────────────────────────────────────────────────────
    public void Cancel(string reason, Guid cancelledBy)
    {
        if (Status != SalesInvoiceStatus.Authorized)
            throw new InvalidOperationException("Solo se pueden anular facturas autorizadas.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("El motivo de anulación es obligatorio.", nameof(reason));

        Status       = SalesInvoiceStatus.Cancelled;
        CancelReason = reason.Trim();
        CancelledAt  = DateTime.UtcNow;
        CancelledBy  = cancelledBy;
        SetUpdated(cancelledBy);
    }

    // ── Guards ──────────────────────────────────────────────────────
    private void EnsureDraft()
    {
        if (Status != SalesInvoiceStatus.Draft)
            throw new InvalidOperationException(
                "Esta factura ya no está en borrador (fue autorizada o anulada), por lo que no se puede modificar. " +
                "Si necesitas corregir algo, contacta a administración o registra una factura nueva.");
    }
}
