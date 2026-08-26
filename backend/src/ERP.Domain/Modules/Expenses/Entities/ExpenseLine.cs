using ERP.Domain.Common;

namespace ERP.Domain.Modules.Expenses.Entities;

public sealed class ExpenseLine : IMustHaveTenant
{
    public const int DescriptionMaxLen = 300;
    public const int NotesMaxLen = 300;
    public const int VatCodeMaxLen = 10;
    public const int VatNameMaxLen = 100;
    public const int AccountCodeMaxLen = 30;
    public const int AccountNameMaxLen = 150;

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ExpenseDocumentId { get; private set; }
    public Guid ExpenseSubcategoryId { get; private set; }
    public Guid SnapshotAccountingAccountId { get; private set; }
    public string? SnapshotAccountingAccountCode { get; private set; }
    public string? SnapshotAccountingAccountName { get; private set; }
    public string Description { get; private set; } = null!;
    public decimal Quantity { get; private set; }
    public decimal UnitAmount { get; private set; }
    public decimal DiscountPct { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public string VatCode { get; private set; } = null!;
    public decimal VatRate { get; private set; }
    public decimal VatAmount { get; private set; }
    public string? SnapshotVatName { get; private set; }
    public string? Notes { get; private set; }
    public short SortOrder { get; private set; }

    public decimal LineSubtotal => Math.Round(Quantity * UnitAmount, FiscalPrecision.TaxAmount, MidpointRounding.AwayFromZero);
    public decimal TaxableBase =>
        Math.Round(LineSubtotal - DiscountAmount, FiscalPrecision.TaxAmount, MidpointRounding.AwayFromZero);
    public decimal TaxInclusiveTotal =>
        Math.Round(TaxableBase + VatAmount, FiscalPrecision.TaxAmount, MidpointRounding.AwayFromZero);

    private ExpenseLine() { }

    public static ExpenseLine Create(
        Guid expenseDocumentId,
        Guid tenantId,
        Guid expenseSubcategoryId,
        Guid snapshotAccountingAccountId,
        string description,
        decimal quantity,
        decimal unitAmount,
        string vatCode,
        decimal vatRate = 0m,
        string? snapshotVatName = null,
        decimal discountPct = 0m,
        string? snapshotAccountingAccountCode = null,
        string? snapshotAccountingAccountName = null,
        string? notes = null
    )
    {
        if (expenseDocumentId == Guid.Empty)
            throw new ArgumentException("El documento de gasto es obligatorio.", nameof(expenseDocumentId));
        if (tenantId == Guid.Empty)
            throw new ArgumentException("El tenant es obligatorio.", nameof(tenantId));
        if (expenseSubcategoryId == Guid.Empty)
            throw new ArgumentException("La subcategoría de gasto es obligatoria.", nameof(expenseSubcategoryId));
        if (snapshotAccountingAccountId == Guid.Empty)
            throw new ArgumentException(
                "La cuenta contable snapshot es obligatoria.",
                nameof(snapshotAccountingAccountId)
            );
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("La descripción de la línea es obligatoria.", nameof(description));
        if (description.Trim().Length > DescriptionMaxLen)
            throw new ArgumentException(
                $"La descripción no puede superar {DescriptionMaxLen} caracteres.",
                nameof(description)
            );
        if (quantity <= 0)
            throw new ArgumentException("La cantidad debe ser mayor a cero.", nameof(quantity));
        if (unitAmount < 0)
            throw new ArgumentException("El valor unitario no puede ser negativo.", nameof(unitAmount));
        if (discountPct is < 0 or > 100)
            throw new ArgumentException("El descuento debe estar entre 0 y 100.", nameof(discountPct));
        if (string.IsNullOrWhiteSpace(vatCode))
            throw new ArgumentException("El código IVA es obligatorio.", nameof(vatCode));
        if (vatRate < 0)
            throw new ArgumentException("La tasa IVA no puede ser negativa.", nameof(vatRate));

        var line = new ExpenseLine
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ExpenseDocumentId = expenseDocumentId,
            ExpenseSubcategoryId = expenseSubcategoryId,
            SnapshotAccountingAccountId = snapshotAccountingAccountId,
            SnapshotAccountingAccountCode = Normalize(snapshotAccountingAccountCode),
            SnapshotAccountingAccountName = Normalize(snapshotAccountingAccountName),
            Description = description.Trim(),
            Quantity = quantity,
            UnitAmount = unitAmount,
            DiscountPct = discountPct,
            VatCode = vatCode.Trim(),
            VatRate = vatRate,
            SnapshotVatName = Normalize(snapshotVatName),
            Notes = Normalize(notes),
        };
        line.RecalculateAmounts();
        return line;
    }

    internal void SetSortOrder(short order) => SortOrder = order;

    private void RecalculateAmounts()
    {
        DiscountAmount =
            DiscountPct > 0
                ? Math.Round(
                    LineSubtotal * DiscountPct / 100m,
                    FiscalPrecision.TaxAmount,
                    MidpointRounding.AwayFromZero
                )
                : 0m;

        if (TaxableBase < 0)
            throw new InvalidOperationException("La base imponible de la línea no puede ser negativa.");

        VatAmount =
            VatRate > 0
                ? Math.Round(
                    TaxableBase * VatRate / 100m,
                    FiscalPrecision.TaxAmount,
                    MidpointRounding.AwayFromZero
                )
                : 0m;

        if (LineSubtotal < 0 || DiscountAmount < 0 || VatAmount < 0 || TaxInclusiveTotal < 0)
            throw new InvalidOperationException("Los totales de la línea de gasto no pueden ser negativos.");
    }

    private static string? Normalize(string? value) => value?.Trim() is { Length: > 0 } text ? text : null;
}
