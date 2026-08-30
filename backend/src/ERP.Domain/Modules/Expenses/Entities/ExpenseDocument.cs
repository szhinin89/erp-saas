using ERP.Domain.Common;
using ERP.Domain.Modules.Expenses.Enums;
using ERP.Domain.Modules.Expenses.Events;

namespace ERP.Domain.Modules.Expenses.Entities;

public sealed class ExpenseDocument : AuditableEntity, ITenantScopedEntity, ICompanyOperationalEntity
{
    public const int DocumentTypeMaxLen = 5;
    public const int DocumentNumberMaxLen = 30;
    public const int AuthorizationNumberMaxLen = 49;
    public const int SupplierNameMaxLen = 200;
    public const int SupplierTaxIdMaxLen = 20;
    public const int PaymentTermNameMaxLen = 120;
    public const int NotesMaxLen = 500;

    public Guid CompanyId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid SupplierId { get; private set; }
    public string SupplierName { get; private set; } = null!;
    public string SupplierTaxId { get; private set; } = null!;
    public DateOnly IssueDate { get; private set; }
    public DateOnly AccountingDate { get; private set; }
    public string DocumentType { get; private set; } = null!;
    public string DocumentNumber { get; private set; } = null!;
    public string? AuthorizationNumber { get; private set; }
    public DateTime? AuthorizationDate { get; private set; }
    public Guid PaymentTermId { get; private set; }
    public string PaymentTermName { get; private set; } = null!;
    public int PaymentTermInstallments { get; private set; }
    public int PaymentTermDaysBetween { get; private set; }
    public DateOnly? DueDate { get; private set; }
    public string? Notes { get; private set; }
    public ExpenseStatus Status { get; private set; } = ExpenseStatus.Draft;

    public decimal? ConfirmedSubtotal { get; private set; }
    public decimal? ConfirmedTotalTax { get; private set; }
    public decimal? ConfirmedTotalDiscount { get; private set; }
    public decimal? ConfirmedGrandTotal { get; private set; }

    public const int CancelReasonMaxLen = 500;

    public string? CancelReason { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public Guid? CancelledBy { get; private set; }

    private readonly List<ExpenseLine> _lines = new();
    public IReadOnlyList<ExpenseLine> Lines => _lines.AsReadOnly();

    private readonly List<ExpensePaymentSchedule> _paymentSchedules = new();
    public IReadOnlyList<ExpensePaymentSchedule> PaymentSchedules => _paymentSchedules.AsReadOnly();

    public decimal Subtotal => ConfirmedSubtotal ?? _lines.Sum(l => l.LineSubtotal);
    public decimal TotalDiscount => ConfirmedTotalDiscount ?? _lines.Sum(l => l.DiscountAmount);
    public decimal TotalVat => _lines.Sum(l => l.VatAmount);
    public decimal TotalTax => ConfirmedTotalTax ?? TotalVat;
    public decimal GrandTotal => ConfirmedGrandTotal ?? _lines.Sum(l => l.TaxInclusiveTotal);

    private ExpenseDocument() { }

    public static ExpenseDocument CreateDraft(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid supplierId,
        string supplierName,
        string supplierTaxId,
        DateOnly issueDate,
        DateOnly accountingDate,
        string documentType,
        string documentNumber,
        Guid paymentTermId,
        string paymentTermName,
        int paymentTermInstallments,
        int paymentTermDaysBetween,
        Guid createdBy,
        string? authorizationNumber = null,
        DateTime? authorizationDate = null,
        DateOnly? dueDate = null,
        string? notes = null
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
        if (string.IsNullOrWhiteSpace(supplierName))
            throw new ArgumentException("El nombre del proveedor es obligatorio.", nameof(supplierName));
        if (string.IsNullOrWhiteSpace(supplierTaxId))
            throw new ArgumentException("El RUC/CI del proveedor es obligatorio.", nameof(supplierTaxId));
        if (string.IsNullOrWhiteSpace(documentType))
            throw new ArgumentException("El tipo de documento es obligatorio.", nameof(documentType));
        if (string.IsNullOrWhiteSpace(documentNumber))
            throw new ArgumentException("El número de documento es obligatorio.", nameof(documentNumber));
        if (paymentTermId == Guid.Empty)
            throw new ArgumentException("La condición de pago es obligatoria.", nameof(paymentTermId));
        if (paymentTermInstallments < 1)
            throw new ArgumentException("Las cuotas deben ser al menos 1.", nameof(paymentTermInstallments));
        if (paymentTermDaysBetween < 0)
            throw new ArgumentException("Los días entre cuotas no pueden ser negativos.", nameof(paymentTermDaysBetween));

        var document = new ExpenseDocument
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            SupplierId = supplierId,
            SupplierName = supplierName.Trim(),
            SupplierTaxId = supplierTaxId.Trim(),
            IssueDate = issueDate,
            AccountingDate = accountingDate,
            DocumentType = documentType.Trim(),
            DocumentNumber = documentNumber.Trim(),
            AuthorizationNumber = OptionalCode.Normalize(authorizationNumber),
            AuthorizationDate = authorizationDate,
            PaymentTermId = paymentTermId,
            PaymentTermName = paymentTermName.Trim(),
            PaymentTermInstallments = paymentTermInstallments,
            PaymentTermDaysBetween = paymentTermDaysBetween,
            DueDate = dueDate,
            Notes = notes?.Trim(),
            Status = ExpenseStatus.Draft,
        };
        document.SetCreated(createdBy);
        return document;
    }

    public void ReplaceLines(IEnumerable<ExpenseLine> lines, Guid updatedBy)
    {
        EnsureDraft();
        _lines.Clear();
        short order = 1;
        foreach (var line in lines)
        {
            if (line.TenantId != TenantId || line.ExpenseDocumentId != Id)
                throw new ArgumentException("La línea no pertenece al documento de gasto.", nameof(lines));

            line.SetSortOrder(order++);
            _lines.Add(line);
        }
        SetUpdated(updatedBy);
    }

    public void UpdateDraft(
        Guid supplierId,
        string supplierName,
        string supplierTaxId,
        DateOnly issueDate,
        DateOnly accountingDate,
        string documentType,
        string documentNumber,
        Guid paymentTermId,
        string paymentTermName,
        int paymentTermInstallments,
        int paymentTermDaysBetween,
        Guid updatedBy,
        string? authorizationNumber = null,
        DateTime? authorizationDate = null,
        DateOnly? dueDate = null,
        string? notes = null
    )
    {
        EnsureDraft();
        if (supplierId == Guid.Empty)
            throw new ArgumentException("El proveedor es obligatorio.", nameof(supplierId));
        if (string.IsNullOrWhiteSpace(supplierName))
            throw new ArgumentException("El nombre del proveedor es obligatorio.", nameof(supplierName));
        if (string.IsNullOrWhiteSpace(supplierTaxId))
            throw new ArgumentException("El RUC/CI del proveedor es obligatorio.", nameof(supplierTaxId));
        if (string.IsNullOrWhiteSpace(documentType))
            throw new ArgumentException("El tipo de documento es obligatorio.", nameof(documentType));
        if (string.IsNullOrWhiteSpace(documentNumber))
            throw new ArgumentException("El número de documento es obligatorio.", nameof(documentNumber));
        if (paymentTermId == Guid.Empty)
            throw new ArgumentException("La condición de pago es obligatoria.", nameof(paymentTermId));
        if (paymentTermInstallments < 1)
            throw new ArgumentException("Las cuotas deben ser al menos 1.", nameof(paymentTermInstallments));
        if (paymentTermDaysBetween < 0)
            throw new ArgumentException("Los días entre cuotas no pueden ser negativos.", nameof(paymentTermDaysBetween));

        SupplierId = supplierId;
        SupplierName = supplierName.Trim();
        SupplierTaxId = supplierTaxId.Trim();
        IssueDate = issueDate;
        AccountingDate = accountingDate;
        DocumentType = documentType.Trim();
        DocumentNumber = documentNumber.Trim();
        AuthorizationNumber = OptionalCode.Normalize(authorizationNumber);
        AuthorizationDate = authorizationDate;
        PaymentTermId = paymentTermId;
        PaymentTermName = paymentTermName.Trim();
        PaymentTermInstallments = paymentTermInstallments;
        PaymentTermDaysBetween = paymentTermDaysBetween;
        DueDate = dueDate;
        Notes = notes?.Trim();
        SetUpdated(updatedBy);
    }

    public void ReplacePaymentSchedule(
        IReadOnlyList<(int Number, DateOnly DueDate, decimal Amount, string? Notes)> installments
    )
    {
        EnsureDraft();
        if (installments.Count == 0)
            throw new ArgumentException("Debe incluir al menos una cuota.", nameof(installments));
        if (GrandTotal <= 0)
            throw new InvalidOperationException(
                "No se puede generar cronograma para un gasto con total cero o negativo."
            );

        var numbers = new HashSet<int>();
        foreach (var inst in installments)
        {
            if (inst.Number < 1)
                throw new ArgumentException("El número de cuota debe ser >= 1.", nameof(installments));
            if (inst.Amount <= 0)
                throw new ArgumentException("El monto de la cuota debe ser mayor a cero.", nameof(installments));
            if (inst.DueDate < IssueDate)
                throw new ArgumentException(
                    "La fecha de vencimiento no puede ser anterior a la fecha de emisión.",
                    nameof(installments)
                );
            if (!numbers.Add(inst.Number))
                throw new ArgumentException("El número de cuota está duplicado.", nameof(installments));
        }

        var total = GrandTotal;
        var sum = installments.Sum(i => i.Amount);
        if (sum != total)
            throw new InvalidOperationException(
                $"La suma de las cuotas ({sum:F2}) no coincide con el total del gasto ({total:F2})."
            );

        _paymentSchedules.Clear();
        foreach (var inst in installments.OrderBy(i => i.Number))
            _paymentSchedules.Add(
                ExpensePaymentSchedule.Create(Id, TenantId, inst.Number, inst.DueDate, inst.Amount, inst.Notes)
            );
    }

    /// <summary>
    /// EXPENSES-CONFIRM-07 — confirma el borrador y deja el documento listo para postear.
    /// <paramref name="lineAccountSnapshots"/> trae, por línea (clave <c>ExpenseLine.Id</c>), la
    /// cuenta contable YA validada por Application (activa, postable, misma empresa, tipo Gasto)
    /// contra la subcategoría vigente — este método solo recongela el snapshot de cada línea con
    /// ese dato (la subcategoría pudo cambiar de cuenta desde que el borrador se creó/editó) y
    /// congela los totales (<c>Confirmed*</c>), nunca vuelve a resolver ni validar cuentas por su
    /// cuenta (esa responsabilidad es de Application/Accounting, no de este aggregate).
    /// </summary>
    public void Confirm(
        IReadOnlyDictionary<Guid, (Guid AccountId, string? Code, string? Name)> lineAccountSnapshots,
        Guid confirmedBy
    )
    {
        EnsureDraft();
        if (_lines.Count == 0)
            throw new InvalidOperationException(
                "El gasto debe tener al menos una línea para confirmarse."
            );

        foreach (var line in _lines)
        {
            if (!lineAccountSnapshots.TryGetValue(line.Id, out var snapshot))
                throw new InvalidOperationException(
                    "Falta la cuenta contable resuelta para una línea del gasto."
                );
            line.RefreshAccountSnapshot(snapshot.AccountId, snapshot.Code, snapshot.Name);
        }

        ConfirmedSubtotal = _lines.Sum(l => l.LineSubtotal);
        ConfirmedTotalDiscount = _lines.Sum(l => l.DiscountAmount);
        ConfirmedTotalTax = _lines.Sum(l => l.VatAmount);
        ConfirmedGrandTotal = _lines.Sum(l => l.TaxInclusiveTotal);
        Status = ExpenseStatus.Confirmed;
        SetUpdated(confirmedBy);

        var lineAllocations = _lines
            .Select(l => new ExpenseDocumentConfirmedLineAllocation(
                l.Id,
                l.SnapshotAccountingAccountId,
                l.TaxableBase,
                l.Description
            ))
            .ToList();

        RaiseDomainEvent(
            new ExpenseDocumentConfirmedEvent(
                TenantId,
                Id,
                SupplierId,
                DocumentNumber,
                CompanyId,
                AccountingDate,
                TotalVat,
                GrandTotal,
                lineAllocations
            )
        );
    }

    /// <summary>
    /// EXPENSES-CANCEL-01 — anula un gasto ya confirmado. Solo Confirmed puede anularse (Draft se
    /// edita/elimina por su propio flujo; Cancelled es terminal). No reversa nada por su cuenta
    /// (CxP, contabilidad) — esa orquestación es responsabilidad de Application
    /// (<c>CancelExpenseDocumentHandler</c>) y del traductor de posting disparado por el evento que
    /// este método levanta.
    /// </summary>
    public void Cancel(string reason, Guid cancelledBy)
    {
        if (Status != ExpenseStatus.Confirmed)
            throw new InvalidOperationException("Solo se pueden anular gastos confirmados.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("El motivo de anulación es obligatorio.", nameof(reason));

        Status = ExpenseStatus.Cancelled;
        CancelReason = reason.Trim();
        CancelledAt = DateTime.UtcNow;
        CancelledBy = cancelledBy;
        SetUpdated(cancelledBy);

        RaiseDomainEvent(
            new ExpenseDocumentCancelledEvent(
                TenantId,
                Id,
                SupplierId,
                DocumentNumber,
                CompanyId,
                CancelReason
            )
        );
    }

    private void EnsureDraft()
    {
        if (Status != ExpenseStatus.Draft)
            throw new InvalidOperationException("Solo se pueden editar gastos en estado borrador.");
    }
}
