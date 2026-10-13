using ERP.Application.Common;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.Events;
using ERP.Domain.Modules.Purchases.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Purchases;

/// <summary>
/// FASE 5 PURCHASE-UX — descartar borrador de compra: soft Draft → Cancelled, sin
/// PurchaseInvoiceCancelledEvent (que dispara reversos) y con evento de auditoría propio.
/// </summary>
public sealed class DiscardPurchaseDraftTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static PurchaseInvoice Draft(Guid? branchId = null)
    {
        var invoice = PurchaseInvoice.CreateDraft(
            TenantId,
            CompanyId,
            branchId ?? BranchId,
            Guid.NewGuid(),
            "DINADEC S.A.",
            "1790012345001",
            "01",
            "001-001-000000123",
            new DateOnly(2026, 10, 12),
            UserId,
            Guid.NewGuid(),
            "Contado",
            1,
            0
        );
        invoice.ReplaceLines(
            [PurchaseInvoiceDetail.Create(invoice.Id, TenantId, "FANTA", 50m, 6m, "0", "UNIT")],
            UserId
        );
        invoice.ClearDomainEvents();
        return invoice;
    }

    // ── Dominio ───────────────────────────────────────────────────────────────

    [Fact]
    public void Borrador_se_descarta_como_Cancelled_sin_evento_de_anulacion_y_con_evento_de_auditoria()
    {
        var invoice = Draft();

        invoice.DiscardDraft(UserId, "  Cargado por error  ");

        invoice.Status.Should().Be(PurchaseStatus.Cancelled);
        invoice.IsDiscardedDraft.Should().BeTrue();
        invoice.CancelReason.Should().Be("Cargado por error");
        invoice.CancelledBy.Should().Be(UserId);
        invoice.CancelledAt.Should().NotBeNull();
        invoice.DomainEvents.Should().NotContain(e => e is PurchaseInvoiceCancelledEvent);
        invoice.DomainEvents.Should().ContainSingle(e => e is PurchaseDraftDiscardedEvent)
            .Which.Should().BeOfType<PurchaseDraftDiscardedEvent>()
            .Which.Reason.Should().Be("Cargado por error");
    }

    [Fact]
    public void Sin_motivo_registra_el_motivo_por_defecto()
    {
        var invoice = Draft();

        invoice.DiscardDraft(UserId);

        invoice.CancelReason.Should().Be(PurchaseInvoice.DiscardedDraftDefaultReason);
    }

    [Fact]
    public void Solo_un_borrador_puede_descartarse()
    {
        var confirmed = Draft();
        confirmed.Confirm(UserId);
        FluentActions.Invoking(() => confirmed.DiscardDraft(UserId))
            .Should().Throw<DomainRuleViolationException>();

        var discarded = Draft();
        discarded.DiscardDraft(UserId);
        FluentActions.Invoking(() => discarded.DiscardDraft(UserId))
            .Should().Throw<DomainRuleViolationException>();
    }

    [Fact]
    public void Una_compra_confirmada_y_anulada_no_es_un_borrador_descartado()
    {
        var invoice = Draft();
        invoice.Confirm(UserId);
        invoice.Cancel("Anulada por error", UserId);

        invoice.Status.Should().Be(PurchaseStatus.Cancelled);
        invoice.IsDiscardedDraft.Should().BeFalse();
    }

    // ── Handler ───────────────────────────────────────────────────────────────

    private static (DiscardPurchaseDraftHandler Handler, Mock<IPurchaseInvoiceRepository> Repo) Build(
        PurchaseInvoice? invoice
    )
    {
        var repo = new Mock<IPurchaseInvoiceRepository>();
        if (invoice is not null)
            repo.Setup(r => r.GetByIdAsync(TenantId, invoice.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(invoice);
        var handler = new DiscardPurchaseDraftHandler(
            repo.Object,
            Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
            Mock.Of<ICurrentBranch>(b => b.BranchId == BranchId),
            Mock.Of<ICurrentUser>(u => u.UserId == UserId)
        );
        return (handler, repo);
    }

    [Fact]
    public async Task Handler_descarta_el_borrador_y_expone_IsDiscardedDraft()
    {
        var invoice = Draft();
        var (handler, repo) = Build(invoice);

        var result = await handler.Handle(
            new DiscardPurchaseDraftCommand(invoice.Id, "Duplicado"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Status.Should().Be("Cancelled");
        result.Value.IsDiscardedDraft.Should().BeTrue();
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handler_rechaza_una_compra_confirmada_y_no_persiste()
    {
        var invoice = Draft();
        invoice.Confirm(UserId);
        var (handler, repo) = Build(invoice);

        var result = await handler.Handle(
            new DiscardPurchaseDraftCommand(invoice.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Anular compra");
        invoice.Status.Should().Be(PurchaseStatus.Confirmed);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handler_no_ve_borradores_de_otra_sucursal()
    {
        var invoice = Draft(branchId: Guid.NewGuid());
        var (handler, repo) = Build(invoice);

        var result = await handler.Handle(
            new DiscardPurchaseDraftCommand(invoice.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        invoice.Status.Should().Be(PurchaseStatus.Draft);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Handler_no_depende_de_inventario_CxP_contabilidad_ni_retencion()
    {
        // Estructural: el handler solo recibe el repositorio de compras y el contexto autenticado —
        // no tiene forma de tocar inventario, CxP, contabilidad ni retenciones.
        var dependencies = typeof(DiscardPurchaseDraftHandler)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(p => p.ParameterType)
            .ToList();

        dependencies.Should().BeEquivalentTo(
            [typeof(IPurchaseInvoiceRepository), typeof(ICurrentTenant), typeof(ICurrentBranch), typeof(ICurrentUser)]
        );
    }

    [Fact]
    public void Comando_es_branch_scoped()
    {
        new DiscardPurchaseDraftCommand(Guid.NewGuid()).Should().BeAssignableTo<IBranchScopedRequest>();
    }
}
