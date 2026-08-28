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
/// FLOW-READY-02C.2 — <c>AuthorizePurchaseCreditNoteHandler</c>: aplica <c>CreditNoteAmount</c>,
/// bloquea por excedente (ajuste obligatorio §4.2, nunca trunca), congela snapshot financiero, marca
/// procesado el documento de recepción vinculado, nunca toca inventario/contabilidad, e idempotencia.
/// </summary>
public sealed class AuthorizePurchaseCreditNoteHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid PaymentTermId = Guid.NewGuid();
    private static readonly Guid WarehouseId = Guid.NewGuid();

    private sealed record Fixture(
        PurchaseInvoice Invoice,
        AccountsPayable Payable,
        PurchaseCreditNote CreditNote
    );

    private static Fixture BuildFixture(
        decimal totalAmount = 1000m,
        decimal paidAmount = 0m,
        decimal creditNoteSubtotal = 100m,
        decimal creditNoteVat = 15m,
        Guid? receptionDocumentId = null,
        PurchaseCreditNoteApplicationType applicationType = PurchaseCreditNoteApplicationType.Discount
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

        var payable = AccountsPayable.CreateFromOrigin(
            TenantId, CompanyId, BranchId, SupplierId,
            AccountsPayableOriginType.PurchaseInvoice, invoice.Id,
            "01", "001-001-000000001",
            invoice.IssueDate, invoice.IssueDate, UserId
        );
        payable.AddInstallment(1, invoice.IssueDate.AddDays(30), totalAmount);
        if (paidAmount > 0)
            payable.RegisterPayment(paidAmount, UserId);

        var creditNote = PurchaseCreditNote.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            SupplierId,
            invoice.Id,
            receptionDocumentId,
            applicationType,
            "001-001-000000005",
            null,
            null,
            null,
            DateOnly.FromDateTime(DateTime.UtcNow),
            "Descuento por volumen",
            new[]
            {
                new PurchaseCreditNote.DraftLineInput(
                    "Descuento",
                    creditNoteSubtotal,
                    "2",
                    15m,
                    creditNoteVat
                ),
            },
            Array.Empty<PurchaseCreditNote.TaxSummaryDraftLineInput>(),
            UserId,
            Guid.NewGuid(),
            "create-hash"
        );

        return new Fixture(invoice, payable, creditNote);
    }

    private sealed class Mocks
    {
        public Mock<IPurchaseCreditNoteRepository> CreditNoteRepo { get; } = new();
        public Mock<IPurchaseInvoiceRepository> InvoiceRepo { get; } = new();
        public Mock<IAccountsPayableRepository> PayableRepo { get; } = new();
        public Mock<IPurchaseReturnRepository> LockRepo { get; } = new();
        public Mock<IPurchaseReceptionDocumentRepository> ReceptionRepo { get; } = new();
        public Mock<IUnitOfWork> Uow { get; } = new();
        public Mock<IDatabaseExceptionTranslator> DbEx { get; } = new();

        public Mocks(Fixture f)
        {
            CreditNoteRepo
                .Setup(r =>
                    r.GetPurchaseInvoiceIdAsync(TenantId, f.CreditNote.Id, It.IsAny<CancellationToken>())
                )
                .ReturnsAsync(f.Invoice.Id);
            CreditNoteRepo
                .Setup(r => r.GetByIdAsync(TenantId, f.CreditNote.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(f.CreditNote);
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
            Uow.SetupGet(u => u.HasActiveTransaction).Returns(true);
        }

        public AuthorizePurchaseCreditNoteHandler BuildHandler() =>
            new(
                CreditNoteRepo.Object,
                InvoiceRepo.Object,
                PayableRepo.Object,
                LockRepo.Object,
                ReceptionRepo.Object,
                Uow.Object,
                DbEx.Object,
                FixedTenant(),
                FixedUser()
            );
    }

    private static ICurrentTenant FixedTenant()
    {
        var m = new Mock<ICurrentTenant>();
        m.SetupGet(x => x.TenantId).Returns(TenantId);
        return m.Object;
    }

    private static ICurrentUser FixedUser()
    {
        var m = new Mock<ICurrentUser>();
        m.SetupGet(x => x.UserId).Returns(UserId);
        return m.Object;
    }

    // ── 6. Authorize válido aplica CreditNoteAmount ──────────────

    [Fact]
    public async Task Authorize_valido_aplica_CreditNoteAmount_en_PurchasePayable()
    {
        var f = BuildFixture(totalAmount: 1000m, creditNoteSubtotal: 100m, creditNoteVat: 15m);
        var m = new Mocks(f);
        var handler = m.BuildHandler();

        var result = await handler.Handle(
            new AuthorizePurchaseCreditNoteCommand(f.CreditNote.Id, Guid.NewGuid()),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        f.CreditNote.Status.Should().Be(PurchaseCreditNoteStatus.Authorized);
        f.Payable.CreditNoteAmount.Should().Be(115m);
        f.Payable.OutstandingAmount.Should().Be(885m);
        m.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── FLOW-READY-02C-R1.1: Authorize sobre tipo Return siempre falla ──

    [Fact]
    public async Task Authorize_sobre_nota_de_credito_tipo_Return_falla_y_no_aplica_nada()
    {
        var f = BuildFixture(applicationType: PurchaseCreditNoteApplicationType.Return);
        var m = new Mocks(f);
        var handler = m.BuildHandler();

        var result = await handler.Handle(
            new AuthorizePurchaseCreditNoteCommand(f.CreditNote.Id, Guid.NewGuid()),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        f.CreditNote.Status.Should().Be(PurchaseCreditNoteStatus.Draft);
        f.Payable.CreditNoteAmount.Should().Be(0m);
        m.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── 7. Authorize bloquea TotalAmount > BalanceDue ───────────────────

    [Fact]
    public async Task Authorize_bloquea_cuando_TotalAmount_excede_BalanceDue_y_no_aplica_nada()
    {
        // BalanceDue = 1000 - 950 = 50; NC total = 115 > 50.
        var f = BuildFixture(totalAmount: 1000m, paidAmount: 950m, creditNoteSubtotal: 100m, creditNoteVat: 15m);
        var m = new Mocks(f);
        var handler = m.BuildHandler();

        var result = await handler.Handle(
            new AuthorizePurchaseCreditNoteCommand(f.CreditNote.Id, Guid.NewGuid()),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("excede");
        f.CreditNote.Status.Should().Be(PurchaseCreditNoteStatus.Draft);
        f.Payable.CreditNoteAmount.Should().Be(0m);
        m.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        m.Uow.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── 8. Authorize congela snapshot financiero ────────────────────────

    [Fact]
    public async Task Authorize_congela_snapshot_financiero()
    {
        var f = BuildFixture(totalAmount: 1000m, creditNoteSubtotal: 300m, creditNoteVat: 45m);
        var m = new Mocks(f);
        var handler = m.BuildHandler();

        await handler.Handle(
            new AuthorizePurchaseCreditNoteCommand(f.CreditNote.Id, Guid.NewGuid()),
            CancellationToken.None
        );

        f.CreditNote.Subtotal.Should().Be(300m);
        f.CreditNote.VatAmount.Should().Be(45m);
        f.CreditNote.TotalAmount.Should().Be(345m);
        f.CreditNote.AppliedToPayableAmount.Should().Be(345m);
        f.CreditNote.AuthorizedAtUtc.Should().NotBeNull();
        f.CreditNote.AuthorizedByUserId.Should().Be(UserId);
    }

    // ── 9. Authorize marca ReceptionDocument como procesado si existe ───

    [Fact]
    public async Task Authorize_marca_ReceptionDocument_como_procesado_si_existe_y_esta_Verified()
    {
        var doc = PurchaseReceptionDocument.Create(
            TenantId,
            CompanyId,
            BranchId,
            PurchaseReceptionSourceDocType.CreditNote,
            "1710034065001",
            "Proveedor Test",
            SupplierId,
            $"AK-{Guid.NewGuid():N}",
            "001-001-000000099",
            DateOnly.FromDateTime(DateTime.UtcNow),
            DateTime.UtcNow,
            100m,
            15m,
            115m,
            UserId
        );
        doc.MarkVerified(UserId);

        var f = BuildFixture(totalAmount: 1000m, receptionDocumentId: doc.Id);
        var m = new Mocks(f);
        m.ReceptionRepo
            .Setup(r => r.GetByIdAsync(TenantId, doc.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc);
        var handler = m.BuildHandler();

        var result = await handler.Handle(
            new AuthorizePurchaseCreditNoteCommand(f.CreditNote.Id, Guid.NewGuid()),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        doc.Status.Should().Be(PurchaseReceptionDocumentStatus.Processed);
        doc.PurchaseId.Should().Be(f.Invoice.Id);
    }

    [Fact]
    public async Task Authorize_falla_si_ReceptionDocument_no_esta_Verified()
    {
        var doc = PurchaseReceptionDocument.Create(
            TenantId,
            CompanyId,
            BranchId,
            PurchaseReceptionSourceDocType.CreditNote,
            "1710034065001",
            "Proveedor Test",
            SupplierId,
            $"AK-{Guid.NewGuid():N}",
            "001-001-000000099",
            DateOnly.FromDateTime(DateTime.UtcNow),
            DateTime.UtcNow,
            100m,
            15m,
            115m,
            UserId
        );
        // Sigue en Imported — nunca se descargó/verificó el XML.

        var f = BuildFixture(totalAmount: 1000m, receptionDocumentId: doc.Id);
        var m = new Mocks(f);
        m.ReceptionRepo
            .Setup(r => r.GetByIdAsync(TenantId, doc.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc);
        var handler = m.BuildHandler();

        var result = await handler.Handle(
            new AuthorizePurchaseCreditNoteCommand(f.CreditNote.Id, Guid.NewGuid()),
            CancellationToken.None
        );

        // El fallo ocurre después de PurchaseCreditNote.Authorize() (que ya mutó el agregado en
        // memoria) pero antes del commit — mismo orden que AuthorizePurchaseReturnHandler con sus
        // movimientos de stock: la transacción de BD se revierte por completo (nunca hay commit),
        // el objeto en memoria de esta invocación fallida simplemente se descarta.
        result.IsSuccess.Should().BeFalse();
        m.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        m.Uow.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    // ── 10/11. No inventario, no contabilidad (estructural) ─────────────

    [Fact]
    public void Handler_no_depende_de_IStockRepository_ni_de_ningun_repositorio_contable()
    {
        var ctor = typeof(AuthorizePurchaseCreditNoteHandler).GetConstructors().Single();
        var paramTypeNames = ctor.GetParameters().Select(p => p.ParameterType.Name).ToList();

        paramTypeNames.Should().NotContain("IStockRepository");
        paramTypeNames.Should().NotContain(n => n.Contains("Posting", StringComparison.Ordinal));
        paramTypeNames.Should().NotContain(n => n.Contains("Accounting", StringComparison.Ordinal));
        paramTypeNames.Should().NotContain(n => n.Contains("JournalEntry", StringComparison.Ordinal));
    }

    // ── Idempotencia ─────────────────────────────────────────────────────

    [Fact]
    public async Task Authorize_repetido_con_mismo_ClientRequestId_retorna_el_mismo_snapshot_sin_reejecutar()
    {
        var f = BuildFixture();
        var m = new Mocks(f);
        var handler = m.BuildHandler();
        var clientRequestId = Guid.NewGuid();

        var first = await handler.Handle(
            new AuthorizePurchaseCreditNoteCommand(f.CreditNote.Id, clientRequestId),
            CancellationToken.None
        );
        first.IsSuccess.Should().BeTrue();

        var retry = await handler.Handle(
            new AuthorizePurchaseCreditNoteCommand(f.CreditNote.Id, clientRequestId),
            CancellationToken.None
        );

        retry.IsSuccess.Should().BeTrue();
        retry.Value!.Id.Should().Be(first.Value!.Id);
        f.Payable.CreditNoteAmount.Should().Be(115m); // no se aplicó dos veces
    }

    [Fact]
    public async Task Authorize_repetido_con_ClientRequestId_distinto_sobre_ya_autorizada_rechaza()
    {
        var f = BuildFixture();
        var m = new Mocks(f);
        var handler = m.BuildHandler();

        await handler.Handle(
            new AuthorizePurchaseCreditNoteCommand(f.CreditNote.Id, Guid.NewGuid()),
            CancellationToken.None
        );

        var result = await handler.Handle(
            new AuthorizePurchaseCreditNoteCommand(f.CreditNote.Id, Guid.NewGuid()),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("con datos distintos");
    }
}
