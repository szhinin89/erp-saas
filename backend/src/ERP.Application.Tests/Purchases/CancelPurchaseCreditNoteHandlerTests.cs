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
/// FLOW-READY-02C.2 — <c>CancelPurchaseCreditNoteHandler</c>: reversa de
/// <c>PurchasePayable.CreditNoteAmount</c> cuando estaba <c>Authorized</c>, sin reversas
/// desde <c>Draft</c>, nunca toca inventario/contabilidad, e idempotencia.
/// </summary>
public sealed class CancelPurchaseCreditNoteHandlerTests
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
        PurchaseCreditNote CreditNote,
        PurchaseReceptionDocument? ReceptionDoc
    );

    private static Fixture BuildFixture(bool authorized, decimal totalAmount = 1000m, bool withReception = false)
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

        PurchaseReceptionDocument? receptionDoc = null;
        if (withReception)
        {
            receptionDoc = PurchaseReceptionDocument.Create(
                TenantId, CompanyId, BranchId, PurchaseReceptionSourceDocType.CreditNote,
                supplierRuc: "1710034065001", supplierName: "Proveedor Test", supplierId: SupplierId,
                accessKey: $"AK-{Guid.NewGuid():N}", invoiceNumber: "001-001-000000099",
                issueDate: DateOnly.FromDateTime(DateTime.UtcNow), authorizationDate: DateTime.UtcNow,
                subtotal: 100m, vatAmount: 15m, totalAmount: 115m, createdBy: UserId
            );
            receptionDoc.MarkVerified(UserId);
        }

        var creditNote = PurchaseCreditNote.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            SupplierId,
            invoice.Id,
            receptionDoc?.Id,
            PurchaseCreditNoteApplicationType.Discount,
            "001-001-000000005",
            null,
            null,
            null,
            DateOnly.FromDateTime(DateTime.UtcNow),
            "Descuento",
            new[] { new PurchaseCreditNote.DraftLineInput("Descuento", 100m, "2", 15m, 15m) },
            Array.Empty<PurchaseCreditNote.TaxSummaryDraftLineInput>(),
            UserId,
            Guid.NewGuid(),
            "create-hash"
        );

        if (authorized)
        {
            creditNote.Authorize(payable.OutstandingAmount, UserId, Guid.NewGuid(), "auth-hash");
            payable.ApplyCreditNote(creditNote.AppliedToPayableAmount!.Value, UserId);
            receptionDoc?.MarkProcessed(invoice.Id, UserId);
        }

        return new Fixture(invoice, payable, creditNote, receptionDoc);
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
            if (f.ReceptionDoc is not null)
            {
                ReceptionRepo
                    .Setup(r => r.GetByIdAsync(TenantId, f.ReceptionDoc.Id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(f.ReceptionDoc);
            }

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

        public CancelPurchaseCreditNoteHandler BuildHandler() =>
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

    // ── 12. Cancel reversa CreditNoteAmount ──────────────────────

    [Fact]
    public async Task Cancel_desde_Authorized_reversa_CreditNoteAmount()
    {
        var f = BuildFixture(authorized: true);
        f.Payable.CreditNoteAmount.Should().Be(115m); // precondición

        var m = new Mocks(f);
        var handler = m.BuildHandler();

        var result = await handler.Handle(
            new CancelPurchaseCreditNoteCommand(f.CreditNote.Id, "Corrección", Guid.NewGuid()),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        f.CreditNote.Status.Should().Be(PurchaseCreditNoteStatus.Cancelled);
        f.Payable.CreditNoteAmount.Should().Be(0m);
        f.Payable.OutstandingAmount.Should().Be(1000m);
    }

    [Fact]
    public async Task Cancel_desde_Draft_no_reversa_nada_porque_nunca_se_aplico()
    {
        var f = BuildFixture(authorized: false);
        var m = new Mocks(f);
        var handler = m.BuildHandler();

        var result = await handler.Handle(
            new CancelPurchaseCreditNoteCommand(f.CreditNote.Id, "Ya no aplica", Guid.NewGuid()),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        f.CreditNote.Status.Should().Be(PurchaseCreditNoteStatus.Cancelled);
        f.Payable.CreditNoteAmount.Should().Be(0m);
    }

    // ── PURCHASE-CREDIT-NOTE-DISCOUNT-CANCEL-RELEASES-RECEPTION-01 ──────

    [Fact]
    public async Task Cancel_NC_Discount_Authorized_libera_documento_de_recepcion_para_reproceso()
    {
        var f = BuildFixture(authorized: true, withReception: true);
        f.ReceptionDoc.Should().NotBeNull();
        f.ReceptionDoc!.Status.Should().Be(PurchaseReceptionDocumentStatus.Processed);
        f.ReceptionDoc.PurchaseId.Should().Be(f.Invoice.Id);

        var m = new Mocks(f);
        var handler = m.BuildHandler();

        var result = await handler.Handle(
            new CancelPurchaseCreditNoteCommand(f.CreditNote.Id, "Corrección", Guid.NewGuid()),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        f.CreditNote.Status.Should().Be(PurchaseCreditNoteStatus.Cancelled);
        f.ReceptionDoc.Status.Should().Be(PurchaseReceptionDocumentStatus.Verified);
        f.ReceptionDoc.PurchaseId.Should().BeNull();
    }

    [Fact]
    public async Task Cancel_NC_Discount_Draft_con_recepcion_vinculada_no_falla_ni_intenta_liberar()
    {
        var f = BuildFixture(authorized: false, withReception: true);
        f.ReceptionDoc.Should().NotBeNull();
        f.ReceptionDoc!.Status.Should().Be(PurchaseReceptionDocumentStatus.Verified); // nunca llegó a Processed

        var m = new Mocks(f);
        var handler = m.BuildHandler();

        var result = await handler.Handle(
            new CancelPurchaseCreditNoteCommand(f.CreditNote.Id, "Ya no aplica", Guid.NewGuid()),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        f.CreditNote.Status.Should().Be(PurchaseCreditNoteStatus.Cancelled);
        f.ReceptionDoc.Status.Should().Be(PurchaseReceptionDocumentStatus.Verified);
        m.ReceptionRepo.Verify(
            r => r.GetByIdAsync(TenantId, f.ReceptionDoc.Id, It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    // ── 13. Cancel no toca inventario/contabilidad (estructural) ────────

    [Fact]
    public void Handler_no_depende_de_IStockRepository_ni_de_ningun_repositorio_contable()
    {
        var ctor = typeof(CancelPurchaseCreditNoteHandler).GetConstructors().Single();
        var paramTypeNames = ctor.GetParameters().Select(p => p.ParameterType.Name).ToList();

        paramTypeNames.Should().NotContain("IStockRepository");
        paramTypeNames.Should().NotContain(n => n.Contains("Posting", StringComparison.Ordinal));
        paramTypeNames.Should().NotContain(n => n.Contains("Accounting", StringComparison.Ordinal));
        paramTypeNames.Should().NotContain(n => n.Contains("JournalEntry", StringComparison.Ordinal));
    }

    // ── Idempotencia ─────────────────────────────────────────────────────

    [Fact]
    public async Task Cancel_repetido_con_mismo_ClientRequestId_retorna_el_mismo_snapshot_sin_reejecutar()
    {
        var f = BuildFixture(authorized: true);
        var m = new Mocks(f);
        var handler = m.BuildHandler();
        var clientRequestId = Guid.NewGuid();

        var first = await handler.Handle(
            new CancelPurchaseCreditNoteCommand(f.CreditNote.Id, "Motivo", clientRequestId),
            CancellationToken.None
        );
        first.IsSuccess.Should().BeTrue();

        var retry = await handler.Handle(
            new CancelPurchaseCreditNoteCommand(f.CreditNote.Id, "Motivo", clientRequestId),
            CancellationToken.None
        );

        retry.IsSuccess.Should().BeTrue();
        f.Payable.CreditNoteAmount.Should().Be(0m); // no reversado dos veces
    }

    [Fact]
    public async Task Cancel_ya_cancelada_con_ClientRequestId_distinto_rechaza()
    {
        var f = BuildFixture(authorized: false);
        var m = new Mocks(f);
        var handler = m.BuildHandler();

        await handler.Handle(
            new CancelPurchaseCreditNoteCommand(f.CreditNote.Id, "Motivo", Guid.NewGuid()),
            CancellationToken.None
        );

        var result = await handler.Handle(
            new CancelPurchaseCreditNoteCommand(f.CreditNote.Id, "Motivo", Guid.NewGuid()),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("ya está cancelada");
    }
}
