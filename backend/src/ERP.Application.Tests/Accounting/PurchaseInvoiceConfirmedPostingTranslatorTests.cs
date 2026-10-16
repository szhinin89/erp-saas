using ERP.Application.Modules.Accounting.Continuity;
using ERP.Application.Modules.Accounting.Posting.Translators;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Purchases.Events;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Accounting;

/// <summary>
/// ACCOUNTING-CONTINUITY-01 — la confirmación de compra registra una obligación durable Pending (no
/// invoca el Posting Engine dentro de la transacción de la compra).
/// </summary>
public sealed class PurchaseInvoiceConfirmedPostingTranslatorTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();

    private static PurchaseInvoiceConfirmedEvent Event(Guid invoiceId, DateOnly issueDate) =>
        new(
            TenantId,
            invoiceId,
            SupplierId,
            "001-001-000000001",
            118.48m,
            CompanyId,
            issueDate,
            100m,
            103m,
            15m,
            0m,
            2m,
            totalIrbpnr: 0.48m
        );

    [Fact]
    public async Task Registra_obligacion_Pending_con_identidad_fecha_e_importes_del_origen()
    {
        var h = new AccountingContinuityTestHarness(TenantId, CompanyId);
        var invoiceId = Guid.NewGuid();
        var issueDate = new DateOnly(2026, 7, 25);

        await new PurchaseInvoiceConfirmedPostingTranslator(h.Recorder).Handle(
            Event(invoiceId, issueDate),
            CancellationToken.None
        );

        var posting = h.Added.Single();
        posting.Status.Should().Be(AccountingPostingStatus.Pending);
        posting.SourceModule.Should().Be("Purchases");
        posting.FactType.Should().Be("InvoiceReceived");
        posting.SourceEventId.Should().Be(invoiceId);
        posting.SourceDocumentNumber.Should().Be("001-001-000000001");
        posting.BusinessDate.Should().Be(issueDate);
        posting.EntryDate.Should().Be(issueDate);
        posting.CreatedBy.Should().Be(AccountingContinuityTestHarness.UserId);
        posting.CorrelationId.Should().Be(AccountingContinuityTestHarness.CorrelationId);
        var payload = h.SinglePayload();
        payload.Subtotal.Should().Be(103m, "Subtotal = CostSubtotal congelado por Compras");
        payload.TotalVat.Should().Be(15m);
        payload.TotalDiscount.Should().Be(2m);
        payload.GrandTotal.Should().Be(118.48m);
        payload.TotalIrbpnr.Should().Be(0.48m);
        h.Queue.Drain().Should().Equal(posting.Id);
    }

    [Fact]
    public async Task Fecha_fuera_de_la_vigencia_Enabled_no_genera_obligacion()
    {
        var h = new AccountingContinuityTestHarness(
            TenantId,
            CompanyId,
            effectiveFrom: new DateOnly(2026, 8, 1)
        );

        await new PurchaseInvoiceConfirmedPostingTranslator(h.Recorder).Handle(
            Event(Guid.NewGuid(), new DateOnly(2026, 7, 31)),
            CancellationToken.None
        );

        h.Added.Should().BeEmpty("activar Accounting no genera contabilización histórica");
        h.Queue.Drain().Should().BeEmpty();
    }

    [Fact]
    public async Task Empresa_sin_modo_operativo_definido_rechaza_la_operacion()
    {
        var h = new AccountingContinuityTestHarness(TenantId, CompanyId, mode: null);

        var act = () =>
            new PurchaseInvoiceConfirmedPostingTranslator(h.Recorder).Handle(
                Event(Guid.NewGuid(), new DateOnly(2026, 7, 25)),
                CancellationToken.None
            );

        await act.Should()
            .ThrowAsync<DomainRuleViolationException>()
            .WithMessage("*modo operativo de Contabilidad*");
        h.Added.Should().BeEmpty();
    }

    [Fact]
    public async Task Obligacion_ya_existente_para_la_identidad_no_se_duplica()
    {
        var h = new AccountingContinuityTestHarness(TenantId, CompanyId);
        var invoiceId = Guid.NewGuid();
        var existing = ERP.Domain.Modules.Accounting.Entities.AccountingPosting.CreatePending(
            TenantId,
            CompanyId,
            "Purchases",
            "InvoiceReceived",
            invoiceId,
            null,
            new DateOnly(2026, 7, 25),
            new DateOnly(2026, 7, 25),
            AccountingPostingPayload.CurrentVersion,
            "{}",
            Guid.NewGuid(),
            null
        );
        h.Postings.Setup(p =>
                p.FindByKeyAsync(TenantId, CompanyId, "Purchases", "InvoiceReceived", invoiceId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(existing);

        await new PurchaseInvoiceConfirmedPostingTranslator(h.Recorder).Handle(
            Event(invoiceId, new DateOnly(2026, 7, 25)),
            CancellationToken.None
        );

        h.Added.Should().BeEmpty();
    }
}
