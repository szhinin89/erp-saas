using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Payables.Interfaces;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Purchases;

/// <summary>
/// FLOW-READY-02C.2 — <c>CreateDraftPurchaseCreditNoteHandler</c>/<c>UpdatePurchaseCreditNoteDraftHandler</c>:
/// creación válida, vínculo de <c>ReceptionDocumentId</c>, rechazo por documento ya usado,
/// inconsistencia proveedor/factura, traducción de violaciones únicas, y restricción a <c>Draft</c>.
/// </summary>
public sealed class PurchaseCreditNoteDraftUseCasesTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid PaymentTermId = Guid.NewGuid();
    private static readonly Guid WarehouseId = Guid.NewGuid();

    private sealed record Fixture(PurchaseInvoice Invoice, AccountsPayable Payable);

    private static Fixture BuildFixture(decimal totalAmount = 1000m, decimal paidAmount = 0m, bool secondLine = false)
    {
        var invoice = PurchaseInvoice.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            SupplierId,
            "Proveedor Test",
            "1234567890001",
            "01",
            "001-001-000000001",
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
            unitPrice: totalAmount,
            vatCode: "0",
            uomCode: "UNIT",
            itemId: Guid.NewGuid(),
            warehouseId: WarehouseId
        );
        line.ApplyTaxes("0", 0m, "IVA", null, 0m, null);
        var invoiceLines = new List<PurchaseInvoiceDetail> { line };
        if (secondLine)
        {
            var other = PurchaseInvoiceDetail.Create(invoice.Id, TenantId, "Producto 2", 2, 50m,
                "2", "UNIT", itemId: Guid.NewGuid(), warehouseId: WarehouseId);
            other.ReplaceTaxes([PurchaseInvoiceDetailTax.Create(other.Id, TenantId, "5", "5001", "IRBPNR", 1m,
                ERP.Domain.Modules.SriCatalogs.Enums.SriTaxCalculationType.Specific,
                other.TaxableBase, 2m, PurchaseTaxSource.Xml)]);
            other.ApplyTaxes("2", 15m, "IVA", "3000", 10m, "ICE");
            invoiceLines.Add(other);
        }
        invoice.ReplaceLines(invoiceLines, UserId);
        invoice.Confirm(UserId);

        var payable = AccountsPayable.CreateFromOrigin(
            TenantId, CompanyId, BranchId, SupplierId,
            AccountsPayableOriginType.PurchaseInvoice, invoice.Id,
            "01", "001-001-000000001",
            invoice.IssueDate, invoice.IssueDate, UserId
        );
        payable.AddInstallment(1, invoice.IssueDate.AddDays(30), totalAmount);
        if (paidAmount > 0)
            payable.RegisterPayment(paidAmount, UserId);

        return new Fixture(invoice, payable);
    }

    private static PurchaseReceptionDocument BuildReceptionDoc(
        Guid? supplierId,
        string? modifiedDocumentNumber = null,
        PurchaseReceptionSourceDocType sourceDocType = PurchaseReceptionSourceDocType.CreditNote
    ) =>
        PurchaseReceptionDocument.Create(
            TenantId,
            CompanyId,
            BranchId,
            sourceDocType,
            supplierRuc: "1710034065001",
            supplierName: "Proveedor Test",
            supplierId: supplierId,
            accessKey: $"AK-{Guid.NewGuid():N}",
            invoiceNumber: "001-001-000000099",
            issueDate: DateOnly.FromDateTime(DateTime.UtcNow),
            authorizationDate: DateTime.UtcNow,
            subtotal: 100m,
            vatAmount: 15m,
            totalAmount: 115m,
            createdBy: UserId,
            modifiedDocumentNumber: modifiedDocumentNumber
        );

    private static IReadOnlyList<PurchaseCreditNoteDraftLineInput> OneLine(decimal subtotal = 100m) =>
        new[] { new PurchaseCreditNoteDraftLineInput("Descuento", subtotal, "2", 15m, subtotal * 0.15m) };

    private sealed class Mocks
    {
        public Mock<IPurchaseCreditNoteRepository> CreditNoteRepo { get; } = new();
        public Mock<IPurchaseInvoiceRepository> InvoiceRepo { get; } = new();
        public Mock<IAccountsPayableRepository> PayableRepo { get; } = new();
        public Mock<IPurchaseReceptionDocumentRepository> ReceptionRepo { get; } = new();
        public Mock<IPurchaseReturnRepository> ReturnRepo { get; } = new();
        public Mock<IDatabaseExceptionTranslator> DbEx { get; } = new();

        public Mocks(Fixture f)
        {
            ReturnRepo.Setup(r => r.GetReturnedQuantitiesByInvoiceDetailIdsAsync(TenantId,
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<Guid, decimal>());
            InvoiceRepo
                .Setup(r => r.GetByIdAsync(TenantId, f.Invoice.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(f.Invoice);
            PayableRepo
                .Setup(r =>
                    r.GetByOriginAsync(
                        TenantId,
                        CompanyId,
                        AccountsPayableOriginType.PurchaseInvoice,
                        f.Invoice.Id,
                        It.IsAny<CancellationToken>()
                    )
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
                .ReturnsAsync(new Dictionary<Guid, decimal>());
        }

        public CreateDraftPurchaseCreditNoteHandler BuildCreateHandler() =>
            new(
                CreditNoteRepo.Object,
                InvoiceRepo.Object,
                PayableRepo.Object,
                ReceptionRepo.Object,
                DbEx.Object,
                FixedTenant(),
                FixedCompany(),
                FixedBranch(),
                FixedUser(),
                ReturnRepo.Object
            );

        public UpdatePurchaseCreditNoteDraftHandler BuildUpdateHandler() =>
            new(CreditNoteRepo.Object, InvoiceRepo.Object, PayableRepo.Object, DbEx.Object, FixedTenant(), FixedCompany(), FixedUser());
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

    // ── 1. CreateDraft válido ────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("CLIENT-KEY")]
    public async Task CreateDraft_copies_authoritative_reception_key(string? clientKey)
    {
        var f = BuildFixture();
        var m = new Mocks(f);
        var reception = BuildReceptionDoc(SupplierId, f.Invoice.InvoiceNumber);
        m.ReceptionRepo.Setup(r => r.GetByIdAsync(TenantId, reception.Id, It.IsAny<CancellationToken>())).ReturnsAsync(reception);
        var result = await m.BuildCreateHandler().Handle(new CreateDraftPurchaseCreditNoteCommand(
            Guid.NewGuid(), f.Invoice.Id, reception.Id, PurchaseCreditNoteApplicationType.Discount,
            reception.InvoiceNumber, clientKey, null, null, f.Invoice.IssueDate, "Descuento", OneLine()), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        result.Value!.AccessKey.Should().Be(reception.AccessKey);
        result.Value.ReceptionDocumentId.Should().Be(reception.Id);
        m.CreditNoteRepo.Verify(r => r.ExistsByAccessKeyAsync(TenantId, reception.AccessKey, It.IsAny<CancellationToken>()), Times.Once);
        m.CreditNoteRepo.Verify(r => r.AddAsync(It.Is<PurchaseCreditNote>(c => c.AccessKey == reception.AccessKey && c.ReceptionDocumentId == reception.Id), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateDraft_rejects_invoice_reception()
    {
        var f = BuildFixture();
        var m = new Mocks(f);
        var reception = BuildReceptionDoc(SupplierId, sourceDocType: PurchaseReceptionSourceDocType.Invoice);
        m.ReceptionRepo.Setup(r => r.GetByIdAsync(TenantId, reception.Id, It.IsAny<CancellationToken>())).ReturnsAsync(reception);
        var result = await m.BuildCreateHandler().Handle(new CreateDraftPurchaseCreditNoteCommand(
            Guid.NewGuid(), f.Invoice.Id, reception.Id, PurchaseCreditNoteApplicationType.Discount,
            reception.InvoiceNumber, null, null, null, f.Invoice.IssueDate, "Descuento", OneLine()), CancellationToken.None);
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("no es una Nota de Crédito");
        m.CreditNoteRepo.Verify(r => r.AddAsync(It.IsAny<PurchaseCreditNote>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateDraft_valido_crea_borrador()
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
                "001-001-000000005",
                null,
                null,
                null,
                DateOnly.FromDateTime(DateTime.UtcNow),
                "Descuento por volumen",
                OneLine()
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be("Draft");
        result.Value.ApplicationType.Should().Be("Discount");
        result.Value.SupplierId.Should().Be(SupplierId);
        result.Value.BranchId.Should().Be(BranchId);
        result.Value.TotalAmount.Should().Be(115m);
        m.CreditNoteRepo.Verify(
            r => r.AddAsync(It.IsAny<PurchaseCreditNote>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    // PURCHASE-CREDIT-NOTE-DISCOUNT-SEQUENCE-NO-MATCH-01 — smoke test rápido (repos mockeados) de
    // que CreateDraftPurchaseCreditNoteHandler acepta descuento por resumen fiscal con IVA 0%. La
    // causa raíz real ("Sequence contains no matching element") solo se manifestaba en el GET
    // posterior (GetPurchaseCreditNoteByIdHandler releyendo desde PostgreSQL real) — cubierto por
    // PurchaseCreditNoteDiscountIntegrationTests (ERP.Infrastructure.Tests), no reproducible con
    // mocks porque ahí la NC nunca se relee del "repositorio".
    [Fact]
    public async Task CreateDraft_descuento_por_resumen_fiscal_con_IVA_0_por_ciento_se_guarda_correctamente()
    {
        var f = BuildFixture();
        var m = new Mocks(f);
        var handler = m.BuildCreateHandler();
        var summary = f.Invoice.TaxSummaries[0];

        var result = await handler.Handle(
            new CreateDraftPurchaseCreditNoteCommand(
                Guid.NewGuid(),
                f.Invoice.Id,
                null,
                PurchaseCreditNoteApplicationType.Discount,
                "001-001-000000005",
                null,
                null,
                null,
                f.Invoice.IssueDate,
                "Descuento",
                Array.Empty<PurchaseCreditNoteDraftLineInput>(),
                new[] { new PurchaseCreditNoteTaxSummaryLineInput(summary.Id, 3.00m) }
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.TaxSummaries.Should().ContainSingle();
        result.Value.TaxSummaries[0].TaxableBase.Should().Be(3.00m);
        result.Value.TaxSummaries[0].VatAmount.Should().Be(0m);
        result.Value.LinkedPurchaseReturnId.Should().BeNull();
        m.ReturnRepo.Verify(
            r => r.AddAsync(It.IsAny<PurchaseReturn>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    /// <summary>Factura con una sola línea IVA 15% (sin ICE) — evita la mezcla ICE+IRBPNR de
    /// <c>BuildFixture(secondLine: true)</c>, que distorsionaría el cálculo esperado de IVA.</summary>
    private static Fixture BuildFixtureWithVat15(decimal totalAmount = 100m)
    {
        var invoice = PurchaseInvoice.CreateDraft(
            TenantId, CompanyId, BranchId, SupplierId, "Proveedor Test", "1234567890001",
            "01", "001-001-000000002", DateOnly.FromDateTime(DateTime.UtcNow), UserId,
            PaymentTermId, "Contado", 1, 30, globalWarehouseId: WarehouseId
        );
        var line = PurchaseInvoiceDetail.Create(
            invoice.Id, TenantId, "Producto gravado", quantity: 1, unitPrice: totalAmount,
            vatCode: "2", uomCode: "UNIT", itemId: Guid.NewGuid(), warehouseId: WarehouseId
        );
        line.ApplyTaxes("2", 15m, "IVA", null, 0m, null);
        invoice.ReplaceLines(new[] { line }, UserId);
        invoice.Confirm(UserId);

        var payable = AccountsPayable.CreateFromOrigin(
            TenantId, CompanyId, BranchId, SupplierId,
            AccountsPayableOriginType.PurchaseInvoice, invoice.Id,
            "01", invoice.InvoiceNumber, invoice.IssueDate, invoice.IssueDate, UserId
        );
        payable.AddInstallment(1, invoice.IssueDate.AddDays(30), totalAmount * 1.15m);

        return new Fixture(invoice, payable);
    }

    [Fact]
    public async Task CreateDraft_descuento_por_resumen_fiscal_con_IVA_15_por_ciento_calcula_IVA()
    {
        var f = BuildFixtureWithVat15(totalAmount: 100m);
        var m = new Mocks(f);
        var handler = m.BuildCreateHandler();
        var summary = f.Invoice.TaxSummaries.Should().ContainSingle().Which;

        var result = await handler.Handle(
            new CreateDraftPurchaseCreditNoteCommand(
                Guid.NewGuid(),
                f.Invoice.Id,
                null,
                PurchaseCreditNoteApplicationType.Discount,
                "001-001-000000006",
                null,
                null,
                null,
                f.Invoice.IssueDate,
                "Descuento",
                Array.Empty<PurchaseCreditNoteDraftLineInput>(),
                new[] { new PurchaseCreditNoteTaxSummaryLineInput(summary.Id, 20m) }
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        var resultSummary = result.Value!.TaxSummaries.Should().ContainSingle().Which;
        resultSummary.TaxableBase.Should().Be(20m);
        resultSummary.VatAmount.Should().Be(3m); // 20 * 15% = 3.00
        m.ReturnRepo.Verify(
            r => r.AddAsync(It.IsAny<PurchaseReturn>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task CreateDraft_descuento_rechaza_resumen_fiscal_inexistente_con_mensaje_claro()
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
                "001-001-000000007",
                null,
                null,
                null,
                f.Invoice.IssueDate,
                "Descuento",
                Array.Empty<PurchaseCreditNoteDraftLineInput>(),
                new[] { new PurchaseCreditNoteTaxSummaryLineInput(Guid.NewGuid(), 3.00m) }
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("no pertenece a la factura afectada");
        m.CreditNoteRepo.Verify(
            r => r.AddAsync(It.IsAny<PurchaseCreditNote>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task CreateDraft_rechaza_factura_Cancelled_con_mensaje_claro()
    {
        // PURCHASE-CREDIT-NOTE-AFFECTED-INVOICE-RESOLVES-CANCELLED-01 — si la resolución de
        // "factura afectada" (PurchaseReceptionVerifier / GetBySupplierAndInvoiceNumberAsync)
        // igual llegara a resolver una compra Cancelled (p. ej. un id viejo persistido en el
        // frontend), este es el candado fail-closed que ya existía y debe seguir rechazando con
        // un mensaje claro — nunca crear la NC contra una compra anulada.
        var f = BuildFixture();
        f.Invoice.Cancel("Anulada por error", UserId);
        var m = new Mocks(f);
        var handler = m.BuildCreateHandler();

        var result = await handler.Handle(
            new CreateDraftPurchaseCreditNoteCommand(
                Guid.NewGuid(),
                f.Invoice.Id,
                null,
                PurchaseCreditNoteApplicationType.Discount,
                "001-001-000000005",
                null,
                null,
                null,
                DateOnly.FromDateTime(DateTime.UtcNow),
                "Descuento por volumen",
                OneLine()
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Solo se pueden registrar notas de crédito sobre facturas de compra confirmadas.");
        m.CreditNoteRepo.Verify(
            r => r.AddAsync(It.IsAny<PurchaseCreditNote>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public void CreateDraft_exige_ApplicationType_valido()
    {
        var validator = new CreateDraftPurchaseCreditNoteValidator();
        var command = new CreateDraftPurchaseCreditNoteCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            (PurchaseCreditNoteApplicationType)0,
            "001-001-000000005",
            null,
            null,
            null,
            DateOnly.FromDateTime(DateTime.UtcNow),
            "Descuento",
            OneLine()
        );

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateDraftPurchaseCreditNoteCommand.ApplicationType));
    }

    private static CreateDraftPurchaseCreditNoteCommand ReturnCommand(Fixture f,
        IReadOnlyList<PurchaseReturnDraftLineInput> lines, Guid? receptionId = null) =>
        new(Guid.NewGuid(), f.Invoice.Id, receptionId, PurchaseCreditNoteApplicationType.Return,
            "001-001-000000013", null, null, null, f.Invoice.IssueDate, "Devolucion", [], ReturnLines: lines);

    [Fact]
    public async Task Return_partial_line_saves_linked_draft_and_server_amounts_without_financial_effects()
    {
        var f = BuildFixture();
        var m = new Mocks(f);
        var result = await m.BuildCreateHandler().Handle(ReturnCommand(f,
            [new(f.Invoice.Lines[0].Id, 0.25m)]), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalAmount.Should().Be(250m);
        result.Value.Lines.Single().PurchaseInvoiceDetailId.Should().Be(f.Invoice.Lines[0].Id);
        result.Value.Lines.Single().Quantity.Should().Be(0.25m);
        result.Value.LinkedPurchaseReturnId.Should().NotBeNull();
        result.Value.Status.Should().Be("Draft");
        f.Payable.OutstandingAmount.Should().Be(f.Payable.TotalAmount);
        m.ReturnRepo.Verify(r => r.AddAsync(It.Is<PurchaseReturn>(x => x.Id == result.Value.LinkedPurchaseReturnId),
            It.IsAny<CancellationToken>()), Times.Once);
        m.ReturnRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        m.CreditNoteRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Return_multiple_lines_prorates_base_IVA_ICE_and_IRBPNR()
    {
        var f = BuildFixture(secondLine: true);
        var m = new Mocks(f);
        var result = await m.BuildCreateHandler().Handle(ReturnCommand(f,
            [new(f.Invoice.Lines[0].Id, .5m), new(f.Invoice.Lines[1].Id, 1m)]), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        result.Value!.Lines.Should().HaveCount(2);
        result.Value.Subtotal.Should().Be(550m);
        result.Value.IceAmount.Should().Be(5m);
        result.Value.VatAmount.Should().Be(8.25m);
        result.Value.Lines.Sum(l => l.IrbpnrAmount).Should().Be(1m);
        result.Value.TotalAmount.Should().Be(564.25m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1.01)]
    [InlineData(0.00001)]
    public async Task Return_rejects_invalid_quantity(decimal quantity)
    {
        var f = BuildFixture(); var m = new Mocks(f);
        var result = await m.BuildCreateHandler().Handle(ReturnCommand(f,
            [new(f.Invoice.Lines[0].Id, quantity)]), CancellationToken.None);
        result.IsSuccess.Should().BeFalse();
        m.CreditNoteRepo.Verify(r => r.AddAsync(It.IsAny<PurchaseCreditNote>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Return_rejects_previous_partial_returns_foreign_products_duplicates_and_free_lines()
    {
        var f = BuildFixture(); var m = new Mocks(f); var id = f.Invoice.Lines[0].Id;
        m.ReturnRepo.Setup(r => r.GetReturnedQuantitiesByInvoiceDetailIdsAsync(TenantId,
            It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, decimal> { [id] = .75m });
        foreach (var inputs in new PurchaseReturnDraftLineInput[][] {
            [new(id, .3m)], [new(Guid.NewGuid(), .1m)], [new(id, .1m), new(id, .1m)] })
        {
            var result = await m.BuildCreateHandler().Handle(ReturnCommand(f, inputs), CancellationToken.None);
            result.IsSuccess.Should().BeFalse();
        }
        var free = await m.BuildCreateHandler().Handle(ReturnCommand(f, []) with { Lines = OneLine() }, CancellationToken.None);
        free.IsSuccess.Should().BeFalse();
        var valid = await m.BuildCreateHandler().Handle(ReturnCommand(f, [new(id, .25m)]), CancellationToken.None);
        valid.IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(.115, true)]
    [InlineData(.2, false)]
    public async Task Return_validates_received_XML_total(decimal quantity, bool success)
    {
        var f = BuildFixture(); var m = new Mocks(f);
        var reception = BuildReceptionDoc(SupplierId, f.Invoice.InvoiceNumber);
        m.ReceptionRepo.Setup(r => r.GetByIdAsync(TenantId, reception.Id, It.IsAny<CancellationToken>())).ReturnsAsync(reception);
        var result = await m.BuildCreateHandler().Handle(ReturnCommand(f,
            [new(f.Invoice.Lines[0].Id, quantity)], reception.Id), CancellationToken.None);
        result.IsSuccess.Should().Be(success);
    }

    [Fact]
    public async Task CreateDraft_con_ReceptionDocumentId_valido_vincula_documento()
    {
        var f = BuildFixture();
        var doc = BuildReceptionDoc(SupplierId, modifiedDocumentNumber: f.Invoice.InvoiceNumber);
        var m = new Mocks(f);
        m.ReceptionRepo
            .Setup(r => r.GetByIdAsync(TenantId, doc.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc);
        var handler = m.BuildCreateHandler();

        var result = await handler.Handle(
            new CreateDraftPurchaseCreditNoteCommand(
                Guid.NewGuid(),
                f.Invoice.Id,
                doc.Id,
                PurchaseCreditNoteApplicationType.Discount,
                "001-001-000000006",
                null,
                null,
                null,
                DateOnly.FromDateTime(DateTime.UtcNow),
                "Descuento",
                OneLine()
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.ReceptionDocumentId.Should().Be(doc.Id);
        result.Value.ReceptionDocumentAccessKey.Should().Be(doc.AccessKey);
    }

    // ── 3. CreateDraft rechaza ReceptionDocumentId ya usado ─────────────

    [Fact]
    public async Task CreateDraft_rechaza_ReceptionDocumentId_ya_usado()
    {
        var f = BuildFixture();
        var doc = BuildReceptionDoc(SupplierId);
        var m = new Mocks(f);
        m.ReceptionRepo
            .Setup(r => r.GetByIdAsync(TenantId, doc.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc);
        m.CreditNoteRepo
            .Setup(r =>
                r.ExistsByReceptionDocumentIdAsync(TenantId, doc.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(true);
        var handler = m.BuildCreateHandler();

        var result = await handler.Handle(
            new CreateDraftPurchaseCreditNoteCommand(
                Guid.NewGuid(),
                f.Invoice.Id,
                doc.Id,
                PurchaseCreditNoteApplicationType.Discount,
                "001-001-000000007",
                null,
                null,
                null,
                DateOnly.FromDateTime(DateTime.UtcNow),
                "Descuento",
                OneLine()
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("vinculado a otra nota de crédito");
    }

    // ── 4. CreateDraft rechaza factura/proveedor inconsistente ──────────

    [Fact]
    public async Task CreateDraft_rechaza_proveedor_del_documento_de_recepcion_inconsistente()
    {
        var f = BuildFixture();
        var otroProveedor = Guid.NewGuid();
        var doc = BuildReceptionDoc(otroProveedor);
        var m = new Mocks(f);
        m.ReceptionRepo
            .Setup(r => r.GetByIdAsync(TenantId, doc.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc);
        var handler = m.BuildCreateHandler();

        var result = await handler.Handle(
            new CreateDraftPurchaseCreditNoteCommand(
                Guid.NewGuid(),
                f.Invoice.Id,
                doc.Id,
                PurchaseCreditNoteApplicationType.Discount,
                "001-001-000000008",
                null,
                null,
                null,
                DateOnly.FromDateTime(DateTime.UtcNow),
                "Descuento",
                OneLine()
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("proveedor del documento de recepción no coincide");
    }

    [Fact]
    public async Task CreateDraft_rechaza_factura_afectada_inconsistente_con_documento_de_recepcion()
    {
        var f = BuildFixture();
        var doc = BuildReceptionDoc(SupplierId, modifiedDocumentNumber: "OTRA-FACTURA-999");
        var m = new Mocks(f);
        m.ReceptionRepo
            .Setup(r => r.GetByIdAsync(TenantId, doc.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc);
        var handler = m.BuildCreateHandler();

        var result = await handler.Handle(
            new CreateDraftPurchaseCreditNoteCommand(
                Guid.NewGuid(),
                f.Invoice.Id,
                doc.Id,
                PurchaseCreditNoteApplicationType.Discount,
                "001-001-000000009",
                null,
                null,
                null,
                DateOnly.FromDateTime(DateTime.UtcNow),
                "Descuento",
                OneLine()
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("no coincide con la factura seleccionada");
    }

    // ── CreateDraft: factura sin saldo pendiente ────────────────────────

    [Fact]
    public async Task CreateDraft_rechaza_factura_sin_saldo_pendiente()
    {
        var f = BuildFixture(totalAmount: 1000m, paidAmount: 1000m);
        var m = new Mocks(f);
        var handler = m.BuildCreateHandler();

        var result = await handler.Handle(
            new CreateDraftPurchaseCreditNoteCommand(
                Guid.NewGuid(),
                f.Invoice.Id,
                null,
                PurchaseCreditNoteApplicationType.Discount,
                "001-001-000000010",
                null,
                null,
                null,
                DateOnly.FromDateTime(DateTime.UtcNow),
                "Descuento",
                OneLine()
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("no tiene saldo pendiente");
    }

    // ── Duplicados traducidos vía IDatabaseExceptionTranslator (carrera) ──

    [Fact]
    public void MapUniqueViolation_traduce_los_3_constraints_de_duplicados()
    {
        CreateDraftPurchaseCreditNoteHandler
            .MapUniqueViolation("uq_purchase_credit_notes_tenant_reception_document_id")
            .Should()
            .Contain("documento de recepción");
        CreateDraftPurchaseCreditNoteHandler
            .MapUniqueViolation("uq_purchase_credit_notes_tenant_access_key")
            .Should()
            .Contain("clave de acceso");
        CreateDraftPurchaseCreditNoteHandler
            .MapUniqueViolation("uq_purchase_credit_notes_tenant_company_supplier_number")
            .Should()
            .Contain("número para este proveedor");
        CreateDraftPurchaseCreditNoteHandler.MapUniqueViolation("otro_constraint").Should().BeNull();
    }

    // ── 5. Update solo en Draft ──────────────────────────────────────────

    [Fact]
    public async Task UpdateDraft_sobre_nota_ya_autorizada_rechaza()
    {
        var f = BuildFixture();
        var creditNote = PurchaseCreditNote.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            SupplierId,
            f.Invoice.Id,
            null,
            PurchaseCreditNoteApplicationType.Discount,
            "001-001-000000011",
            null,
            null,
            null,
            DateOnly.FromDateTime(DateTime.UtcNow),
            "Descuento",
            new[] { new PurchaseCreditNote.DraftLineInput("Descuento", 100m, "2", 15m, 15m) },
            Array.Empty<PurchaseCreditNote.TaxSummaryDraftLineInput>(),
            UserId,
            Guid.NewGuid(),
            "hash"
        );
        creditNote.Authorize(1000m, UserId, Guid.NewGuid(), "auth-hash");

        var m = new Mocks(f);
        m.CreditNoteRepo
            .Setup(r => r.GetByIdAsync(TenantId, creditNote.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(creditNote);
        var handler = m.BuildUpdateHandler();

        var result = await handler.Handle(
            new UpdatePurchaseCreditNoteDraftCommand(
                creditNote.Id,
                "001-001-000000011",
                null,
                null,
                null,
                DateOnly.FromDateTime(DateTime.UtcNow),
                "Nuevo motivo",
                OneLine()
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("ya no está en borrador");
        m.CreditNoteRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateDraft_en_borrador_reemplaza_lineas_y_motivo()
    {
        var f = BuildFixture();
        var creditNote = PurchaseCreditNote.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            SupplierId,
            f.Invoice.Id,
            null,
            PurchaseCreditNoteApplicationType.Discount,
            "001-001-000000012",
            null,
            null,
            null,
            DateOnly.FromDateTime(DateTime.UtcNow),
            "Descuento",
            new[] { new PurchaseCreditNote.DraftLineInput("Descuento", 100m, "2", 15m, 15m) },
            Array.Empty<PurchaseCreditNote.TaxSummaryDraftLineInput>(),
            UserId,
            Guid.NewGuid(),
            "hash"
        );

        var m = new Mocks(f);
        m.CreditNoteRepo
            .Setup(r => r.GetByIdAsync(TenantId, creditNote.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(creditNote);
        var handler = m.BuildUpdateHandler();

        var result = await handler.Handle(
            new UpdatePurchaseCreditNoteDraftCommand(
                creditNote.Id,
                "001-001-000000012",
                null,
                null,
                null,
                DateOnly.FromDateTime(DateTime.UtcNow),
                "Nuevo motivo",
                OneLine(subtotal: 200m)
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.Reason.Should().Be("Nuevo motivo");
        result.Value.TotalAmount.Should().Be(230m);
    }
    [Fact]
    public async Task UpdateDraft_preserves_reception_access_key()
    {
        var f = BuildFixture();
        var creditNote = PurchaseCreditNote.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            SupplierId,
            f.Invoice.Id,
            Guid.NewGuid(),
            PurchaseCreditNoteApplicationType.Discount,
            "001-001-000000012",
            "RECEPTION-KEY",
            null,
            null,
            DateOnly.FromDateTime(DateTime.UtcNow),
            "Descuento",
            new[] { new PurchaseCreditNote.DraftLineInput("Descuento", 100m, "2", 15m, 15m) },
            Array.Empty<PurchaseCreditNote.TaxSummaryDraftLineInput>(),
            UserId,
            Guid.NewGuid(),
            "hash"
        );

        var m = new Mocks(f);
        m.CreditNoteRepo
            .Setup(r => r.GetByIdAsync(TenantId, creditNote.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(creditNote);
        var handler = m.BuildUpdateHandler();

        var result = await handler.Handle(
            new UpdatePurchaseCreditNoteDraftCommand(
                creditNote.Id,
                "001-001-000000012",
                "CLIENT-KEY",
                null,
                null,
                DateOnly.FromDateTime(DateTime.UtcNow),
                "Nuevo motivo",
                OneLine(subtotal: 200m)
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.Reason.Should().Be("Nuevo motivo");
        result.Value.AccessKey.Should().Be("RECEPTION-KEY");
    }
}
