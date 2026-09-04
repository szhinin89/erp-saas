using ERP.Domain.Modules.Expenses.Entities;
using ERP.Domain.Modules.Expenses.Enums;
using ERP.Domain.Modules.Expenses.Events;
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

    [Fact]
    public void Confirm_documento_valido_pasa_a_Confirmed_y_congela_totales()
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
        var newAccountId = Guid.NewGuid();

        document.Confirm(
            new Dictionary<Guid, (Guid, string?, string?)>
            {
                [line.Id] = (newAccountId, "6.1.02", "Gasto reasignado"),
            },
            UserId
        );

        document.Status.Should().Be(ExpenseStatus.Confirmed);
        document.ConfirmedGrandTotal.Should().Be(115m);
        document.ConfirmedTotalTax.Should().Be(15m);
        document.Lines.Single().SnapshotAccountingAccountId.Should().Be(newAccountId);
        document.Lines.Single().SnapshotAccountingAccountCode.Should().Be("6.1.02");
        document.Lines.Single().SnapshotAccountingAccountName.Should().Be("Gasto reasignado");
    }

    [Fact]
    public void Confirm_levanta_ExpenseDocumentConfirmedEvent_con_una_allocation_por_linea()
    {
        var document = CreateDraftDocument();
        var lineA = ExpenseLine.Create(
            document.Id, TenantId, ExpenseSubcategoryId, ExpenseAccountId,
            "Internet", 1m, 100m, "2", vatRate: 15m
        );
        var accountB = Guid.NewGuid();
        var lineB = ExpenseLine.Create(
            document.Id, TenantId, Guid.NewGuid(), accountB,
            "Suministros", 1m, 50m, "0"
        );
        document.ReplaceLines([lineA, lineB], UserId);

        document.Confirm(
            new Dictionary<Guid, (Guid, string?, string?)>
            {
                [lineA.Id] = (ExpenseAccountId, "6.1.01", "Internet"),
                [lineB.Id] = (accountB, "6.1.02", "Suministros"),
            },
            UserId
        );

        var raised = document.DomainEvents.OfType<ExpenseDocumentConfirmedEvent>().Single();
        raised.LineAllocations.Should().HaveCount(2);
        raised.LineAllocations.Should()
            .Contain(a => a.AccountingAccountId == ExpenseAccountId && a.Amount == 100m);
        raised.LineAllocations.Should()
            .Contain(a => a.AccountingAccountId == accountB && a.Amount == 50m);
        raised.TotalVat.Should().Be(15m);
        raised.GrandTotal.Should().Be(165m);
    }

    [Fact]
    public void Confirm_documento_ya_confirmado_lanza_excepcion()
    {
        var document = CreateDraftDocument();
        var line = ExpenseLine.Create(
            document.Id, TenantId, ExpenseSubcategoryId, ExpenseAccountId,
            "Internet", 1m, 100m, "0"
        );
        document.ReplaceLines([line], UserId);
        document.Confirm(
            new Dictionary<Guid, (Guid, string?, string?)> { [line.Id] = (ExpenseAccountId, null, null) },
            UserId
        );

        var act = () =>
            document.Confirm(
                new Dictionary<Guid, (Guid, string?, string?)>
                {
                    [line.Id] = (ExpenseAccountId, null, null),
                },
                UserId
            );

        act.Should().Throw<InvalidOperationException>().WithMessage("*borrador*");
    }

    [Fact]
    public void Confirm_sin_lineas_lanza_excepcion()
    {
        var document = CreateDraftDocument();

        var act = () => document.Confirm(new Dictionary<Guid, (Guid, string?, string?)>(), UserId);

        act.Should().Throw<InvalidOperationException>().WithMessage("*al menos una línea*");
    }

    [Fact]
    public void Cancel_gasto_confirmado_pasa_a_Cancelled_y_guarda_motivo_fecha_y_autor()
    {
        var document = ConfirmedDocument();
        var beforeCancel = DateTime.UtcNow;

        document.Cancel("  Documento duplicado  ", UserId);

        document.Status.Should().Be(ExpenseStatus.Cancelled);
        document.CancelReason.Should().Be("Documento duplicado");
        document.CancelledBy.Should().Be(UserId);
        document.CancelledAt.Should().NotBeNull();
        document.CancelledAt!.Value.Should().BeOnOrAfter(beforeCancel);
        document.UpdatedBy.Should().Be(UserId);
    }

    [Fact]
    public void Cancel_levanta_ExpenseDocumentCancelledEvent_con_motivo()
    {
        var document = ConfirmedDocument();

        document.Cancel("Error de digitación", UserId);

        var raised = document.DomainEvents.OfType<ExpenseDocumentCancelledEvent>().Single();
        raised.ExpenseDocumentId.Should().Be(document.Id);
        raised.SupplierId.Should().Be(SupplierId);
        raised.CancelReason.Should().Be("Error de digitación");
    }

    [Fact]
    public void Cancel_sobre_Draft_lanza_excepcion()
    {
        var document = CreateDraftDocument();

        var act = () => document.Cancel("Motivo", UserId);

        act.Should().Throw<InvalidOperationException>().WithMessage("*confirmados*");
    }

    [Fact]
    public void Cancel_sobre_documento_ya_Cancelled_lanza_excepcion()
    {
        var document = ConfirmedDocument();
        document.Cancel("Primera anulación", UserId);

        var act = () => document.Cancel("Segunda anulación", UserId);

        act.Should().Throw<InvalidOperationException>().WithMessage("*confirmados*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Cancel_sin_motivo_lanza_excepcion(string? reason)
    {
        var document = ConfirmedDocument();

        var act = () => document.Cancel(reason!, UserId);

        act.Should().Throw<ArgumentException>().WithMessage("*motivo*");
    }

    private static ExpenseDocument ConfirmedDocument()
    {
        var document = CreateDraftDocument();
        var line = ExpenseLine.Create(
            document.Id, TenantId, ExpenseSubcategoryId, ExpenseAccountId,
            "Internet", 1m, 100m, "2", vatRate: 15m
        );
        document.ReplaceLines([line], UserId);
        document.Confirm(
            new Dictionary<Guid, (Guid, string?, string?)>
            {
                [line.Id] = (ExpenseAccountId, "6.1.01", "Internet"),
            },
            UserId
        );
        return document;
    }

    [Fact]
    public void ExpenseDocument_no_referencia_conceptos_de_inventario_kardex_o_compras()
    {
        var forbiddenPropertyNames = new[]
        {
            "ItemId",
            "WarehouseId",
            "PackagingLevelId",
            "ReceptionLineId",
            "PurchaseReceptionLineId",
            "KardexId",
            "PurchaseInvoiceId",
            "PurchaseInvoiceDetailId",
            "Pvp",
            "AverageCost",
        };

        var propertyNames = typeof(ExpenseDocument)
            .GetProperties()
            .Select(p => p.Name)
            .Concat(typeof(ExpenseDocumentConfirmedEvent).GetProperties().Select(p => p.Name))
            .ToHashSet();

        propertyNames.Should().NotContain(forbiddenPropertyNames);
    }

    // ── RETENTIONS-SOURCE-DOCUMENT-TAX-SUPPORT-02G ──────────────────────────

    [Fact]
    public void CreateDraft_normaliza_TaxSupportCode_vacio_a_null()
    {
        var document = ExpenseDocument.CreateDraft(
            TenantId, CompanyId, BranchId, SupplierId, "Proveedor Servicios", "1790012345001",
            DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow),
            "01", "001-001-000000001", PaymentTermId, "Contado", 1, 0, UserId,
            taxSupportCode: "   "
        );

        document.TaxSupportCode.Should().BeNull();
    }

    [Fact]
    public void CreateDraft_recorta_y_persiste_TaxSupportCode_con_contenido_real()
    {
        var document = ExpenseDocument.CreateDraft(
            TenantId, CompanyId, BranchId, SupplierId, "Proveedor Servicios", "1790012345001",
            DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow),
            "01", "001-001-000000001", PaymentTermId, "Contado", 1, 0, UserId,
            taxSupportCode: " 02 "
        );

        document.TaxSupportCode.Should().Be("02");
    }

    [Fact]
    public void CreateDraft_sin_TaxSupportCode_queda_null()
    {
        var document = CreateDraftDocument();

        document.TaxSupportCode.Should().BeNull();
    }

    [Fact]
    public void UpdateDraft_actualiza_TaxSupportCode()
    {
        var document = CreateDraftDocument();

        document.UpdateDraft(
            SupplierId, "Proveedor Servicios", "1790012345001",
            document.IssueDate, document.AccountingDate, document.DocumentType, document.DocumentNumber,
            PaymentTermId, "Contado", 1, 0, UserId,
            taxSupportCode: "02"
        );

        document.TaxSupportCode.Should().Be("02");
    }

    [Fact]
    public void UpdateDraft_sin_TaxSupportCode_lo_deja_null()
    {
        var document = CreateDraftDocument();
        document.UpdateDraft(
            SupplierId, "Proveedor Servicios", "1790012345001",
            document.IssueDate, document.AccountingDate, document.DocumentType, document.DocumentNumber,
            PaymentTermId, "Contado", 1, 0, UserId,
            taxSupportCode: "02"
        );

        // Segunda edición sin pasar taxSupportCode — mismo criterio que el resto de campos
        // opcionales de UpdateDraft (Notes, AuthorizationNumber): el default null sobreescribe,
        // no preserva el valor anterior. UpdateDraft reemplaza el estado completo del borrador.
        document.UpdateDraft(
            SupplierId, "Proveedor Servicios", "1790012345001",
            document.IssueDate, document.AccountingDate, document.DocumentType, document.DocumentNumber,
            PaymentTermId, "Contado", 1, 0, UserId
        );

        document.TaxSupportCode.Should().BeNull();
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
