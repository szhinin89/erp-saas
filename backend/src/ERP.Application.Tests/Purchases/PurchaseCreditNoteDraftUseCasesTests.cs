using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.Purchases.UseCases;
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

    private sealed record Fixture(PurchaseInvoice Invoice, PurchasePayable Payable);

    private static Fixture BuildFixture(decimal totalAmount = 1000m, decimal paidAmount = 0m)
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
        invoice.ReplaceLines(new[] { line }, UserId);
        invoice.Confirm(UserId);

        var payable = PurchasePayable.Create(TenantId, CompanyId, invoice.Id, SupplierId, totalAmount, UserId);
        if (paidAmount > 0)
            payable.RegisterPayment(paidAmount, UserId);

        return new Fixture(invoice, payable);
    }

    private static PurchaseReceptionDocument BuildReceptionDoc(
        Guid? supplierId,
        string? modifiedDocumentNumber = null
    ) =>
        PurchaseReceptionDocument.Create(
            TenantId,
            CompanyId,
            BranchId,
            PurchaseReceptionSourceDocType.CreditNote,
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
        public Mock<IPurchaseReceptionDocumentRepository> ReceptionRepo { get; } = new();
        public Mock<IDatabaseExceptionTranslator> DbEx { get; } = new();

        public Mocks(Fixture f)
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

        public UpdatePurchaseCreditNoteDraftHandler BuildUpdateHandler() =>
            new(CreditNoteRepo.Object, InvoiceRepo.Object, DbEx.Object, FixedTenant(), FixedCompany(), FixedUser());
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

    [Fact]
    public async Task CreateDraft_Return_guarda_cabecera_fiscal_sin_aplicar_CxP()
    {
        var f = BuildFixture();
        var m = new Mocks(f);
        var handler = m.BuildCreateHandler();

        var result = await handler.Handle(
            new CreateDraftPurchaseCreditNoteCommand(
                Guid.NewGuid(),
                f.Invoice.Id,
                null,
                PurchaseCreditNoteApplicationType.Return,
                "001-001-000000013",
                null,
                null,
                null,
                DateOnly.FromDateTime(DateTime.UtcNow),
                "Devolución de mercadería",
                OneLine()
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.ApplicationType.Should().Be("Return");
        result.Value.Status.Should().Be("Draft");
        result.Value.AppliedToPayableAmount.Should().BeNull();
        result.Value.LinkedPurchaseReturnId.Should().BeNull();
        f.Payable.BalanceDue.Should().Be(f.Payable.TotalAmount); // CxP intacta
    }

    // ── 2. CreateDraft con ReceptionDocumentId válido ───────────────────

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
}
