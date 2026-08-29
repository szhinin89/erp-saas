using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Domain.Modules.Sales.Events;
using FluentAssertions;

namespace ERP.Domain.Tests.Sales;

public sealed class SalesReturnTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid SalesInvoiceId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static SalesReturn CreateDraft(string reason = "Producto en mal estado") =>
        SalesReturn.CreateDraft(
            TenantId,
            CompanyId,
            SalesInvoiceId,
            CustomerId,
            "DEV-000001",
            reason,
            UserId
        );

    private static SalesReturnDetail CreateLine(decimal quantity = 2m, decimal unitPrice = 10m) =>
        SalesReturnDetail.Create(
            Guid.NewGuid(),
            TenantId,
            Guid.NewGuid(),
            "Producto de prueba",
            quantity,
            unitPrice,
            discountPct: 0m,
            vatCode: "2",
            vatRate: 15m,
            uomCode: "UNIT"
        );

    private static SalesReturnRefundAllocation CreateAllocation(
        SalesReturn salesReturn,
        decimal amount,
        SalesReturnRefundMethod method = SalesReturnRefundMethod.Cash
    ) => SalesReturnRefundAllocation.Create(salesReturn.Id, TenantId, method, amount);

    // ── CreateDraft ───────────────────────────────────────────────────

    [Fact]
    public void CreateDraft_con_datos_validos_queda_en_borrador()
    {
        var salesReturn = CreateDraft();

        salesReturn.Status.Should().Be(SalesReturnStatus.Draft);
        salesReturn.SalesInvoiceId.Should().Be(SalesInvoiceId);
        salesReturn.CustomerId.Should().Be(CustomerId);
        salesReturn.Lines.Should().BeEmpty();
        salesReturn.RefundAllocations.Should().BeEmpty();
    }

    [Fact]
    public void CreateDraft_genera_SalesReturnDraftCreatedEvent_con_los_datos_correctos()
    {
        var salesReturn = CreateDraft();

        var evt = salesReturn
            .DomainEvents.Should()
            .ContainSingle()
            .Which.Should()
            .BeOfType<SalesReturnDraftCreatedEvent>()
            .Which;

        evt.SalesReturnId.Should().Be(salesReturn.Id);
        evt.SalesInvoiceId.Should().Be(SalesInvoiceId);
        evt.CustomerId.Should().Be(CustomerId);
        evt.ReturnNumber.Should().Be("DEV-000001");
        evt.Reason.Should().Be("Producto en mal estado");
        evt.TenantId.Should().Be(TenantId);
        evt.CompanyId.Should().Be(CompanyId);
        evt.UserId.Should().Be(UserId);
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
            SalesReturn.CreateDraft(
                TenantId,
                CompanyId,
                Guid.Empty,
                CustomerId,
                "DEV-000001",
                "Motivo",
                UserId
            );

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateDraft_rechaza_cliente_vacio()
    {
        var act = () =>
            SalesReturn.CreateDraft(
                TenantId,
                CompanyId,
                SalesInvoiceId,
                Guid.Empty,
                "DEV-000001",
                "Motivo",
                UserId
            );

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateDraft_rechaza_numero_de_devolucion_vacio()
    {
        var act = () =>
            SalesReturn.CreateDraft(
                TenantId,
                CompanyId,
                SalesInvoiceId,
                CustomerId,
                " ",
                "Motivo",
                UserId
            );

        act.Should().Throw<ArgumentException>();
    }

    // ── AddLine / RemoveLine ─────────────────────────────────────────────

    [Fact]
    public void AddLine_en_borrador_agrega_la_linea()
    {
        var salesReturn = CreateDraft();
        var line = CreateLine();

        salesReturn.AddLine(line, UserId);

        salesReturn.Lines.Should().ContainSingle().Which.Should().Be(line);
    }

    [Fact]
    public void RemoveLine_en_borrador_quita_la_linea()
    {
        var salesReturn = CreateDraft();
        var line = CreateLine();
        salesReturn.AddLine(line, UserId);

        salesReturn.RemoveLine(line.Id, UserId);

        salesReturn.Lines.Should().BeEmpty();
    }

    [Fact]
    public void RemoveLine_con_id_inexistente_lanza()
    {
        var salesReturn = CreateDraft();

        var act = () => salesReturn.RemoveLine(Guid.NewGuid(), UserId);

        act.Should().Throw<InvalidOperationException>();
    }

    // ── AddRefundAllocation / RemoveRefundAllocation ──────────────────────

    [Fact]
    public void AddRefundAllocation_en_borrador_agrega_la_asignacion()
    {
        var salesReturn = CreateDraft();
        var allocation = CreateAllocation(salesReturn, 20m);

        salesReturn.AddRefundAllocation(allocation, UserId);

        salesReturn.RefundAllocations.Should().ContainSingle().Which.Should().Be(allocation);
    }

    [Fact]
    public void RemoveRefundAllocation_con_id_inexistente_lanza()
    {
        var salesReturn = CreateDraft();

        var act = () => salesReturn.RemoveRefundAllocation(Guid.NewGuid(), UserId);

        act.Should().Throw<InvalidOperationException>();
    }

    // ── Authorize ─────────────────────────────────────────────────────

    [Fact]
    public void Authorize_con_lineas_y_asignacion_completa_autoriza_y_congela()
    {
        var salesReturn = CreateDraft();
        var line = CreateLine(quantity: 2m, unitPrice: 10m); // GrandTotal = 23 (20 + 15% IVA)
        salesReturn.AddLine(line, UserId);
        salesReturn.AddRefundAllocation(CreateAllocation(salesReturn, 23m), UserId);

        salesReturn.Authorize(UserId);

        salesReturn.Status.Should().Be(SalesReturnStatus.Authorized);
        salesReturn.AuthorizedGrandTotal.Should().Be(23m);
        salesReturn.AuthorizedSubtotal.Should().Be(20m);
        salesReturn.AuthorizedTotalVat.Should().Be(3m);
        line.IsFrozen.Should().BeTrue();
    }

    [Fact]
    public void Authorize_con_IRBPNR_en_la_linea_lo_incluye_en_AuthorizedTotalIrbpnr_y_GrandTotal()
    {
        // TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.3, Subfase 5D-4)
        var salesReturn = CreateDraft();
        var line = CreateLine(quantity: 2m, unitPrice: 10m); // Subtotal=20, VAT=3
        line.ReplaceTaxes(
            [
                SalesReturnDetailTax.Create(
                    line.Id, TenantId, "5", "5001", "IRBPNR", 0.02m,
                    ERP.Domain.Modules.SriCatalogs.Enums.SriTaxCalculationType.Specific, 0.24m
                ),
            ]
        );
        salesReturn.AddLine(line, UserId);
        salesReturn.AddRefundAllocation(CreateAllocation(salesReturn, 23.24m), UserId);

        salesReturn.Authorize(UserId);

        salesReturn.AuthorizedTotalIrbpnr.Should().Be(0.24m);
        salesReturn.TotalIrbpnr.Should().Be(0.24m);
        salesReturn.AuthorizedGrandTotal.Should().Be(23.24m); // 20 + 3 (VAT) + 0.24 (IRBPNR)
    }

    [Fact]
    public void Authorize_sin_lineas_lanza()
    {
        var salesReturn = CreateDraft();
        salesReturn.AddRefundAllocation(CreateAllocation(salesReturn, 23m), UserId);

        var act = () => salesReturn.Authorize(UserId);

        act.Should().Throw<InvalidOperationException>().WithMessage("*líneas agregadas*");
    }

    [Fact]
    public void Authorize_sin_asignaciones_de_reembolso_lanza()
    {
        var salesReturn = CreateDraft();
        salesReturn.AddLine(CreateLine(), UserId);

        var act = () => salesReturn.Authorize(UserId);

        act.Should().Throw<InvalidOperationException>().WithMessage("*reembolsará*");
    }

    [Fact]
    public void Authorize_con_suma_de_asignaciones_distinta_al_total_lanza()
    {
        var salesReturn = CreateDraft();
        salesReturn.AddLine(CreateLine(quantity: 2m, unitPrice: 10m), UserId); // GrandTotal = 23
        salesReturn.AddRefundAllocation(CreateAllocation(salesReturn, 10m), UserId);

        var act = () => salesReturn.Authorize(UserId);

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*asignaciones de reembolso*no coincide*");
    }

    [Fact]
    public void Authorize_con_asignaciones_mixtas_que_suman_el_total_autoriza()
    {
        var salesReturn = CreateDraft();
        salesReturn.AddLine(CreateLine(quantity: 2m, unitPrice: 10m), UserId); // GrandTotal = 23
        salesReturn.AddRefundAllocation(
            CreateAllocation(salesReturn, 15m, SalesReturnRefundMethod.Cash),
            UserId
        );
        salesReturn.AddRefundAllocation(
            CreateAllocation(salesReturn, 8m, SalesReturnRefundMethod.ReceivableCredit),
            UserId
        );

        salesReturn.Authorize(UserId);

        salesReturn.Status.Should().Be(SalesReturnStatus.Authorized);
    }

    [Fact]
    public void Authorize_ya_autorizada_no_se_puede_volver_a_autorizar()
    {
        var salesReturn = CreateDraft();
        salesReturn.AddLine(CreateLine(quantity: 2m, unitPrice: 10m), UserId);
        salesReturn.AddRefundAllocation(CreateAllocation(salesReturn, 23m), UserId);
        salesReturn.Authorize(UserId);

        var act = () => salesReturn.Authorize(UserId);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Authorize_lineas_congeladas_no_permiten_agregar_ni_quitar_lineas()
    {
        var salesReturn = CreateDraft();
        var line = CreateLine(quantity: 2m, unitPrice: 10m);
        salesReturn.AddLine(line, UserId);
        salesReturn.AddRefundAllocation(CreateAllocation(salesReturn, 23m), UserId);
        salesReturn.Authorize(UserId);

        var addAct = () => salesReturn.AddLine(CreateLine(), UserId);
        var removeAct = () => salesReturn.RemoveLine(line.Id, UserId);

        addAct.Should().Throw<InvalidOperationException>();
        removeAct.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Authorize_genera_SalesReturnAuthorizedEvent_con_los_datos_correctos()
    {
        var salesReturn = CreateDraft();
        salesReturn.AddLine(CreateLine(quantity: 2m, unitPrice: 10m), UserId); // GrandTotal = 23
        salesReturn.AddRefundAllocation(CreateAllocation(salesReturn, 23m), UserId);

        salesReturn.Authorize(UserId);

        var evt = salesReturn
            .DomainEvents.Should()
            .ContainSingle(e => e is SalesReturnAuthorizedEvent)
            .Which.Should()
            .BeOfType<SalesReturnAuthorizedEvent>()
            .Which;

        evt.SalesReturnId.Should().Be(salesReturn.Id);
        evt.SalesInvoiceId.Should().Be(SalesInvoiceId);
        evt.ReturnNumber.Should().Be("DEV-000001");
        evt.TenantId.Should().Be(TenantId);
        evt.CompanyId.Should().Be(CompanyId);
        evt.UserId.Should().Be(UserId);
        evt.GrandTotal.Should().Be(23m);
        evt.Subtotal.Should().Be(20m);
        evt.TotalVat.Should().Be(3m);
        evt.TotalIce.Should().Be(0m);
        evt.TotalDiscount.Should().Be(0m);
        evt.Reason.Should().Be("Producto en mal estado");
    }

    // ── Cancel ────────────────────────────────────────────────────────

    [Fact]
    public void Cancel_desde_borrador_cambia_a_cancelada()
    {
        var salesReturn = CreateDraft();

        salesReturn.Cancel(UserId);

        salesReturn.Status.Should().Be(SalesReturnStatus.Cancelled);
    }

    [Fact]
    public void Cancel_desde_borrador_genera_SalesReturnCancelledEvent_con_los_datos_correctos()
    {
        var salesReturn = CreateDraft();

        salesReturn.Cancel(UserId);

        var evt = salesReturn
            .DomainEvents.Should()
            .ContainSingle(e => e is SalesReturnCancelledEvent)
            .Which.Should()
            .BeOfType<SalesReturnCancelledEvent>()
            .Which;

        evt.SalesReturnId.Should().Be(salesReturn.Id);
        evt.SalesInvoiceId.Should().Be(SalesInvoiceId);
        evt.CustomerId.Should().Be(CustomerId);
        evt.ReturnNumber.Should().Be("DEV-000001");
        evt.Reason.Should().Be("Producto en mal estado");
        evt.TenantId.Should().Be(TenantId);
        evt.CompanyId.Should().Be(CompanyId);
        evt.UserId.Should().Be(UserId);
    }

    [Fact]
    public void Cancel_desde_autorizada_lanza()
    {
        var salesReturn = CreateDraft();
        salesReturn.AddLine(CreateLine(quantity: 2m, unitPrice: 10m), UserId);
        salesReturn.AddRefundAllocation(CreateAllocation(salesReturn, 23m), UserId);
        salesReturn.Authorize(UserId);

        var act = () => salesReturn.Cancel(UserId);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Cancel_desde_cancelada_lanza()
    {
        var salesReturn = CreateDraft();
        salesReturn.Cancel(UserId);

        var act = () => salesReturn.Cancel(UserId);

        act.Should().Throw<InvalidOperationException>();
    }

    // ══════════════════════════════ SetCreditNoteDocumentNumber (P0-01 Fase 8) ══════════════════════════════

    [Fact]
    public void SetCreditNoteDocumentNumber_asigna_el_numero_una_sola_vez()
    {
        var salesReturn = CreateDraft();

        salesReturn.SetCreditNoteDocumentNumber("001-001-000000001");

        salesReturn.CreditNoteDocumentNumber.Should().Be("001-001-000000001");
    }

    [Fact]
    public void SetCreditNoteDocumentNumber_ya_asignado_no_se_puede_reasignar()
    {
        var salesReturn = CreateDraft();
        salesReturn.SetCreditNoteDocumentNumber("001-001-000000001");

        var act = () => salesReturn.SetCreditNoteDocumentNumber("001-001-000000002");

        act.Should().Throw<InvalidOperationException>();
        salesReturn.CreditNoteDocumentNumber.Should().Be("001-001-000000001");
    }

    [Fact]
    public void SetCreditNoteDocumentNumber_rechaza_numero_vacio()
    {
        var salesReturn = CreateDraft();

        var act = () => salesReturn.SetCreditNoteDocumentNumber(" ");

        act.Should().Throw<ArgumentException>();
    }
}
