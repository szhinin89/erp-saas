using ERP.Domain.Common;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.Events;

namespace ERP.Domain.Modules.Purchases.Entities;

public sealed class PurchaseInvoice : AuditableEntity, ITenantScopedEntity, ICompanyOperationalEntity
{
    public Guid CompanyId { get; private set; }

    /// <summary>
    /// Sucursal de origen de la operación (Branch Ownership) — asignada exclusivamente al crear
    /// el borrador desde <see cref="ICurrentBranch"/> del handler, nunca desde el cliente.
    /// Inmutable tras la creación.
    /// </summary>
    public Guid BranchId { get; private set; }

    public const int InvoiceNumberMaxLen = 30;
    public const int AccessKeyLen = 49;
    public const int DocTypeCodeMaxLen = 5;
    public const int SriPaymentMethodMaxLen = 5;
    public const int SriPaymentMethodNameMaxLen = 100;
    public const int NotesMaxLen = 500;
    public const int CurrencyCodeMaxLen = 3;
    public const int SupplierNameMaxLen = 200;
    public const int SupplierTaxIdMaxLen = 20;
    public const int PurchaseOrderNumMaxLen = 30;

    public Guid SupplierId { get; private set; }
    public string DocTypeCode { get; private set; } = null!;
    public string InvoiceNumber { get; private set; } = null!;
    public DateOnly IssueDate { get; private set; }
    public string? AccessKey { get; private set; }
    public string? AuthorizationNumber { get; private set; }
    public DateTime? AuthorizationDate { get; private set; }
    public string? TaxSupportCode { get; private set; }
    public string? SriPaymentMethodCode { get; private set; }
    public string? SriPaymentMethodName { get; private set; }

    // ── Snapshot proveedor (trazabilidad fiscal) ──────────────────────
    public string SupplierName { get; private set; } = null!;
    public string SupplierTaxId { get; private set; } = null!;

    // ── Moneda ───────────────────────────────────────────────────────
    public string CurrencyCode { get; private set; } = "USD";
    public decimal ExchangeRate { get; private set; } = 1m;

    // ── Referencia a Orden de Compra ─────────────────────────────────
    public Guid? PurchaseOrderId { get; private set; }
    public string? PurchaseOrderNumber { get; private set; }

    public Guid? GlobalWarehouseId { get; private set; }

    public const int PaymentTermNameMaxLen = 120;

    public Guid PaymentTermId { get; private set; }
    public string PaymentTermName { get; private set; } = null!;
    public int PaymentTermInstallments { get; private set; }
    public int PaymentTermDaysBetween { get; private set; }

    public DateOnly? DueDate { get; private set; }
    public string? Notes { get; private set; }
    public PurchaseStatus Status { get; private set; } = PurchaseStatus.Draft;

    // ── Totales snapshot (congelados al confirmar) ────────────────────
    public decimal? ConfirmedSubtotal { get; private set; }
    public decimal? ConfirmedTotalTax { get; private set; }
    public decimal? ConfirmedTotalDiscount { get; private set; }
    public decimal? ConfirmedGrandTotal { get; private set; }

    private readonly List<PurchaseInvoiceDetail> _lines = new();
    public IReadOnlyList<PurchaseInvoiceDetail> Lines => _lines.AsReadOnly();

    private readonly List<PurchasePaymentSchedule> _paymentSchedules = new();
    public IReadOnlyList<PurchasePaymentSchedule> PaymentSchedules => _paymentSchedules.AsReadOnly();

    public decimal Subtotal => ConfirmedSubtotal ?? _lines.Sum(l => l.LineSubtotal);
    public decimal TotalDiscount => ConfirmedTotalDiscount ?? _lines.Sum(l => l.DiscountAmount);
    public decimal TotalIce => _lines.Sum(l => l.IceAmount);
    public decimal TotalVat => _lines.Sum(l => l.VatAmount);
    public decimal TotalTax => TotalIce + TotalVat;
    public decimal TotalFreight => _lines.Sum(l => l.FreightAllocated);
    public decimal TotalOtherCosts => _lines.Sum(l => l.OtherCostsAllocated);
    public decimal GrandTotal => ConfirmedGrandTotal
        ?? (_lines.Sum(l => l.TaxInclusiveTotal) + TotalFreight + TotalOtherCosts);

    public decimal TotalCostValue => _lines.Sum(l => l.TotalLineCost);

    public int CreditTermDays => PaymentTermInstallments * PaymentTermDaysBetween;

    private PurchaseInvoice() { }

    public static PurchaseInvoice CreateDraft(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid supplierId,
        string supplierName,
        string supplierTaxId,
        string docTypeCode,
        string invoiceNumber,
        DateOnly issueDate,
        Guid createdBy,
        Guid paymentTermId,
        string paymentTermName,
        int paymentTermInstallments,
        int paymentTermDaysBetween,
        string? accessKey = null,
        string? authorizationNumber = null,
        DateTime? authorizationDate = null,
        string? taxSupportCode = null,
        string? sriPaymentMethodCode = null,
        string? sriPaymentMethodName = null,
        Guid? globalWarehouseId = null,
        DateOnly? dueDate = null,
        string? notes = null,
        string currencyCode = "USD",
        decimal exchangeRate = 1m,
        Guid? purchaseOrderId = null,
        string? purchaseOrderNumber = null)
    {
        if (branchId == Guid.Empty)
            throw new ArgumentException("La sucursal es obligatoria.", nameof(branchId));
        if (string.IsNullOrWhiteSpace(docTypeCode))
            throw new ArgumentException("El tipo de comprobante es obligatorio.", nameof(docTypeCode));
        if (string.IsNullOrWhiteSpace(invoiceNumber))
            throw new ArgumentException("El número de factura es obligatorio.", nameof(invoiceNumber));
        if (supplierId == Guid.Empty)
            throw new ArgumentException("El proveedor es obligatorio.", nameof(supplierId));
        if (string.IsNullOrWhiteSpace(supplierName))
            throw new ArgumentException("El nombre del proveedor es obligatorio.", nameof(supplierName));
        if (string.IsNullOrWhiteSpace(supplierTaxId))
            throw new ArgumentException("El RUC/CI del proveedor es obligatorio.", nameof(supplierTaxId));
        if (paymentTermId == Guid.Empty)
            throw new ArgumentException("La condición de pago es obligatoria.", nameof(paymentTermId));
        if (paymentTermInstallments < 1)
            throw new ArgumentException("Las cuotas deben ser al menos 1.", nameof(paymentTermInstallments));
        if (exchangeRate <= 0)
            throw new ArgumentException("El tipo de cambio debe ser mayor a cero.", nameof(exchangeRate));

        var inv = new PurchaseInvoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            SupplierId = supplierId,
            SupplierName = supplierName.Trim(),
            SupplierTaxId = supplierTaxId.Trim(),
            DocTypeCode = docTypeCode.Trim(),
            InvoiceNumber = invoiceNumber.Trim(),
            IssueDate = issueDate,
            AccessKey = OptionalCode.Normalize(accessKey),
            AuthorizationNumber = OptionalCode.Normalize(authorizationNumber),
            AuthorizationDate = authorizationDate,
            TaxSupportCode = OptionalCode.Normalize(taxSupportCode),
            SriPaymentMethodCode = OptionalCode.Normalize(sriPaymentMethodCode),
            SriPaymentMethodName = OptionalCode.Normalize(sriPaymentMethodName),
            CurrencyCode = currencyCode.Trim().ToUpperInvariant(),
            ExchangeRate = exchangeRate,
            PurchaseOrderId = purchaseOrderId,
            PurchaseOrderNumber = purchaseOrderNumber?.Trim(),
            GlobalWarehouseId = globalWarehouseId,
            PaymentTermId = paymentTermId,
            PaymentTermName = paymentTermName.Trim(),
            PaymentTermInstallments = paymentTermInstallments,
            PaymentTermDaysBetween = paymentTermDaysBetween,
            DueDate = dueDate,
            Notes = notes?.Trim(),
            Status = PurchaseStatus.Draft,
        };
        inv.SetCreated(createdBy);
        return inv;
    }

    public void UpdateDraft(
        Guid supplierId,
        string supplierName,
        string supplierTaxId,
        string docTypeCode,
        string invoiceNumber,
        DateOnly issueDate,
        Guid updatedBy,
        string? accessKey = null,
        string? authorizationNumber = null,
        DateTime? authorizationDate = null,
        string? taxSupportCode = null,
        string? sriPaymentMethodCode = null,
        string? sriPaymentMethodName = null,
        Guid? globalWarehouseId = null,
        DateOnly? dueDate = null,
        string? notes = null,
        string currencyCode = "USD",
        decimal exchangeRate = 1m,
        Guid? purchaseOrderId = null,
        string? purchaseOrderNumber = null)
    {
        if (Status != PurchaseStatus.Draft)
            throw new InvalidOperationException("Solo se pueden editar compras en estado borrador.");
        if (string.IsNullOrWhiteSpace(docTypeCode))
            throw new ArgumentException("El tipo de comprobante es obligatorio.", nameof(docTypeCode));
        if (string.IsNullOrWhiteSpace(invoiceNumber))
            throw new ArgumentException("El número de factura es obligatorio.", nameof(invoiceNumber));
        if (exchangeRate <= 0)
            throw new ArgumentException("El tipo de cambio debe ser mayor a cero.", nameof(exchangeRate));

        SupplierId = supplierId;
        SupplierName = supplierName.Trim();
        SupplierTaxId = supplierTaxId.Trim();
        DocTypeCode = docTypeCode.Trim();
        InvoiceNumber = invoiceNumber.Trim();
        IssueDate = issueDate;
        AccessKey = OptionalCode.Normalize(accessKey);
        AuthorizationNumber = OptionalCode.Normalize(authorizationNumber);
        AuthorizationDate = authorizationDate;
        TaxSupportCode = OptionalCode.Normalize(taxSupportCode);
        SriPaymentMethodCode = OptionalCode.Normalize(sriPaymentMethodCode);
        SriPaymentMethodName = OptionalCode.Normalize(sriPaymentMethodName);
        CurrencyCode = currencyCode.Trim().ToUpperInvariant();
        ExchangeRate = exchangeRate;
        PurchaseOrderId = purchaseOrderId;
        PurchaseOrderNumber = purchaseOrderNumber?.Trim();
        GlobalWarehouseId = globalWarehouseId;
        DueDate = dueDate;
        Notes = notes?.Trim();
        SetUpdated(updatedBy);
    }

    public void ReplaceLines(IEnumerable<PurchaseInvoiceDetail> lines, Guid updatedBy)
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

        if (_lines.Any(l => l.FreightAllocated > 0 || l.OtherCostsAllocated > 0))
            RedistributeCosts(TotalFreight, TotalOtherCosts);

        SetUpdated(updatedBy);
    }

    public void DistributeCosts(decimal freightCost, decimal otherCosts, Guid updatedBy)
    {
        EnsureDraft();
        if (freightCost < 0) throw new ArgumentException("El flete no puede ser negativo.", nameof(freightCost));
        if (otherCosts < 0) throw new ArgumentException("Los otros costos no pueden ser negativos.", nameof(otherCosts));
        RedistributeCosts(freightCost, otherCosts);
        SetUpdated(updatedBy);
    }

    private void RedistributeCosts(decimal freightCost, decimal otherCosts)
    {
        if (_lines.Count == 0)
        {
            return;
        }

        ProrateCostToLines(freightCost, (line, amount) => line.SetFreightAllocated(amount));
        ProrateCostToLines(otherCosts, (line, amount) => line.SetOtherCostsAllocated(amount));
    }

    private void ProrateCostToLines(decimal totalCost, Action<PurchaseInvoiceDetail, decimal> setter)
    {
        if (totalCost <= 0)
        {
            foreach (var line in _lines) setter(line, 0);
            return;
        }

        var totalBase = _lines.Sum(l => l.LineSubtotal - l.DiscountAmount);
        if (totalBase <= 0)
        {
            var equal = Math.Round(totalCost / _lines.Count, 6);
            foreach (var line in _lines) setter(line, equal);
        }
        else
        {
            decimal allocated = 0;
            for (var i = 0; i < _lines.Count; i++)
            {
                var line = _lines[i];
                var lineBase = line.LineSubtotal - line.DiscountAmount;
                if (i == _lines.Count - 1)
                {
                    setter(line, totalCost - allocated);
                }
                else
                {
                    var share = Math.Round(totalCost * lineBase / totalBase, 6);
                    setter(line, share);
                    allocated += share;
                }
            }
        }
    }

    public void Confirm(Guid updatedBy)
    {
        EnsureDraft();
        if (_lines.Count == 0)
            throw new InvalidOperationException("No se puede confirmar una compra sin líneas.");

        foreach (var line in _lines)
        {
            if (line.Quantity <= 0)
                throw new InvalidOperationException($"Línea '{line.Description}': cantidad debe ser mayor a cero.");
            if (string.IsNullOrWhiteSpace(line.VatCode))
                throw new InvalidOperationException($"Línea '{line.Description}': código IVA es obligatorio.");
            if (line.ItemId.HasValue && line.WarehouseId is null && GlobalWarehouseId is null)
                throw new InvalidOperationException($"Línea '{line.Description}': el producto requiere una bodega destino.");
        }

        // Líneas que provienen de Recepción Electrónica (PurchaseReceptionLineId != null) y aún no
        // tienen ItemId resuelto son, por construcción, Item Matching pendiente (Pending/NeedsReview)
        // — no se confirma la compra, para no generar Inventario/Kardex/CxP/Contabilidad con un
        // vínculo de producto sin resolver. Líneas manuales (PurchaseReceptionLineId == null) siguen
        // permitiendo ItemId nulo, igual que antes — ese caso no es "matching pendiente".
        var unresolved = _lines.Where(l => l.PurchaseReceptionLineId.HasValue && l.ItemId is null).ToList();
        if (unresolved.Count > 0)
        {
            var names = string.Join(", ", unresolved.Select(l => l.Description));
            throw new InvalidOperationException(
                $"No se puede confirmar la compra: hay productos pendientes de vinculación ({names}).");
        }

        foreach (var line in _lines)
            line.FreezeCosts();

        ConfirmedSubtotal = _lines.Sum(l => l.LineSubtotal);
        ConfirmedTotalDiscount = _lines.Sum(l => l.DiscountAmount);
        ConfirmedTotalTax = _lines.Sum(l => l.IceAmount) + _lines.Sum(l => l.VatAmount);
        ConfirmedGrandTotal = _lines.Sum(l => l.TaxInclusiveTotal) + TotalFreight + TotalOtherCosts;

        Status = PurchaseStatus.Confirmed;
        SetUpdated(updatedBy);
        RaiseDomainEvent(new PurchaseInvoiceConfirmedEvent(
            TenantId, Id, SupplierId, InvoiceNumber, GrandTotal, CompanyId, IssueDate,
            Subtotal, TotalVat, TotalIce, TotalDiscount));
    }

    /// <summary>
    /// Registra que la confirmación de esta compra actualizó el precio base (SSOT) del ítem
    /// <paramref name="itemId"/> — el propio agregado Item se muta aparte vía su repositorio;
    /// este método solo levanta el evento de auditoría correspondiente a esa decisión de negocio.
    /// </summary>
    public void RecordConfirmedItemPvpUpdate(Guid itemId, decimal oldPvp, decimal newPvp)
        => RaiseDomainEvent(new PurchaseLinePvpUpdatedEvent(
            TenantId, Id, InvoiceNumber, itemId, oldPvp, newPvp, "ConfirmedUpdate"));

    /// <summary>
    /// Edita el PVP snapshot de una línea en borrador. Único punto autorizado de mutación —
    /// reemplaza el acceso directo a <see cref="PurchaseInvoiceDetail.SetItemPvpSnapshot"/> desde
    /// Application, que violaba el límite del agregado.
    /// </summary>
    public void UpdateLinePvp(Guid lineId, decimal newPvp, Guid updatedBy)
    {
        EnsureDraft();
        var line = _lines.FirstOrDefault(l => l.Id == lineId)
            ?? throw new InvalidOperationException("Línea no encontrada.");

        var oldPvp = line.SnapshotItemPvp;
        line.SetItemPvpSnapshot(newPvp);
        SetUpdated(updatedBy);

        if (line.ItemId is Guid itemId)
            RaiseDomainEvent(new PurchaseLinePvpUpdatedEvent(
                TenantId, Id, InvoiceNumber, itemId, oldPvp, newPvp, "Updated"));
    }

    public const int CancelReasonMaxLen = 500;

    public string? CancelReason { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public Guid? CancelledBy { get; private set; }

    public void Cancel(string reason, Guid cancelledBy)
    {
        if (Status != PurchaseStatus.Confirmed)
            throw new InvalidOperationException("Solo se pueden anular compras confirmadas.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("El motivo de anulación es obligatorio.", nameof(reason));

        Status = PurchaseStatus.Cancelled;
        CancelReason = reason.Trim();
        CancelledAt = DateTime.UtcNow;
        CancelledBy = cancelledBy;
        SetUpdated(cancelledBy);
        RaiseDomainEvent(new PurchaseInvoiceCancelledEvent(TenantId, Id, SupplierId, InvoiceNumber, GrandTotal, CancelReason));
    }

    public void UpdatePaymentTermSnapshot(Guid paymentTermId, string paymentTermName, int installments, int daysBetween)
    {
        EnsureDraft();
        if (_paymentSchedules.Count > 0)
            throw new InvalidOperationException("No se puede cambiar la condición de pago después de generar el cronograma.");

        PaymentTermId = paymentTermId;
        PaymentTermName = paymentTermName.Trim();
        PaymentTermInstallments = installments;
        PaymentTermDaysBetween = daysBetween;
    }

    public void GeneratePaymentSchedule()
    {
        if (_paymentSchedules.Count > 0)
            throw new InvalidOperationException(
                "El cronograma de pagos ya fue generado. No se permite regeneración.");

        if (GrandTotal <= 0)
            throw new InvalidOperationException(
                "No se puede generar cronograma para una compra con total cero o negativo.");

        if (PaymentTermInstallments < 1)
            throw new InvalidOperationException(
                "La condición de pago debe tener al menos 1 cuota.");

        var total = GrandTotal;
        var installmentAmount = Math.Round(total / PaymentTermInstallments, 2);
        decimal accumulated = 0;

        for (var i = 1; i <= PaymentTermInstallments; i++)
        {
            var dueDate = IssueDate.AddDays(PaymentTermDaysBetween * i);
            var amount = i == PaymentTermInstallments
                ? total - accumulated
                : installmentAmount;

            _paymentSchedules.Add(PurchasePaymentSchedule.Create(
                Id, TenantId, i, dueDate, amount));
            accumulated += amount;
        }

        ValidateScheduleTotal(total);
    }

    public void ReplacePaymentSchedule(
        IReadOnlyList<(int Number, DateOnly DueDate, decimal Amount, string? Notes)> installments)
    {
        if (installments.Count == 0)
            throw new ArgumentException("Debe incluir al menos una cuota.", nameof(installments));

        var total = GrandTotal;
        if (total <= 0)
            throw new InvalidOperationException(
                "No se puede generar cronograma para una compra con total cero o negativo.");

        var numbers = new HashSet<int>();
        foreach (var inst in installments)
        {
            if (inst.Number < 1)
                throw new ArgumentException($"El número de cuota debe ser >= 1 (recibido: {inst.Number}).");
            if (inst.Amount <= 0)
                throw new ArgumentException($"Cuota #{inst.Number}: el monto debe ser mayor a cero.");
            if (inst.DueDate < IssueDate)
                throw new ArgumentException($"Cuota #{inst.Number}: la fecha de vencimiento no puede ser anterior a la fecha de emisión.");
            if (!numbers.Add(inst.Number))
                throw new ArgumentException($"Cuota #{inst.Number}: número de cuota duplicado.");
        }

        var sum = installments.Sum(i => i.Amount);
        if (sum != total)
            throw new InvalidOperationException(
                $"La suma de las cuotas ({sum:F2}) no coincide con el total de la compra ({total:F2}).");

        _paymentSchedules.Clear();
        foreach (var inst in installments.OrderBy(i => i.Number))
        {
            _paymentSchedules.Add(PurchasePaymentSchedule.Create(
                Id, TenantId, inst.Number, inst.DueDate, inst.Amount, inst.Notes));
        }
    }

    private void ValidateScheduleTotal(decimal expectedTotal)
    {
        var sum = _paymentSchedules.Sum(s => s.Amount);
        if (sum != expectedTotal)
            throw new InvalidOperationException(
                $"La suma de las cuotas ({sum:F2}) no coincide con el total de la compra ({expectedTotal:F2}).");
    }

    private void EnsureDraft()
    {
        if (Status != PurchaseStatus.Draft)
            throw new InvalidOperationException("Solo se pueden editar compras en estado borrador.");
    }
}
