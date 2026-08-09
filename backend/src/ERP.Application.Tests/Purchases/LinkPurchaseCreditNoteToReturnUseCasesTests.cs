using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Purchases;

/// <summary>
/// FLOW-READY-02C-R1.1 — <c>LinkPurchaseCreditNoteToReturnHandler</c>: vínculo válido, rechazo por
/// tipo distinto de Return, ya vinculada, y datos inconsistentes (factura/proveedor/empresa/sucursal).
/// Nunca toca inventario/CxP/contabilidad — solo persiste <c>LinkedPurchaseReturnId</c>.
/// </summary>
public sealed class LinkPurchaseCreditNoteToReturnUseCasesTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid PurchaseInvoiceId = Guid.NewGuid();

    private static PurchaseCreditNote BuildCreditNote(
        PurchaseCreditNoteApplicationType applicationType = PurchaseCreditNoteApplicationType.Return
    ) =>
        PurchaseCreditNote.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            SupplierId,
            PurchaseInvoiceId,
            null,
            applicationType,
            "001-001-000000005",
            null,
            null,
            null,
            DateOnly.FromDateTime(DateTime.UtcNow),
            "Devolución de mercadería",
            new[] { new PurchaseCreditNote.DraftLineInput("Devolución", 100m, "2", 15m, 15m) },
            Array.Empty<PurchaseCreditNote.TaxSummaryDraftLineInput>(),
            UserId,
            Guid.NewGuid(),
            "create-hash"
        );

    private static PurchaseReturn BuildReturn(
        Guid? companyId = null,
        Guid? branchId = null,
        Guid? purchaseInvoiceId = null,
        Guid? supplierId = null
    ) =>
        PurchaseReturn.CreateDraft(
            TenantId,
            companyId ?? CompanyId,
            branchId ?? BranchId,
            purchaseInvoiceId ?? PurchaseInvoiceId,
            supplierId ?? SupplierId,
            "Producto defectuoso",
            new[]
            {
                new PurchaseReturn.DraftLineInput(Guid.NewGuid(), Guid.NewGuid(), 1m, Guid.NewGuid()),
            },
            UserId,
            Guid.NewGuid(),
            "return-create-hash"
        );

    private sealed class Mocks
    {
        public Mock<IPurchaseCreditNoteRepository> CreditNoteRepo { get; } = new();
        public Mock<IPurchaseReturnRepository> ReturnRepo { get; } = new();
        public Mock<IDatabaseExceptionTranslator> DbEx { get; } = new();

        public Mocks(PurchaseCreditNote creditNote, PurchaseReturn? purchaseReturn = null)
        {
            CreditNoteRepo
                .Setup(r => r.GetByIdAsync(TenantId, creditNote.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(creditNote);
            CreditNoteRepo
                .Setup(r =>
                    r.ExistsByLinkedPurchaseReturnIdAsync(
                        TenantId,
                        It.IsAny<Guid>(),
                        It.IsAny<Guid?>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(false);
            if (purchaseReturn is not null)
                ReturnRepo
                    .Setup(r => r.GetByIdAsync(TenantId, purchaseReturn.Id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(purchaseReturn);
        }

        public LinkPurchaseCreditNoteToReturnHandler BuildHandler() =>
            new(CreditNoteRepo.Object, ReturnRepo.Object, DbEx.Object, FixedTenant(), FixedUser());
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

    [Fact]
    public async Task LinkPurchaseReturn_valido_vincula_y_no_toca_CxP()
    {
        var creditNote = BuildCreditNote();
        var purchaseReturn = BuildReturn();
        var m = new Mocks(creditNote, purchaseReturn);
        var handler = m.BuildHandler();

        var result = await handler.Handle(
            new LinkPurchaseCreditNoteToReturnCommand(creditNote.Id, purchaseReturn.Id, Guid.NewGuid()),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.LinkedPurchaseReturnId.Should().Be(purchaseReturn.Id);
        creditNote.AppliedToPayableAmount.Should().BeNull();
        creditNote.Status.Should().Be(PurchaseCreditNoteStatus.Draft);
        m.CreditNoteRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LinkPurchaseReturn_falla_si_ApplicationType_no_es_Return()
    {
        var creditNote = BuildCreditNote(PurchaseCreditNoteApplicationType.Discount);
        var purchaseReturn = BuildReturn();
        var m = new Mocks(creditNote, purchaseReturn);
        var handler = m.BuildHandler();

        var result = await handler.Handle(
            new LinkPurchaseCreditNoteToReturnCommand(creditNote.Id, purchaseReturn.Id, Guid.NewGuid()),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Devolución");
    }

    [Fact]
    public async Task LinkPurchaseReturn_falla_si_ya_esta_vinculada_a_otra_devolucion()
    {
        var creditNote = BuildCreditNote();
        var firstReturn = BuildReturn();
        creditNote.LinkPurchaseReturn(firstReturn.Id, UserId);
        var otherReturn = BuildReturn();
        var m = new Mocks(creditNote, otherReturn);
        var handler = m.BuildHandler();

        var result = await handler.Handle(
            new LinkPurchaseCreditNoteToReturnCommand(creditNote.Id, otherReturn.Id, Guid.NewGuid()),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("ya está vinculada");
    }

    [Fact]
    public async Task LinkPurchaseReturn_repetido_con_la_misma_devolucion_es_idempotente()
    {
        var creditNote = BuildCreditNote();
        var purchaseReturn = BuildReturn();
        var m = new Mocks(creditNote, purchaseReturn);
        var handler = m.BuildHandler();
        var clientRequestId = Guid.NewGuid();

        var first = await handler.Handle(
            new LinkPurchaseCreditNoteToReturnCommand(creditNote.Id, purchaseReturn.Id, clientRequestId),
            CancellationToken.None
        );
        var retry = await handler.Handle(
            new LinkPurchaseCreditNoteToReturnCommand(creditNote.Id, purchaseReturn.Id, Guid.NewGuid()),
            CancellationToken.None
        );

        first.IsSuccess.Should().BeTrue();
        retry.IsSuccess.Should().BeTrue();
        retry.Value!.LinkedPurchaseReturnId.Should().Be(purchaseReturn.Id);
        m.CreditNoteRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("company")]
    [InlineData("branch")]
    [InlineData("invoice")]
    [InlineData("supplier")]
    public async Task LinkPurchaseReturn_falla_si_datos_no_coinciden(string mismatchedField)
    {
        var creditNote = BuildCreditNote();
        var purchaseReturn = mismatchedField switch
        {
            "company" => BuildReturn(companyId: Guid.NewGuid()),
            "branch" => BuildReturn(branchId: Guid.NewGuid()),
            "invoice" => BuildReturn(purchaseInvoiceId: Guid.NewGuid()),
            "supplier" => BuildReturn(supplierId: Guid.NewGuid()),
            _ => throw new ArgumentOutOfRangeException(nameof(mismatchedField)),
        };
        var m = new Mocks(creditNote, purchaseReturn);
        var handler = m.BuildHandler();

        var result = await handler.Handle(
            new LinkPurchaseCreditNoteToReturnCommand(creditNote.Id, purchaseReturn.Id, Guid.NewGuid()),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task LinkPurchaseReturn_falla_si_devolucion_no_existe()
    {
        var creditNote = BuildCreditNote();
        var m = new Mocks(creditNote);
        var handler = m.BuildHandler();

        var result = await handler.Handle(
            new LinkPurchaseCreditNoteToReturnCommand(creditNote.Id, Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
    }
}
