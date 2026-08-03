using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.Events;
using FluentAssertions;

namespace ERP.Domain.Tests.Purchases;

public sealed class PurchaseReturnTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid PurchaseInvoiceId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static PurchaseReturn.DraftLineInput Line(
        Guid? originalInvoiceDetailId = null,
        decimal quantity = 3m
    ) =>
        new(
            originalInvoiceDetailId ?? Guid.NewGuid(),
            Guid.NewGuid(),
            quantity,
            Guid.NewGuid()
        );

    private static PurchaseReturn CreateDraft(
        string reason = "Producto defectuoso",
        Guid? branchId = null,
        IEnumerable<PurchaseReturn.DraftLineInput>? lines = null,
        Guid? createClientRequestId = null,
        string createRequestPayloadHash = "hash-create-default"
    ) =>
        PurchaseReturn.CreateDraft(
            TenantId,
            CompanyId,
            branchId ?? BranchId,
            PurchaseInvoiceId,
            SupplierId,
            reason,
            lines ?? new[] { Line() },
            UserId,
            createClientRequestId ?? Guid.NewGuid(),
            createRequestPayloadHash
        );

    // ── CreateDraft ───────────────────────────────────────────────────

    [Fact]
    public void CreateDraft_con_datos_validos_queda_en_borrador()
    {
        var createClientRequestId = Guid.NewGuid();
        var purchaseReturn = CreateDraft(
            createClientRequestId: createClientRequestId,
            createRequestPayloadHash: "hash-create-001"
        );

        purchaseReturn.Status.Should().Be(PurchaseReturnStatus.Draft);
        purchaseReturn.FiscalStatus.Should().Be(PurchaseReturnFiscalStatus.NotApplicable);
        purchaseReturn.BranchId.Should().Be(BranchId);
        purchaseReturn.PurchaseInvoiceId.Should().Be(PurchaseInvoiceId);
        purchaseReturn.SupplierId.Should().Be(SupplierId);
        purchaseReturn.ReturnNumber.Should().BeNull();
        purchaseReturn.Lines.Should().ContainSingle();
        purchaseReturn.CreateClientRequestId.Should().Be(createClientRequestId);
        purchaseReturn.CreateRequestPayloadHash.Should().Be("hash-create-001");
        purchaseReturn.AuthorizeClientRequestId.Should().BeNull();
        purchaseReturn.AuthorizeRequestPayloadHash.Should().BeNull();
        purchaseReturn.CancelClientRequestId.Should().BeNull();
        purchaseReturn.CancelRequestPayloadHash.Should().BeNull();
        purchaseReturn.LinkCreditNoteClientRequestId.Should().BeNull();
        purchaseReturn.LinkCreditNoteRequestPayloadHash.Should().BeNull();
    }

    [Fact]
    public void CreateDraft_rechaza_CreateClientRequestId_vacio()
    {
        var act = () => CreateDraft(createClientRequestId: Guid.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateDraft_rechaza_CreateRequestPayloadHash_null()
    {
        var act = () => CreateDraft(createRequestPayloadHash: null!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateDraft_rechaza_CreateRequestPayloadHash_vacio()
    {
        var act = () => CreateDraft(createRequestPayloadHash: "");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateDraft_rechaza_CreateRequestPayloadHash_solo_espacios()
    {
        var act = () => CreateDraft(createRequestPayloadHash: "   ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateDraft_rechaza_BranchId_vacio()
    {
        var act = () => CreateDraft(branchId: Guid.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateDraft_rechaza_motivo_vacio()
    {
        var act = () => CreateDraft(reason: " ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateDraft_rechaza_factura_origen_vacia()
    {
        var act = () =>
            PurchaseReturn.CreateDraft(
                TenantId,
                CompanyId,
                BranchId,
                Guid.Empty,
                SupplierId,
                "Motivo",
                new[] { Line() },
                UserId,
                Guid.NewGuid(),
                "hash-create-default"
            );

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateDraft_rechaza_proveedor_vacio()
    {
        var act = () =>
            PurchaseReturn.CreateDraft(
                TenantId,
                CompanyId,
                BranchId,
                PurchaseInvoiceId,
                Guid.Empty,
                "Motivo",
                new[] { Line() },
                UserId,
                Guid.NewGuid(),
                "hash-create-default"
            );

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateDraft_rechaza_sin_lineas()
    {
        var act = () => CreateDraft(lines: Array.Empty<PurchaseReturn.DraftLineInput>());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateDraft_rechaza_linea_de_factura_duplicada()
    {
        var detailId = Guid.NewGuid();
        var act = () =>
            CreateDraft(lines: new[] { Line(detailId), Line(detailId) });

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateDraft_no_expone_setter_publico_de_BranchId()
    {
        // Verificación estructural: BranchId solo tiene "init"/private setter — este test documenta
        // que el compilador ya lo garantiza (no hay ChangeBranch/SetBranch en la API pública).
        var purchaseReturn = CreateDraft();
        purchaseReturn.BranchId.Should().Be(BranchId);
        var methodNames = typeof(PurchaseReturn).GetMethods().Select(m => m.Name).ToList();
        methodNames.Should().NotContain("ChangeBranch");
        methodNames.Should().NotContain("SetBranch");
    }

    // ── UpdateDraft ───────────────────────────────────────────────────

    [Fact]
    public void UpdateDraft_en_borrador_reemplaza_lineas_y_motivo()
    {
        var purchaseReturn = CreateDraft();
        var newLine = Line();

        purchaseReturn.UpdateDraft("Nuevo motivo", new[] { newLine }, UserId);

        purchaseReturn.Reason.Should().Be("Nuevo motivo");
        purchaseReturn.Lines.Should().ContainSingle(
            l => l.OriginalInvoiceDetailId == newLine.OriginalInvoiceDetailId
        );
    }

    [Fact]
    public void UpdateDraft_luego_de_autorizar_lanza()
    {
        var purchaseReturn = CreateDraft();
        AuthorizeHappyPath(purchaseReturn);

        var act = () => purchaseReturn.UpdateDraft("Motivo", new[] { Line() }, UserId);

        act.Should().Throw<InvalidOperationException>();
    }

    // ── Authorize ─────────────────────────────────────────────────────

    [Fact]
    public void Authorize_feliz_congela_lineas_y_calcula_totales()
    {
        var originalDetailId = Guid.NewGuid();
        var purchaseReturn = CreateDraft(lines: new[] { Line(originalDetailId, quantity: 3m) });
        var original = new Dictionary<Guid, PurchaseReturn.OriginalLineSnapshot>
        {
            [originalDetailId] = new(
                OriginalQuantity: 10m,
                LineSubtotal: 1000m,
                DiscountAmount: 0m,
                VatAmount: 120m,
                IceAmount: 0m,
                VatCode: "2",
                VatRate: 15m,
                IceCode: null,
                IceRate: 0m,
                LandedUnitCost: 115m
            ),
        };

        var authorizeClientRequestId = Guid.NewGuid();
        var credit = purchaseReturn.Authorize(
            "00000001",
            original,
            balanceDueBeforeApplication: 1120m,
            currencyCode: "USD",
            hasIssuedWithholding: false,
            UserId,
            authorizeClientRequestId,
            "hash-authorize-001"
        );

        purchaseReturn.Status.Should().Be(PurchaseReturnStatus.Authorized);
        purchaseReturn.FiscalStatus.Should().Be(PurchaseReturnFiscalStatus.PendingSupplierCreditNote);
        purchaseReturn.ReturnNumber.Should().Be("00000001");
        purchaseReturn.AuthorizedSubtotal.Should().Be(300.00m);
        purchaseReturn.AuthorizedVatTotal.Should().Be(36.00m);
        purchaseReturn.AuthorizedGrandTotal.Should().Be(336.00m);
        purchaseReturn.HistoricalCostTotal.Should().Be(345.00m);
        purchaseReturn.CostVarianceTotal.Should().Be(45.00m);
        purchaseReturn.AppliedToPayableAmount.Should().Be(336.00m);
        purchaseReturn.SupplierCreditAmount.Should().Be(0m);
        credit.Should().BeNull();
        purchaseReturn.Lines.Single().IsFrozen.Should().BeTrue();
        purchaseReturn.AuthorizeClientRequestId.Should().Be(authorizeClientRequestId);
        purchaseReturn.AuthorizeRequestPayloadHash.Should().Be("hash-authorize-001");
    }

    [Fact]
    public void Authorize_con_factura_totalmente_pagada_crea_SupplierCredit_con_mismo_BranchId()
    {
        var originalDetailId = Guid.NewGuid();
        var purchaseReturn = CreateDraft(lines: new[] { Line(originalDetailId, quantity: 3m) });
        var original = OriginalLines(originalDetailId);

        var credit = purchaseReturn.Authorize(
            "00000001",
            original,
            balanceDueBeforeApplication: 0m,
            currencyCode: "USD",
            hasIssuedWithholding: false,
            UserId,
            Guid.NewGuid(),
            "hash-authorize-002"
        );

        credit.Should().NotBeNull();
        credit!.BranchId.Should().Be(purchaseReturn.BranchId);
        credit.OriginalAmount.Should().Be(purchaseReturn.SupplierCreditAmount!.Value);
        purchaseReturn.AppliedToPayableAmount.Should().Be(0m);
    }

    [Fact]
    public void Authorize_con_retencion_emitida_lanza_y_no_autoriza()
    {
        var originalDetailId = Guid.NewGuid();
        var purchaseReturn = CreateDraft(lines: new[] { Line(originalDetailId, quantity: 3m) });
        var original = OriginalLines(originalDetailId);

        var act = () =>
            purchaseReturn.Authorize(
                "00000001",
                original,
                balanceDueBeforeApplication: 1120m,
                currencyCode: "USD",
                hasIssuedWithholding: true,
                UserId,
                Guid.NewGuid(),
                "hash-authorize-003"
            );

        act.Should().Throw<InvalidOperationException>().WithMessage("*retención*");
        purchaseReturn.Status.Should().Be(PurchaseReturnStatus.Draft);
    }

    [Fact]
    public void Authorize_ya_autorizada_no_se_puede_volver_a_autorizar()
    {
        var originalDetailId = Guid.NewGuid();
        var purchaseReturn = CreateDraft(lines: new[] { Line(originalDetailId, quantity: 3m) });
        purchaseReturn.Authorize(
            "00000001",
            OriginalLines(originalDetailId),
            1120m,
            "USD",
            false,
            UserId,
            Guid.NewGuid(),
            "hash-authorize-004"
        );

        var act = () =>
            purchaseReturn.Authorize(
                "00000002",
                OriginalLines(originalDetailId),
                1120m,
                "USD",
                false,
                UserId,
                Guid.NewGuid(),
                "hash-authorize-005"
            );

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Authorize_rechaza_AuthorizeClientRequestId_vacio()
    {
        var originalDetailId = Guid.NewGuid();
        var purchaseReturn = CreateDraft(lines: new[] { Line(originalDetailId, quantity: 3m) });

        var act = () =>
            purchaseReturn.Authorize(
                "00000001",
                OriginalLines(originalDetailId),
                1120m,
                "USD",
                false,
                UserId,
                Guid.Empty,
                "hash-authorize-006"
            );

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Authorize_rechaza_AuthorizeRequestPayloadHash_invalido(string? hash)
    {
        var originalDetailId = Guid.NewGuid();
        var purchaseReturn = CreateDraft(lines: new[] { Line(originalDetailId, quantity: 3m) });

        var act = () =>
            purchaseReturn.Authorize(
                "00000001",
                OriginalLines(originalDetailId),
                1120m,
                "USD",
                false,
                UserId,
                Guid.NewGuid(),
                hash!
            );

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Authorize_antes_de_autorizar_AuthorizeClientRequestId_es_null()
    {
        var originalDetailId = Guid.NewGuid();
        var purchaseReturn = CreateDraft(lines: new[] { Line(originalDetailId, quantity: 3m) });

        purchaseReturn.AuthorizeClientRequestId.Should().BeNull();
        purchaseReturn.AuthorizeRequestPayloadHash.Should().BeNull();
    }

    [Fact]
    public void Authorize_genera_PurchaseReturnAuthorizedEvent_con_los_datos_correctos()
    {
        var originalDetailId = Guid.NewGuid();
        var purchaseReturn = CreateDraft(lines: new[] { Line(originalDetailId, quantity: 3m) });

        purchaseReturn.Authorize(
            "00000001",
            OriginalLines(originalDetailId),
            1120m,
            "USD",
            false,
            UserId,
            Guid.NewGuid(),
            "hash-authorize-007"
        );

        var evt = purchaseReturn
            .DomainEvents.Should()
            .ContainSingle(e => e is PurchaseReturnAuthorizedEvent)
            .Which.Should()
            .BeOfType<PurchaseReturnAuthorizedEvent>()
            .Which;

        evt.PurchaseReturnId.Should().Be(purchaseReturn.Id);
        evt.PurchaseInvoiceId.Should().Be(PurchaseInvoiceId);
        evt.BranchId.Should().Be(BranchId);
        evt.GrandTotal.Should().Be(336.00m);
        evt.CostVarianceTotal.Should().Be(45.00m);
    }

    // ── Cancel ────────────────────────────────────────────────────────

    [Fact]
    public void Cancel_desde_borrador_cambia_a_cancelada_sin_montos()
    {
        var purchaseReturn = CreateDraft();
        var cancelClientRequestId = Guid.NewGuid();

        purchaseReturn.Cancel("Ya no aplica", UserId, cancelClientRequestId, "hash-cancel-001");

        purchaseReturn.Status.Should().Be(PurchaseReturnStatus.Cancelled);
        var evt = purchaseReturn
            .DomainEvents.Should()
            .ContainSingle(e => e is PurchaseReturnCancelledEvent)
            .Which.Should()
            .BeOfType<PurchaseReturnCancelledEvent>()
            .Which;
        evt.AppliedToPayableAmount.Should().BeNull();
        purchaseReturn.CancelClientRequestId.Should().Be(cancelClientRequestId);
        purchaseReturn.CancelRequestPayloadHash.Should().Be("hash-cancel-001");
    }

    [Fact]
    public void Cancel_desde_autorizada_cambia_a_cancelada_con_montos_snapshot()
    {
        var purchaseReturn = CreateDraft();
        AuthorizeHappyPath(purchaseReturn);

        purchaseReturn.Cancel("Devuelto por error", UserId, Guid.NewGuid(), "hash-cancel-002");

        purchaseReturn.Status.Should().Be(PurchaseReturnStatus.Cancelled);
        var evt = purchaseReturn
            .DomainEvents.Should()
            .ContainSingle(e => e is PurchaseReturnCancelledEvent)
            .Which.Should()
            .BeOfType<PurchaseReturnCancelledEvent>()
            .Which;
        evt.AppliedToPayableAmount.Should().Be(purchaseReturn.AppliedToPayableAmount);
    }

    [Fact]
    public void Cancel_desde_cancelada_lanza()
    {
        var purchaseReturn = CreateDraft();
        purchaseReturn.Cancel("Motivo", UserId, Guid.NewGuid(), "hash-cancel-003");

        var act = () => purchaseReturn.Cancel("Otro motivo", UserId, Guid.NewGuid(), "hash-cancel-004");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Cancel_rechaza_motivo_vacio()
    {
        var purchaseReturn = CreateDraft();

        var act = () => purchaseReturn.Cancel(" ", UserId, Guid.NewGuid(), "hash-cancel-005");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Cancel_rechaza_CancelClientRequestId_vacio()
    {
        var purchaseReturn = CreateDraft();

        var act = () => purchaseReturn.Cancel("Motivo", UserId, Guid.Empty, "hash-cancel-006");

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Cancel_rechaza_CancelRequestPayloadHash_invalido(string? hash)
    {
        var purchaseReturn = CreateDraft();

        var act = () => purchaseReturn.Cancel("Motivo", UserId, Guid.NewGuid(), hash!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Cancel_antes_de_cancelar_CancelClientRequestId_es_null()
    {
        var purchaseReturn = CreateDraft();

        purchaseReturn.CancelClientRequestId.Should().BeNull();
        purchaseReturn.CancelRequestPayloadHash.Should().BeNull();
    }

    // ── LinkSupplierCreditNote ────────────────────────────────────────

    [Fact]
    public void LinkSupplierCreditNote_desde_autorizada_actualiza_FiscalStatus()
    {
        var purchaseReturn = CreateDraft();
        AuthorizeHappyPath(purchaseReturn);
        var docId = Guid.NewGuid();
        var linkClientRequestId = Guid.NewGuid();

        purchaseReturn.LinkSupplierCreditNote(
            docId,
            UserId,
            linkClientRequestId,
            "hash-link-001"
        );

        purchaseReturn.SupplierCreditNoteDocumentId.Should().Be(docId);
        purchaseReturn.FiscalStatus.Should().Be(PurchaseReturnFiscalStatus.SupplierCreditNoteRegistered);
        purchaseReturn.LinkCreditNoteClientRequestId.Should().Be(linkClientRequestId);
        purchaseReturn.LinkCreditNoteRequestPayloadHash.Should().Be("hash-link-001");
    }

    [Fact]
    public void LinkSupplierCreditNote_desde_borrador_lanza()
    {
        var purchaseReturn = CreateDraft();

        var act = () =>
            purchaseReturn.LinkSupplierCreditNote(
                Guid.NewGuid(),
                UserId,
                Guid.NewGuid(),
                "hash-link-002"
            );

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void LinkSupplierCreditNote_rechaza_LinkCreditNoteClientRequestId_vacio()
    {
        var purchaseReturn = CreateDraft();
        AuthorizeHappyPath(purchaseReturn);

        var act = () =>
            purchaseReturn.LinkSupplierCreditNote(
                Guid.NewGuid(),
                UserId,
                Guid.Empty,
                "hash-link-003"
            );

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void LinkSupplierCreditNote_rechaza_LinkCreditNoteRequestPayloadHash_invalido(string? hash)
    {
        var purchaseReturn = CreateDraft();
        AuthorizeHappyPath(purchaseReturn);

        var act = () =>
            purchaseReturn.LinkSupplierCreditNote(Guid.NewGuid(), UserId, Guid.NewGuid(), hash!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void LinkSupplierCreditNote_antes_de_vincular_LinkCreditNoteClientRequestId_es_null()
    {
        var purchaseReturn = CreateDraft();
        AuthorizeHappyPath(purchaseReturn);

        purchaseReturn.LinkCreditNoteClientRequestId.Should().BeNull();
        purchaseReturn.LinkCreditNoteRequestPayloadHash.Should().BeNull();
    }

    // ── helpers ──────────────────────────────────────────────────────

    private static Dictionary<Guid, PurchaseReturn.OriginalLineSnapshot> OriginalLines(
        Guid originalDetailId
    ) =>
        new()
        {
            [originalDetailId] = new(
                OriginalQuantity: 10m,
                LineSubtotal: 1000m,
                DiscountAmount: 0m,
                VatAmount: 120m,
                IceAmount: 0m,
                VatCode: "2",
                VatRate: 15m,
                IceCode: null,
                IceRate: 0m,
                LandedUnitCost: 115m
            ),
        };

    private static void AuthorizeHappyPath(PurchaseReturn purchaseReturn)
    {
        var originalDetailId = purchaseReturn.Lines.Single().OriginalInvoiceDetailId;
        purchaseReturn.Authorize(
            "00000001",
            OriginalLines(originalDetailId),
            1120m,
            "USD",
            false,
            UserId,
            Guid.NewGuid(),
            "hash-authorize-happy-path"
        );
    }
}
