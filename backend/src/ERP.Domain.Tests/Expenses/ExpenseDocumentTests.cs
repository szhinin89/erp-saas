using ERP.Domain.Modules.Expenses.Entities;
using ERP.Domain.Modules.Expenses.Enums;
using FluentAssertions;

namespace ERP.Domain.Tests.Expenses;

public sealed class ExpenseDocumentTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid PaymentTermId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid ExpenseSubcategoryId = Guid.NewGuid();
    private static readonly Guid ExpenseAccountId = Guid.NewGuid();

    [Fact]
    public void ExpenseLine_does_not_allow_negative_totals()
    {
        var act = () =>
            ExpenseLine.Create(
                Guid.NewGuid(),
                TenantId,
                ExpenseSubcategoryId,
                ExpenseAccountId,
                "Servicio de internet",
                quantity: 1m,
                unitAmount: -10m,
                vatCode: "0"
            );

        act.Should().Throw<ArgumentException>().WithMessage("*unitario no puede ser negativo*");
    }

    [Fact]
    public void ExpenseDocument_starts_in_draft()
    {
        var document = CreateDraftDocument();

        document.Status.Should().Be(ExpenseStatus.Draft);
    }

    [Fact]
    public void ExpenseLine_has_no_item_warehouse_kardex_or_purchase_detail_dependencies()
    {
        var forbiddenPropertyNames = new[]
        {
            "ItemId",
            "WarehouseId",
            "UomCode",
            "PackagingLevelId",
            "ReceptionLineId",
            "PurchaseReceptionLineId",
            "KardexId",
            "PurchaseInvoiceDetailId",
            "LandedUnitCost",
            "TotalLineCost",
        };

        var propertyNames = typeof(ExpenseLine).GetProperties().Select(p => p.Name).ToHashSet();

        propertyNames.Should().NotContain(forbiddenPropertyNames);
    }

    [Fact]
    public void ExpenseDocument_accepts_non_inventory_expense_lines()
    {
        var document = CreateDraftDocument();
        var line = ExpenseLine.Create(
            document.Id,
            TenantId,
            ExpenseSubcategoryId,
            ExpenseAccountId,
            "Servicio de internet",
            quantity: 1m,
            unitAmount: 100m,
            vatCode: "2",
            vatRate: 15m
        );

        document.ReplaceLines([line], UserId);

        document.Subtotal.Should().Be(100m);
        document.TotalVat.Should().Be(15m);
        document.GrandTotal.Should().Be(115m);
    }

    private static ExpenseDocument CreateDraftDocument() =>
        ExpenseDocument.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            SupplierId,
            "Proveedor Servicios",
            "1790012345001",
            DateOnly.FromDateTime(DateTime.UtcNow),
            DateOnly.FromDateTime(DateTime.UtcNow),
            "01",
            "001-001-000000001",
            PaymentTermId,
            "Contado",
            1,
            0,
            UserId
        );
}
