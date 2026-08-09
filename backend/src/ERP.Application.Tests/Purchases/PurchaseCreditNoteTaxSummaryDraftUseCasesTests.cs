using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Purchases;

/// <summary>
/// FLOW-READY-02C-R1.2 — CreateDraftPurchaseCreditNoteHandler/UpdatePurchaseCreditNoteDraftHandler:
/// flujo principal de Discount por resumen fiscal de compra (<c>PurchaseCreditNoteTaxSummary</c>).
/// Nunca acepta VatCode/VatRate/IceCode/IceRate del cliente — siempre heredados del
/// <c>PurchaseInvoiceTaxSummary</c> real; recalcula IceAmount/VatAmount server-side; bloquea si la
/// base excede la disponible (base de compra − ya acreditado por NC Discount no canceladas).
/// </summary>
public sealed class PurchaseCreditNoteTaxSummaryDraftUseCasesTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid PaymentTermId = Guid.NewGuid();
    private static readonly Guid WarehouseId = Guid.NewGuid();
    private static readonly Guid ItemId = Guid.NewGuid();

    private sealed record Fixture(PurchaseInvoice Invoice, PurchasePayable Payable);

    private static Fixture BuildFixture(
        decimal unitPrice = 1000m,
        decimal vatRate = 15m,
        string vatCode = "10"
    )
    {
        var invoice = PurchaseInvoice.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            SupplierId,
            "Proveedor Test",
            "1234567890001",
            "01",
            $"001-001-{Random.Shared.Next(100000000, 999999999)}",
            DateOnly.FromDateTime(DateTime.UtcNow),
            UserId,
            PaymentTermId,
            "Contado",
            1,
            30,
            globalWarehouseId: WarehouseId
        );
        var line = PurchaseInvoiceDetail.Create(
            invoice.Id,
            TenantId,
            "Producto 1",
            quantity: 1,
            unitPrice: unitPrice,
            vatCode: vatCode,
            uomCode: "UNIT",
            itemId: ItemId,
            warehouseId: WarehouseId
        );
        invoice.ReplaceLines(new[] { line }, UserId);
        line.ApplyTaxes(vatCode, vatRate, "IVA 15%", null, 0m, null);
        invoice.Confirm(UserId);

        var payable = PurchasePayable.Create(
            TenantId,
            CompanyId,
            invoice.Id,
            SupplierId,
            invoice.GrandTotal,
            UserId
        );

        return new Fixture(invoice, payable);
    }

    private sealed class Mocks
    {
        public Mock<IPurchaseCreditNoteRepository> CreditNoteRepo { get; } = new();
        public Mock<IPurchaseInvoiceRepository> InvoiceRepo { get; } = new();
        public Mock<IPurchaseReceptionDocumentRepository> ReceptionRepo { get; } = new();
        public Mock<IDatabaseExceptionTranslator> DbEx { get; } = new();

        public Mocks(Fixture f, IReadOnlyDictionary<Guid, decimal>? creditedBySourceId = null)
        {
            InvoiceRepo
                .Setup(r => r.GetByIdAsync(TenantId, f.Invoice.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(f.Invoice);
            InvoiceRepo
                .Setup(r =>
                    r.GetPayableByPurchaseIdAsync(TenantId, f.Invoice.Id, It.IsAny<CancellationToken>())
                )
                .ReturnsAsync(f.Payable);
            CreditNoteRepo
                .Setup(r =>
                    r.GetByCreateClientRequestIdAsync(
                        TenantId,
                        It.IsAny<Guid>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync((PurchaseCreditNote?)null);
            CreditNoteRepo
                .Setup(r =>
                    r.ExistsByAccessKeyAsync(TenantId, It.IsAny<string>(), It.IsAny<CancellationToken>())
                )
                .ReturnsAsync(false);
            CreditNoteRepo
                .Setup(r =>
                    r.ExistsBySupplierAndCreditNoteNumberAsync(
                        TenantId,
                        CompanyId,
                        It.IsAny<Guid>(),
                        It.IsAny<string>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(false);
            CreditNoteRepo
                .Setup(r =>
                    r.ExistsByReceptionDocumentIdAsync(
                        TenantId,
                        It.IsAny<Guid>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(false);
            CreditNoteRepo
                .Setup(r =>
                    r.GetCreditedTaxableBaseByPurchaseTaxSummaryIdsAsync(
                        TenantId,
                        It.IsAny<IReadOnlyCollection<Guid>>(),
                        It.IsAny<Guid?>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(creditedBySourceId ?? new Dictionary<Guid, decimal>());
        }

        public CreateDraftPurchaseCreditNoteHandler BuildCreateHandler() =>
            new(
                CreditNoteRepo.Object,
                InvoiceRepo.Object,
                ReceptionRepo.Object,
                DbEx.Object,
                FixedTenant(),
                FixedCompany(),
                FixedBranch(),
                FixedUser()
            );
    }

    private static ICurrentTenant FixedTenant()
    {
        var m = new Mock<ICurrentTenant>();
        m.SetupGet(x => x.TenantId).Returns(TenantId);
        return m.Object;
    }

    private static ICurrentCompany FixedCompany()
    {
        var m = new Mock<ICurrentCompany>();
        m.SetupGet(x => x.CompanyId).Returns(CompanyId);
        m.SetupGet(x => x.HasCompanyContext).Returns(true);
        return m.Object;
    }

    private static ICurrentBranch FixedBranch()
    {
        var m = new Mock<ICurrentBranch>();
        m.SetupGet(x => x.BranchId).Returns(BranchId);
        return m.Object;
    }

    private static ICurrentUser FixedUser()
    {
        var m = new Mock<ICurrentUser>();
        m.SetupGet(x => x.UserId).Returns(UserId);
        return m.Object;
    }

    [Fact]
    public async Task CreateDraft_Discount_con_TaxSummaryLines_crea_resumenes_desde_invoice_tax_summaries()
    {
        var f = BuildFixture();
        var source = f.Invoice.TaxSummaries.Single();
        var m = new Mocks(f);
        var handler = m.BuildCreateHandler();

        var result = await handler.Handle(
            new CreateDraftPurchaseCreditNoteCommand(
                Guid.NewGuid(),
                f.Invoice.Id,
                null,
                PurchaseCreditNoteApplicationType.Discount,
                "001-001-000000020",
                null,
                null,
                null,
                DateOnly.FromDateTime(DateTime.UtcNow),
                "Descuento por volumen",
                Array.Empty<PurchaseCreditNoteDraftLineInput>(),
                new[] { new PurchaseCreditNoteTaxSummaryLineInput(source.Id, 200m) }
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue($"Error: {result.Error}");
        result.Value!.TaxSummaries.Should().ContainSingle();
        var dtoSummary = result.Value.TaxSummaries.Single();
        dtoSummary.SourcePurchaseInvoiceTaxSummaryId.Should().Be(source.Id);
        dtoSummary.VatCode.Should().Be(source.VatCode);
        dtoSummary.VatRate.Should().Be(source.VatRate);
        dtoSummary.TaxableBase.Should().Be(200m);
        dtoSummary.VatAmount.Should().Be(30m); // 200 * 15%
        dtoSummary.TotalAmount.Should().Be(230m);
    }

    [Fact]
    public async Task CreateDraft_TaxSummaryLines_rechaza_source_que_no_pertenece_a_la_factura()
    {
        var f = BuildFixture();
        var m = new Mocks(f);
        var handler = m.BuildCreateHandler();

        var result = await handler.Handle(
            new CreateDraftPurchaseCreditNoteCommand(
                Guid.NewGuid(),
                f.Invoice.Id,
                null,
                PurchaseCreditNoteApplicationType.Discount,
                "001-001-000000021",
                null,
                null,
                null,
                DateOnly.FromDateTime(DateTime.UtcNow),
                "Descuento",
                Array.Empty<PurchaseCreditNoteDraftLineInput>(),
                new[] { new PurchaseCreditNoteTaxSummaryLineInput(Guid.NewGuid(), 100m) }
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("no pertenece a la factura afectada");
    }

    [Fact]
    public async Task CreateDraft_TaxSummaryLines_rechaza_base_que_excede_la_disponible()
    {
        var f = BuildFixture(unitPrice: 100m);
        var source = f.Invoice.TaxSummaries.Single(); // TaxableBase = 100
        var m = new Mocks(f);
        var handler = m.BuildCreateHandler();

        var result = await handler.Handle(
            new CreateDraftPurchaseCreditNoteCommand(
                Guid.NewGuid(),
                f.Invoice.Id,
                null,
                PurchaseCreditNoteApplicationType.Discount,
                "001-001-000000022",
                null,
                null,
                null,
                DateOnly.FromDateTime(DateTime.UtcNow),
                "Descuento",
                Array.Empty<PurchaseCreditNoteDraftLineInput>(),
                new[] { new PurchaseCreditNoteTaxSummaryLineInput(source.Id, 150m) }
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("excede la base disponible");
    }

    [Fact]
    public async Task CreateDraft_TaxSummaryLines_descuenta_lo_ya_acreditado_por_otras_NC_no_canceladas()
    {
        var f = BuildFixture(unitPrice: 100m);
        var source = f.Invoice.TaxSummaries.Single(); // TaxableBase = 100
        var credited = new Dictionary<Guid, decimal> { [source.Id] = 60m }; // disponible = 40
        var m = new Mocks(f, credited);
        var handler = m.BuildCreateHandler();

        var result = await handler.Handle(
            new CreateDraftPurchaseCreditNoteCommand(
                Guid.NewGuid(),
                f.Invoice.Id,
                null,
                PurchaseCreditNoteApplicationType.Discount,
                "001-001-000000023",
                null,
                null,
                null,
                DateOnly.FromDateTime(DateTime.UtcNow),
                "Descuento",
                Array.Empty<PurchaseCreditNoteDraftLineInput>(),
                new[] { new PurchaseCreditNoteTaxSummaryLineInput(source.Id, 50m) }
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("excede la base disponible");
    }

    [Fact]
    public async Task CreateDraft_Return_con_TaxSummaryLines_es_rechazado()
    {
        var f = BuildFixture();
        var source = f.Invoice.TaxSummaries.Single();
        var m = new Mocks(f);
        var handler = m.BuildCreateHandler();

        var result = await handler.Handle(
            new CreateDraftPurchaseCreditNoteCommand(
                Guid.NewGuid(),
                f.Invoice.Id,
                null,
                PurchaseCreditNoteApplicationType.Return,
                "001-001-000000024",
                null,
                null,
                null,
                DateOnly.FromDateTime(DateTime.UtcNow),
                "Devolución",
                Array.Empty<PurchaseCreditNoteDraftLineInput>(),
                new[] { new PurchaseCreditNoteTaxSummaryLineInput(source.Id, 50m) }
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Devolución");
    }
}
