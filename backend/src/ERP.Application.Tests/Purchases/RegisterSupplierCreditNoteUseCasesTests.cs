using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Purchases;

/// <summary>
/// P0-02 Fase 9 — RegisterAndLinkSupplierCreditNoteHandler: vínculo feliz (Difference=0 y
/// Difference=0.01 exacto), rechazo SC-017/SC-018 (fuera de tolerancia en ambas direcciones),
/// SC-013 (moneda distinta), SC-009 (devolución con NC previa), sin efectos financieros.
/// </summary>
public sealed class RegisterSupplierCreditNoteUseCasesTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private const string SupplierRuc = "1791352688001";

    private sealed record Fixture(PurchaseInvoice Invoice, PurchaseReturn Return);

    private static Fixture BuildAuthorizedReturn(decimal grandTotal = 100m)
    {
        var inv = PurchaseInvoice.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            SupplierId,
            "Proveedor Test",
            SupplierRuc,
            "01",
            "001-001-000000001",
            new DateOnly(2026, 6, 1),
            UserId,
            Guid.NewGuid(),
            "Contado",
            1,
            30
        );
        var line = PurchaseInvoiceDetail.Create(
            inv.Id,
            TenantId,
            "Producto 1",
            quantity: 1m,
            unitPrice: grandTotal,
            vatCode: "10",
            uomCode: "UNIT"
        );
        inv.ReplaceLines(new[] { line }, UserId);

        var ret = PurchaseReturn.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            inv.Id,
            SupplierId,
            "Producto defectuoso",
            new[]
            {
                new PurchaseReturn.DraftLineInput(
                    inv.Lines[0].Id,
                    Guid.NewGuid(),
                    1m,
                    Guid.NewGuid()
                ),
            },
            UserId,
            Guid.NewGuid(),
            "hash-draft"
        );
        var original = inv.Lines[0];
        var snapshot = new Dictionary<Guid, PurchaseReturn.OriginalLineSnapshot>
        {
            [original.Id] = new PurchaseReturn.OriginalLineSnapshot(
                original.Quantity,
                original.LineSubtotal,
                original.DiscountAmount,
                original.VatAmount,
                original.IceAmount,
                original.VatCode,
                original.VatRate,
                original.IceCode,
                original.IceRate,
                original.LandedUnitCost,
                []
            ),
        };
        ret.Authorize(
            "00000001",
            snapshot,
            balanceDueBeforeApplication: grandTotal,
            inv.CurrencyCode,
            hasIssuedWithholding: false,
            UserId,
            Guid.NewGuid(),
            "hash-authorize"
        );

        return new Fixture(inv, ret);
    }

    private sealed class Mocks
    {
        public Mock<IPurchaseReturnRepository> ReturnRepo { get; } = new();
        public Mock<IPurchaseInvoiceRepository> InvoiceRepo { get; } = new();
        public Mock<IPurchaseReceptionDocumentRepository> ReceptionRepo { get; } = new();
        public Mock<IUnitOfWork> Uow { get; } = new();
        public Mock<IDatabaseExceptionTranslator> DbEx { get; } = new();

        public Mocks(Fixture f)
        {
            ReturnRepo
                .Setup(r => r.GetByIdAsync(TenantId, f.Return.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(f.Return);
            InvoiceRepo
                .Setup(r => r.GetByIdAsync(TenantId, f.Invoice.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(f.Invoice);
            ReceptionRepo
                .Setup(r =>
                    r.GetByAccessKeyAsync(
                        TenantId,
                        It.IsAny<string>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync((PurchaseReceptionDocument?)null);
            Uow.SetupGet(u => u.HasActiveTransaction).Returns(true);
        }

        public RegisterAndLinkSupplierCreditNoteHandler BuildHandler() =>
            new(
                ReturnRepo.Object,
                InvoiceRepo.Object,
                ReceptionRepo.Object,
                Uow.Object,
                DbEx.Object,
                new FixedCurrentTenant(),
                new FixedCurrentUser()
            );
    }

    private static RegisterAndLinkSupplierCreditNoteCommand BuildCommand(
        Guid purchaseReturnId,
        decimal totalAmount,
        string currencyCode = "USD",
        DateOnly? issueDate = null
    ) =>
        new(
            purchaseReturnId,
            $"AK-{Guid.NewGuid():N}",
            SupplierRuc,
            "Proveedor Test",
            "001-001-000000099",
            issueDate ?? new DateOnly(2026, 6, 15),
            Subtotal: totalAmount,
            VatAmount: 0m,
            TotalAmount: totalAmount,
            CurrencyCode: currencyCode,
            ClientRequestId: Guid.NewGuid()
        );

    [Fact]
    public async Task Vinculo_feliz_con_Difference_cero_transiciona_FiscalStatus()
    {
        var f = BuildAuthorizedReturn(100m);
        var m = new Mocks(f);
        var handler = m.BuildHandler();

        var result = await handler.Handle(BuildCommand(f.Return.Id, 100m), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        f.Return.FiscalStatus.Should()
            .Be(
                Domain
                    .Modules
                    .Purchases
                    .Enums
                    .PurchaseReturnFiscalStatus
                    .SupplierCreditNoteRegistered
            );
        f.Return.SupplierCreditNoteDocumentId.Should().NotBeNull();
    }

    [Fact]
    public async Task Vinculo_feliz_dentro_de_tolerancia_exacta_0_01_acepta()
    {
        var f = BuildAuthorizedReturn(100m);
        var m = new Mocks(f);
        var handler = m.BuildHandler();

        var result = await handler.Handle(
            BuildCommand(f.Return.Id, 100.01m),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
    }

    [Fact]
    public async Task Diferencia_0_02_por_debajo_rechaza_SC_017()
    {
        var f = BuildAuthorizedReturn(100m);
        var m = new Mocks(f);
        var handler = m.BuildHandler();

        var result = await handler.Handle(
            BuildCommand(f.Return.Id, 99.98m),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("inferior");
        f.Return.FiscalStatus.Should()
            .Be(
                Domain.Modules.Purchases.Enums.PurchaseReturnFiscalStatus.PendingSupplierCreditNote
            );
    }

    [Fact]
    public async Task Diferencia_0_02_por_encima_rechaza_SC_018()
    {
        var f = BuildAuthorizedReturn(100m);
        var m = new Mocks(f);
        var handler = m.BuildHandler();

        var result = await handler.Handle(
            BuildCommand(f.Return.Id, 100.02m),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("superior");
    }

    [Fact]
    public async Task Moneda_distinta_rechaza_SC_013()
    {
        var f = BuildAuthorizedReturn(100m);
        var m = new Mocks(f);
        var handler = m.BuildHandler();

        var result = await handler.Handle(
            BuildCommand(f.Return.Id, 100m, currencyCode: "EUR"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("moneda");
    }

    [Fact]
    public async Task Proveedor_distinto_rechaza_SC_008()
    {
        var f = BuildAuthorizedReturn(100m);
        var m = new Mocks(f);
        var handler = m.BuildHandler();
        var cmd = BuildCommand(f.Return.Id, 100m) with { SupplierRuc = "9999999999001" };

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("proveedor");
    }

    [Fact]
    public async Task Devolucion_con_NC_previa_rechaza_SC_009_sin_segundo_vinculo()
    {
        var f = BuildAuthorizedReturn(100m);
        var m = new Mocks(f);
        var handler = m.BuildHandler();

        var first = await handler.Handle(BuildCommand(f.Return.Id, 100m), CancellationToken.None);
        first.IsSuccess.Should().BeTrue(first.Error);
        var firstDocId = f.Return.SupplierCreditNoteDocumentId;

        var second = await handler.Handle(BuildCommand(f.Return.Id, 100m), CancellationToken.None);

        second.IsSuccess.Should().BeFalse();
        f.Return.SupplierCreditNoteDocumentId.Should()
            .Be(firstDocId, "el segundo intento no debe reemplazar el vínculo ya establecido");
    }

    [Fact]
    public async Task Sin_efectos_financieros_no_publica_evento_de_dominio_con_efecto_contable()
    {
        var f = BuildAuthorizedReturn(100m);
        var m = new Mocks(f);
        var handler = m.BuildHandler();

        await handler.Handle(BuildCommand(f.Return.Id, 100m), CancellationToken.None);

        // Único evento levantado por LinkSupplierCreditNote: PurchaseReturnSupplierCreditNoteLinkedEvent
        // (documental, IAuditEvent) — nunca un evento con efecto financiero/contable (§18.5/§19.5).
        var events =
            f.Return.GetType()
                .GetProperty(
                    "DomainEvents",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance
                )
                ?.GetValue(f.Return) as System.Collections.IEnumerable;
        events.Should().NotBeNull();
        foreach (var evt in events!)
            evt.GetType()
                .Name.Should()
                .NotContain(
                    "Posting",
                    "ningún evento con efecto contable debe originarse en el vínculo de NC"
                );
    }

    private sealed class FixedCurrentTenant : ICurrentTenant
    {
        public Guid TenantId => RegisterSupplierCreditNoteUseCasesTests.TenantId;
        public string? Slug => null;
    }

    private sealed class FixedCurrentUser : ICurrentUser
    {
        public Guid UserId => RegisterSupplierCreditNoteUseCasesTests.UserId;
        public bool IsAuthenticated => true;
        public string? Username => "tester";
        public string? Email => null;
        public string? FullName => null;
        public string? Role => null;
    }
}
