using ERP.Domain.Audit;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.Events;
using FluentAssertions;

namespace ERP.Domain.Tests.Purchases;

/// <summary>
/// FLOW-READY-02C.1 — pruebas de dominio puro de <see cref="PurchaseCreditNote"/> (descuento/
/// promoción). Cubre los ajustes obligatorios de la aprobación del diseño: bloqueo por excedente
/// (§0.2 #1), snapshot financiero al autorizar (§0.2 #3) y ausencia de efecto contable (§0.2 #2).
/// </summary>
public sealed class PurchaseCreditNoteTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid PurchaseInvoiceId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static PurchaseCreditNote.DraftLineInput Line(
        string description = "Descuento por promoción",
        decimal subtotal = 100m,
        decimal vatAmount = 15m
    ) => new(description, subtotal, "2", 15m, vatAmount);

    private static PurchaseCreditNote.TaxSummaryDraftLineInput TaxSummaryLine(
        Guid? sourceId = null,
        string vatCode = "10",
        decimal vatRate = 15m,
        string? vatName = "IVA 15%",
        string? iceCode = null,
        decimal iceRate = 0m,
        string? iceName = null,
        decimal taxableBase = 100m,
        string? irbpnrCode = null,
        decimal irbpnrRate = 0m,
        string? irbpnrName = null,
        decimal? sourceTaxableBase = null,
        decimal sourceIrbpnrAmount = 0m
    ) =>
        new(
            sourceId ?? Guid.NewGuid(),
            vatCode,
            vatRate,
            vatName,
            iceCode,
            iceRate,
            iceName,
            taxableBase,
            irbpnrCode,
            irbpnrRate,
            irbpnrName,
            sourceTaxableBase ?? taxableBase,
            sourceIrbpnrAmount
        );

    private static PurchaseCreditNote CreateDraft(
        Guid? branchId = null,
        Guid? receptionDocumentId = null,
        string reason = "Descuento por volumen de compra",
        IEnumerable<PurchaseCreditNote.DraftLineInput>? lines = null,
        IEnumerable<PurchaseCreditNote.TaxSummaryDraftLineInput>? taxSummaryLines = null,
        Guid? createClientRequestId = null,
        string createRequestPayloadHash = "hash-create-default",
        PurchaseCreditNoteApplicationType applicationType = PurchaseCreditNoteApplicationType.Discount
    ) =>
        PurchaseCreditNote.CreateDraft(
            TenantId,
            CompanyId,
            branchId ?? BranchId,
            SupplierId,
            PurchaseInvoiceId,
            receptionDocumentId,
            applicationType,
            "001-001-000000001",
            accessKey: null,
            authorizationNumber: null,
            authorizationDate: null,
            issueDate: DateOnly.FromDateTime(DateTime.UtcNow),
            reason,
            lines ?? (taxSummaryLines is null ? new[] { Line() } : Array.Empty<PurchaseCreditNote.DraftLineInput>()),
            taxSummaryLines ?? Array.Empty<PurchaseCreditNote.TaxSummaryDraftLineInput>(),
            UserId,
            createClientRequestId ?? Guid.NewGuid(),
            createRequestPayloadHash
        );

    // ── CreateDraft ───────────────────────────────────────────────────

    [Fact]
    public void CreateDraft_con_datos_validos_queda_en_borrador()
    {
        var creditNote = CreateDraft();

        creditNote.Status.Should().Be(PurchaseCreditNoteStatus.Draft);
        creditNote.BranchId.Should().Be(BranchId);
        creditNote.CompanyId.Should().Be(CompanyId);
        creditNote.SupplierId.Should().Be(SupplierId);
        creditNote.PurchaseInvoiceId.Should().Be(PurchaseInvoiceId);
        creditNote.ReceptionDocumentId.Should().BeNull();
        creditNote.Lines.Should().ContainSingle();
        creditNote.Subtotal.Should().Be(100m);
        creditNote.VatAmount.Should().Be(15m);
        creditNote.TotalAmount.Should().Be(115m);
        creditNote.AppliedToPayableAmount.Should().BeNull();
        creditNote.AuthorizedAtUtc.Should().BeNull();
    }

    [Fact]
    public void CreateDraft_con_detalle_de_descuento_valido_calcula_totales_de_la_linea()
    {
        var creditNote = CreateDraft(lines: new[] { Line(subtotal: 200m, vatAmount: 30m) });

        var line = creditNote.Lines.Single();
        line.Subtotal.Should().Be(200m);
        line.VatAmount.Should().Be(30m);
        line.TotalAmount.Should().Be(230m);
    }

    [Fact]
    public void CreateDraft_rechaza_sin_lineas()
    {
        var act = () => CreateDraft(lines: Array.Empty<PurchaseCreditNote.DraftLineInput>());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateDraft_rechaza_linea_con_subtotal_no_positivo()
    {
        var act = () => CreateDraft(lines: new[] { Line(subtotal: 0m) });

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateDraft_rechaza_BranchId_vacio()
    {
        var act = () => CreateDraft(branchId: Guid.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateDraft_rechaza_factura_afectada_vacia()
    {
        var act = () =>
            PurchaseCreditNote.CreateDraft(
                TenantId,
                CompanyId,
                BranchId,
                SupplierId,
                Guid.Empty,
                null,
                PurchaseCreditNoteApplicationType.Discount,
                "001-001-000000001",
                null,
                null,
                null,
                DateOnly.FromDateTime(DateTime.UtcNow),
                "Motivo",
                new[] { Line() },
                Array.Empty<PurchaseCreditNote.TaxSummaryDraftLineInput>(),
                UserId,
                Guid.NewGuid(),
                "hash-create-default"
            );

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateDraft_rechaza_motivo_vacio()
    {
        var act = () => CreateDraft(reason: " ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateDraft_rechaza_CreateClientRequestId_vacio()
    {
        var act = () => CreateDraft(createClientRequestId: Guid.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateDraft_acepta_ReceptionDocumentId_opcional()
    {
        var receptionDocumentId = Guid.NewGuid();
        var creditNote = CreateDraft(receptionDocumentId: receptionDocumentId);

        creditNote.ReceptionDocumentId.Should().Be(receptionDocumentId);
    }

    // ── Authorize ─────────────────────────────────────────────────────

    [Fact]
    public void Authorize_cuando_TotalAmount_es_menor_o_igual_al_saldo_autoriza()
    {
        var creditNote = CreateDraft(lines: new[] { Line(subtotal: 100m, vatAmount: 15m) }); // Total = 115

        var authorizeClientRequestId = Guid.NewGuid();
        creditNote.Authorize(
            balanceDueBeforeApplication: 200m,
            UserId,
            authorizeClientRequestId,
            "hash-authorize-001"
        );

        creditNote.Status.Should().Be(PurchaseCreditNoteStatus.Authorized);
        creditNote.AuthorizedAtUtc.Should().NotBeNull();
        creditNote.AuthorizedByUserId.Should().Be(UserId);
        creditNote.AuthorizeClientRequestId.Should().Be(authorizeClientRequestId);
    }

    [Fact]
    public void Authorize_cuando_TotalAmount_es_igual_al_saldo_autoriza()
    {
        var creditNote = CreateDraft(lines: new[] { Line(subtotal: 100m, vatAmount: 15m) }); // Total = 115

        creditNote.Authorize(115m, UserId, Guid.NewGuid(), "hash-authorize-002");

        creditNote.Status.Should().Be(PurchaseCreditNoteStatus.Authorized);
        creditNote.AppliedToPayableAmount.Should().Be(115m);
    }

    [Fact]
    public void Authorize_bloquea_cuando_TotalAmount_excede_el_saldo_pendiente_y_no_trunca()
    {
        // Ajuste obligatorio #1 de la aprobación (§0.2, §4.2): bloquea, nunca trunca ni genera crédito.
        var creditNote = CreateDraft(lines: new[] { Line(subtotal: 100m, vatAmount: 15m) }); // Total = 115

        var act = () => creditNote.Authorize(100m, UserId, Guid.NewGuid(), "hash-authorize-003");

        act.Should().Throw<InvalidOperationException>().WithMessage("*excede*");
        creditNote.Status.Should().Be(PurchaseCreditNoteStatus.Draft);
        creditNote.AppliedToPayableAmount.Should().BeNull();
        creditNote.AuthorizedAtUtc.Should().BeNull();
    }

    [Fact]
    public void Authorize_congela_el_snapshot_financiero()
    {
        var creditNote = CreateDraft(lines: new[] { Line(subtotal: 300m, vatAmount: 45m) }); // Total = 345

        creditNote.Authorize(345m, UserId, Guid.NewGuid(), "hash-authorize-004");

        creditNote.Subtotal.Should().Be(300m);
        creditNote.VatAmount.Should().Be(45m);
        creditNote.TotalAmount.Should().Be(345m);
        creditNote.AppliedToPayableAmount.Should().Be(345m);
    }

    [Fact]
    public void Authorize_sin_lineas_no_es_posible_porque_CreateDraft_las_exige()
    {
        // No existe un camino de dominio para llegar a Authorize() sin líneas — CreateDraft/
        // UpdateDraft ya rechazan la colección vacía (ReplaceLines). Se documenta explícitamente
        // en vez de duplicar un escenario inalcanzable.
        var creditNote = CreateDraft();
        creditNote.Lines.Should().NotBeEmpty();
    }

    [Fact]
    public void Authorize_ya_autorizada_no_se_puede_volver_a_autorizar()
    {
        var creditNote = CreateDraft();
        creditNote.Authorize(1000m, UserId, Guid.NewGuid(), "hash-authorize-005");

        var act = () =>
            creditNote.Authorize(1000m, UserId, Guid.NewGuid(), "hash-authorize-006");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Authorize_rechaza_AuthorizeClientRequestId_vacio()
    {
        var creditNote = CreateDraft();

        var act = () => creditNote.Authorize(1000m, UserId, Guid.Empty, "hash-authorize-007");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Authorize_genera_PurchaseCreditNoteAuthorizedEvent_con_los_datos_correctos()
    {
        var creditNote = CreateDraft(lines: new[] { Line(subtotal: 100m, vatAmount: 15m) });

        creditNote.Authorize(115m, UserId, Guid.NewGuid(), "hash-authorize-008");

        var evt = creditNote
            .DomainEvents.Should()
            .ContainSingle(e => e is PurchaseCreditNoteAuthorizedEvent)
            .Which.Should()
            .BeOfType<PurchaseCreditNoteAuthorizedEvent>()
            .Which;

        evt.PurchaseCreditNoteId.Should().Be(creditNote.Id);
        evt.TotalAmount.Should().Be(115m);
        evt.AppliedToPayableAmount.Should().Be(115m);
        evt.IceAmount.Should().Be(0m);
    }

    [Fact]
    public void Authorize_con_ICE_propaga_IceAmount_al_PurchaseCreditNoteAuthorizedEvent()
    {
        // ACCOUNTING-PURCHASE-CREDIT-NOTE-ICE-08B: IceAmount ya se calculaba en la entidad
        // (RecalculateTotals, vía TaxSummaries) pero nunca llegaba al evento — este test confirma
        // que ahora sí, con el mismo monto ya congelado, sin recalcular.
        var creditNote = CreateDraft(
            taxSummaryLines: new[]
            {
                TaxSummaryLine(vatCode: "10", vatRate: 15m, iceCode: "3023", iceRate: 10m, taxableBase: 100m),
            }
        );
        var totalDue = creditNote.TotalAmount;

        creditNote.Authorize(totalDue, UserId, Guid.NewGuid(), "hash-authorize-ice-001");

        var evt = creditNote
            .DomainEvents.Should()
            .ContainSingle(e => e is PurchaseCreditNoteAuthorizedEvent)
            .Which.Should()
            .BeOfType<PurchaseCreditNoteAuthorizedEvent>()
            .Which;

        evt.IceAmount.Should().Be(creditNote.IceAmount);
        evt.IceAmount.Should().Be(10m); // 100 * 10% = 10, mismo cálculo que CreateDraft_con_TaxSummaryLines_calcula_IceAmount_con_SriTaxCalculator
        evt.TotalAmount.Should().Be(creditNote.Subtotal + creditNote.IceAmount + creditNote.VatAmount);
    }

    // ── TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.3, Subfase 5D-2) ────────────────

    [Fact]
    public void TaxSummaryLine_solo_IVA_revierte_unicamente_IVA()
    {
        var creditNote = CreateDraft(
            taxSummaryLines: new[] { TaxSummaryLine(vatRate: 15m, taxableBase: 100m) }
        );

        var summary = creditNote.TaxSummaries.Single();
        summary.VatAmount.Should().Be(15m);
        summary.IceAmount.Should().Be(0m);
        summary.IrbpnrAmount.Should().Be(0m);
        summary.IrbpnrCode.Should().BeNull();
    }

    [Fact]
    public void TaxSummaryLine_con_ICE_revierte_IVA_e_ICE_sin_generar_IRBPNR_falso()
    {
        var creditNote = CreateDraft(
            taxSummaryLines: new[]
            {
                TaxSummaryLine(vatRate: 15m, iceCode: "3023", iceRate: 10m, taxableBase: 100m),
            }
        );

        var summary = creditNote.TaxSummaries.Single();
        summary.IceAmount.Should().Be(10m);
        summary.VatAmount.Should().Be(16.5m); // (100+10)*15%
        summary.IrbpnrAmount.Should().Be(0m);
        summary.IrbpnrCode.Should().BeNull();
    }

    [Fact]
    public void TaxSummaryLine_con_IRBPNR_completo_prorratea_al_100_por_ciento()
    {
        // Fracción = TaxableBase de la NC / TaxableBase de la fuente = 100/100 = 1 → prorrateo íntegro.
        var creditNote = CreateDraft(
            taxSummaryLines: new[]
            {
                TaxSummaryLine(
                    vatRate: 15m,
                    taxableBase: 100m,
                    irbpnrCode: "5001",
                    irbpnrRate: 0.02m,
                    irbpnrName: "IRBPNR",
                    sourceTaxableBase: 100m,
                    sourceIrbpnrAmount: 1.00m
                ),
            }
        );

        var summary = creditNote.TaxSummaries.Single();
        summary.VatAmount.Should().Be(15m);
        summary.IrbpnrCode.Should().Be("5001");
        summary.IrbpnrAmount.Should().Be(1.00m);
        summary.TotalAmount.Should().Be(100m + 15m + 1.00m);
    }

    [Fact]
    public void TaxSummaryLine_con_IVA_ICE_e_IRBPNR_revierte_los_tres_impuestos()
    {
        var creditNote = CreateDraft(
            taxSummaryLines: new[]
            {
                TaxSummaryLine(
                    vatRate: 15m,
                    iceCode: "3023",
                    iceRate: 10m,
                    taxableBase: 100m,
                    irbpnrCode: "5001",
                    irbpnrRate: 0.02m,
                    sourceTaxableBase: 100m,
                    sourceIrbpnrAmount: 1.00m
                ),
            }
        );

        var summary = creditNote.TaxSummaries.Single();
        summary.IceAmount.Should().Be(10m);
        summary.VatAmount.Should().Be(16.5m);
        summary.IrbpnrAmount.Should().Be(1.00m);
        creditNote.IceAmount.Should().Be(10m);
        creditNote.VatAmount.Should().Be(16.5m);
        creditNote.IrbpnrAmount.Should().Be(1.00m);
        creditNote.TotalAmount.Should().Be(100m + 10m + 16.5m + 1.00m);
    }

    [Fact]
    public void TaxSummaryLine_con_credito_parcial_prorratea_IRBPNR_por_la_fraccion_de_TaxableBase()
    {
        // La factura original tenía TaxableBase=1000 con IRBPNR=1.00 total. Esta NC solo acredita
        // 300 de esos 1000 (fracción 0.3) — el IRBPNR revertido debe ser proporcional, igual que
        // ya ocurre implícitamente con IVA/ICE en este flujo (tarifa constante × base reducida).
        var creditNote = CreateDraft(
            taxSummaryLines: new[]
            {
                TaxSummaryLine(
                    vatRate: 15m,
                    taxableBase: 300m,
                    irbpnrCode: "5001",
                    irbpnrRate: 0.02m,
                    sourceTaxableBase: 1000m,
                    sourceIrbpnrAmount: 1.00m
                ),
            }
        );

        var summary = creditNote.TaxSummaries.Single();
        summary.IrbpnrAmount.Should().Be(0.30m); // 0.3 * 1.00
    }

    [Fact]
    public void TaxSummaryLine_sin_IRBPNR_en_la_fuente_no_genera_IRBPNR_falso()
    {
        var creditNote = CreateDraft(
            taxSummaryLines: new[]
            {
                TaxSummaryLine(vatRate: 15m, taxableBase: 100m, sourceTaxableBase: 100m, sourceIrbpnrAmount: 0m),
            }
        );

        var summary = creditNote.TaxSummaries.Single();
        summary.IrbpnrCode.Should().BeNull();
        summary.IrbpnrAmount.Should().Be(0m);
    }

    [Fact]
    public void TaxSummaryLine_con_IVA_ICE_e_IRBPNR_persiste_una_fila_por_impuesto_en_Taxes()
    {
        // Confirma la corrección post-revisión de 5D-2: la fuente de verdad es la colección Taxes
        // (PurchaseCreditNoteTaxSummaryLine), no columnas fijas Vat*/Ice*/Irbpnr*.
        var creditNote = CreateDraft(
            taxSummaryLines: new[]
            {
                TaxSummaryLine(
                    vatRate: 15m,
                    iceCode: "3023",
                    iceRate: 10m,
                    taxableBase: 100m,
                    irbpnrCode: "5001",
                    irbpnrRate: 0.02m,
                    sourceTaxableBase: 100m,
                    sourceIrbpnrAmount: 1.00m
                ),
            }
        );

        var summary = creditNote.TaxSummaries.Single();
        summary.Taxes.Should().HaveCount(3);
        summary.Taxes.Should().Contain(t => t.TaxCode == "2" && t.TaxAmount == 16.5m);
        summary.Taxes.Should().Contain(t => t.TaxCode == "3" && t.TaxAmount == 10m);
        summary.Taxes.Should().Contain(t => t.TaxCode == "5" && t.TaxAmount == 1.00m);
    }

    [Fact]
    public void TaxSummaryLine_sin_ICE_ni_IRBPNR_Taxes_solo_contiene_la_fila_de_IVA()
    {
        var creditNote = CreateDraft(
            taxSummaryLines: new[] { TaxSummaryLine(vatRate: 15m, taxableBase: 100m) }
        );

        var summary = creditNote.TaxSummaries.Single();
        summary.Taxes.Should().ContainSingle(t => t.TaxCode == "2");
    }

    [Fact]
    public void Authorize_no_agrega_IrbpnrAmount_al_PurchaseCreditNoteAuthorizedEvent()
    {
        // Deliberado: el evento alimenta el traductor contable (Subfase 5E, fuera de alcance aquí).
        var creditNote = CreateDraft(
            taxSummaryLines: new[]
            {
                TaxSummaryLine(
                    vatRate: 15m,
                    taxableBase: 100m,
                    irbpnrCode: "5001",
                    irbpnrRate: 0.02m,
                    sourceTaxableBase: 100m,
                    sourceIrbpnrAmount: 1.00m
                ),
            }
        );
        var totalDue = creditNote.TotalAmount;

        creditNote.Authorize(totalDue, UserId, Guid.NewGuid(), "hash-authorize-irbpnr-001");

        var evt = creditNote
            .DomainEvents.Should()
            .ContainSingle(e => e is PurchaseCreditNoteAuthorizedEvent)
            .Which.Should()
            .BeOfType<PurchaseCreditNoteAuthorizedEvent>()
            .Which;

        // El evento no tiene campo IrbpnrAmount — este test documenta la decisión, no un valor.
        typeof(PurchaseCreditNoteAuthorizedEvent)
            .GetProperty("IrbpnrAmount")
            .Should()
            .BeNull("IrbpnrAmount se agrega al evento en la Subfase 5E, no aquí");
        evt.TotalAmount.Should().Be(creditNote.TotalAmount); // 100 + 0 (ICE) + 15 + 1.00 (IRBPNR)
        creditNote.TotalAmount.Should().Be(116.00m);
    }

    [Fact]
    public void Authorize_no_genera_evento_de_auditoria_contable_IAuditEvent()
    {
        // Ajuste obligatorio #2 (§0.2, §4.3): el evento es un punto de extensión inerte — nunca
        // implementa IAuditEvent en esta fase, para que ningún traductor/handler genérico pueda
        // engancharse accidentalmente y producir un PostingFact.
        var creditNote = CreateDraft();
        creditNote.Authorize(1000m, UserId, Guid.NewGuid(), "hash-authorize-009");

        var evt = creditNote.DomainEvents.Single(e => e is PurchaseCreditNoteAuthorizedEvent);
        evt.Should().NotBeAssignableTo<IAuditEvent>();
    }

    // ── Cancel ────────────────────────────────────────────────────────

    [Fact]
    public void Cancel_desde_borrador_cambia_a_cancelada_sin_montos()
    {
        var creditNote = CreateDraft();

        creditNote.Cancel("Ya no aplica", UserId, Guid.NewGuid(), "hash-cancel-001");

        creditNote.Status.Should().Be(PurchaseCreditNoteStatus.Cancelled);
        var evt = creditNote
            .DomainEvents.Should()
            .ContainSingle(e => e is PurchaseCreditNoteCancelledEvent)
            .Which.Should()
            .BeOfType<PurchaseCreditNoteCancelledEvent>()
            .Which;
        evt.AppliedToPayableAmount.Should().BeNull();
    }

    [Fact]
    public void Cancel_desde_autorizada_cambia_a_cancelada_con_monto_snapshot()
    {
        var creditNote = CreateDraft();
        creditNote.Authorize(1000m, UserId, Guid.NewGuid(), "hash-authorize-010");

        creditNote.Cancel("Corrección", UserId, Guid.NewGuid(), "hash-cancel-002");

        creditNote.Status.Should().Be(PurchaseCreditNoteStatus.Cancelled);
        var evt = creditNote
            .DomainEvents.Should()
            .ContainSingle(e => e is PurchaseCreditNoteCancelledEvent)
            .Which.Should()
            .BeOfType<PurchaseCreditNoteCancelledEvent>()
            .Which;
        evt.AppliedToPayableAmount.Should().Be(creditNote.AppliedToPayableAmount);
    }

    [Fact]
    public void Cancel_desde_cancelada_lanza()
    {
        var creditNote = CreateDraft();
        creditNote.Cancel("Motivo", UserId, Guid.NewGuid(), "hash-cancel-003");

        var act = () =>
            creditNote.Cancel("Otro motivo", UserId, Guid.NewGuid(), "hash-cancel-004");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Cancel_rechaza_motivo_vacio()
    {
        var creditNote = CreateDraft();

        var act = () => creditNote.Cancel(" ", UserId, Guid.NewGuid(), "hash-cancel-005");

        act.Should().Throw<ArgumentException>();
    }

    // ── FLOW-READY-02C-R1.1: ApplicationType / LinkPurchaseReturn ────────

    [Fact]
    public void CreateDraft_Return_no_aplica_CxP_ni_congela_snapshot()
    {
        var creditNote = CreateDraft(applicationType: PurchaseCreditNoteApplicationType.Return);

        creditNote.ApplicationType.Should().Be(PurchaseCreditNoteApplicationType.Return);
        creditNote.Status.Should().Be(PurchaseCreditNoteStatus.Draft);
        creditNote.AppliedToPayableAmount.Should().BeNull();
        creditNote.LinkedPurchaseReturnId.Should().BeNull();
    }

    [Fact]
    public void Authorize_sobre_tipo_Return_siempre_falla()
    {
        var creditNote = CreateDraft(applicationType: PurchaseCreditNoteApplicationType.Return);

        var act = () => creditNote.Authorize(100_000m, UserId, Guid.NewGuid(), "hash-authorize-return");

        act.Should().Throw<InvalidOperationException>();
        creditNote.Status.Should().Be(PurchaseCreditNoteStatus.Draft);
        creditNote.AppliedToPayableAmount.Should().BeNull();
    }

    [Fact]
    public void LinkPurchaseReturn_sobre_tipo_Return_vincula_una_sola_vez()
    {
        var creditNote = CreateDraft(applicationType: PurchaseCreditNoteApplicationType.Return);
        var purchaseReturnId = Guid.NewGuid();

        creditNote.LinkPurchaseReturn(purchaseReturnId, UserId);

        creditNote.LinkedPurchaseReturnId.Should().Be(purchaseReturnId);

        var act = () => creditNote.LinkPurchaseReturn(Guid.NewGuid(), UserId);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void LinkPurchaseReturn_sobre_tipo_Discount_falla()
    {
        var creditNote = CreateDraft(applicationType: PurchaseCreditNoteApplicationType.Discount);

        var act = () => creditNote.LinkPurchaseReturn(Guid.NewGuid(), UserId);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void LinkPurchaseReturn_rechaza_Guid_vacio()
    {
        var creditNote = CreateDraft(applicationType: PurchaseCreditNoteApplicationType.Return);

        var act = () => creditNote.LinkPurchaseReturn(Guid.Empty, UserId);

        act.Should().Throw<ArgumentException>();
    }

    // ── FLOW-READY-02C-R1.2: TaxSummaryDraftLineInput / PurchaseCreditNoteTaxSummary ──

    [Fact]
    public void CreateDraft_con_TaxSummaryLines_calcula_TotalAmount_TaxableBase_mas_IceAmount_mas_VatAmount()
    {
        var creditNote = CreateDraft(
            taxSummaryLines: new[] { TaxSummaryLine(vatCode: "10", vatRate: 15m, taxableBase: 100m) }
        );

        var summary = creditNote.TaxSummaries.Single();
        summary.TotalAmount.Should().Be(summary.TaxableBase + summary.IceAmount + summary.VatAmount);
        creditNote.TotalAmount.Should().Be(creditNote.Subtotal + creditNote.IceAmount + creditNote.VatAmount);
    }

    [Fact]
    public void CreateDraft_con_TaxSummaryLines_hereda_TenantId_CompanyId_BranchId_de_la_NC()
    {
        var creditNote = CreateDraft(taxSummaryLines: new[] { TaxSummaryLine() });

        var summary = creditNote.TaxSummaries.Single();
        summary.TenantId.Should().Be(TenantId);
        summary.CompanyId.Should().Be(CompanyId);
        summary.BranchId.Should().Be(BranchId);
        summary.PurchaseCreditNoteId.Should().Be(creditNote.Id);
        summary.PurchaseInvoiceId.Should().Be(PurchaseInvoiceId);
    }

    [Fact]
    public void CreateDraft_con_TaxSummaryLines_referencia_SourcePurchaseInvoiceTaxSummaryId()
    {
        var sourceId = Guid.NewGuid();

        var creditNote = CreateDraft(taxSummaryLines: new[] { TaxSummaryLine(sourceId: sourceId) });

        creditNote.TaxSummaries.Single().SourcePurchaseInvoiceTaxSummaryId.Should().Be(sourceId);
    }

    [Fact]
    public void CreateDraft_con_TaxSummaryLines_rechaza_TaxableBase_no_positiva()
    {
        var act = () =>
            CreateDraft(taxSummaryLines: new[] { TaxSummaryLine(taxableBase: 0m) });

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateDraft_con_TaxSummaryLines_calcula_IceAmount_con_SriTaxCalculator()
    {
        var creditNote = CreateDraft(
            taxSummaryLines: new[]
            {
                TaxSummaryLine(
                    vatCode: "10",
                    vatRate: 15m,
                    iceCode: "3023",
                    iceRate: 10m,
                    taxableBase: 100m
                ),
            }
        );

        var summary = creditNote.TaxSummaries.Single();
        summary.IceAmount.Should().Be(10m); // 100 * 10% = 10
        summary.VatAmount.Should().Be(16.5m); // (100 + 10) * 15% = 16.5
    }

    [Fact]
    public void CreateDraft_sin_lineas_ni_resumenes_fiscales_lanza()
    {
        var act = () =>
            CreateDraft(
                lines: Array.Empty<PurchaseCreditNote.DraftLineInput>(),
                taxSummaryLines: Array.Empty<PurchaseCreditNote.TaxSummaryDraftLineInput>()
            );

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Authorize_con_solo_TaxSummaryLines_sin_lineas_libres_funciona()
    {
        var creditNote = CreateDraft(
            taxSummaryLines: new[] { TaxSummaryLine(taxableBase: 100m, vatRate: 15m) }
        );

        creditNote.Authorize(1000m, UserId, Guid.NewGuid(), "auth-hash-taxsummary");

        creditNote.Status.Should().Be(PurchaseCreditNoteStatus.Authorized);
        creditNote.Lines.Should().BeEmpty();
    }

    // ── Estructural: nunca mueve inventario ─────────────────────────────

    [Fact]
    public void PurchaseCreditNoteDetail_no_expone_campos_de_inventario()
    {
        // Verificación estructural (§0.1, §2.2 del diseño): esta entidad es solo para descuento/
        // promoción — nunca ItemId/WarehouseId/Quantity/PurchaseInvoiceDetailId/AffectsStock,
        // porque el caso Return delega completamente en PurchaseReturn.
        var propertyNames = typeof(PurchaseCreditNoteDetail)
            .GetProperties()
            .Select(p => p.Name)
            .ToList();

        propertyNames.Should().NotContain("ItemId");
        propertyNames.Should().NotContain("WarehouseId");
        propertyNames.Should().NotContain("Quantity");
        propertyNames.Should().NotContain("PurchaseInvoiceDetailId");
        propertyNames.Should().NotContain("AffectsStock");
    }
}
