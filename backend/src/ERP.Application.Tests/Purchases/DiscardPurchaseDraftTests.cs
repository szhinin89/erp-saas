using ERP.Application.Common;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.Events;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Models;
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

    private static readonly string AccessKey = new('7', 49);

    private static PurchaseInvoice Draft(Guid? branchId = null, string? accessKey = null)
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
            0,
            accessKey: accessKey
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
    public void Borrador_se_descarta_como_Discarded_sin_evento_de_anulacion_y_con_evento_de_auditoria()
    {
        var invoice = Draft();

        invoice.DiscardDraft(UserId, "  Cargado por error  ");

        invoice.Status.Should().Be(PurchaseStatus.Discarded);
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
    }

    // ── Handler ───────────────────────────────────────────────────────────────

    /// <summary>Recepción DINADEC verificada (XML autorizado), opcionalmente vinculada a una compra.</summary>
    private static PurchaseReceptionDocument Reception(Guid? linkedPurchaseId)
    {
        var reception = PurchaseReceptionDocument.Create(
            TenantId,
            CompanyId,
            BranchId,
            PurchaseReceptionSourceDocType.Invoice,
            "1790012345001",
            "DINADEC S.A.",
            Guid.NewGuid(),
            AccessKey,
            "001-001-000000123",
            new DateOnly(2026, 10, 12),
            null,
            300m,
            0m,
            300m,
            UserId
        );
        var line = PurchaseReceptionLine.Create(
            reception.Id, TenantId, "FANTA", 50m, 6m, "0", "2", 0m, 0m, 0m, 0m, 300m, 300m,
            supplierCode: "8433"
        );
        reception.AttachSriAuthorization(
            "1",
            DateTime.UtcNow,
            "<factura/>",
            DateTime.UtcNow,
            [line],
            UserId,
            "01",
            null,
            new PurchaseReceptionProcessingOutcome(PurchaseReceptionProcessingStatus.Processed, 1, 1, null)
        );
        if (linkedPurchaseId is { } purchaseId)
            reception.MarkProcessed(purchaseId, UserId);
        return reception;
    }

    private static (
        DiscardPurchaseDraftHandler Handler,
        Mock<IPurchaseInvoiceRepository> Repo,
        Mock<IPurchaseReceptionDocumentRepository> Receptions
    ) Build(PurchaseInvoice? invoice, PurchaseReceptionDocument? reception = null)
    {
        var repo = new Mock<IPurchaseInvoiceRepository>();
        if (invoice is not null)
            repo.Setup(r => r.GetByIdAsync(TenantId, invoice.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(invoice);
        var receptions = new Mock<IPurchaseReceptionDocumentRepository>();
        if (reception is not null)
            receptions
                .Setup(r => r.GetByAccessKeyAsync(TenantId, reception.AccessKey, It.IsAny<CancellationToken>()))
                .ReturnsAsync(reception);
        var handler = new DiscardPurchaseDraftHandler(
            repo.Object,
            receptions.Object,
            Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
            Mock.Of<ICurrentBranch>(b => b.BranchId == BranchId),
            Mock.Of<ICurrentUser>(u => u.UserId == UserId)
        );
        return (handler, repo, receptions);
    }

    [Fact]
    public async Task Handler_descarta_el_borrador_y_expone_estado_Discarded()
    {
        var invoice = Draft();
        var (handler, repo, _) = Build(invoice);

        var result = await handler.Handle(
            new DiscardPurchaseDraftCommand(invoice.Id, "Duplicado"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Status.Should().Be("Discarded");
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handler_rechaza_una_compra_confirmada_y_no_persiste()
    {
        var invoice = Draft();
        invoice.Confirm(UserId);
        var (handler, repo, _) = Build(invoice);

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
        var (handler, repo, _) = Build(invoice);

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
        // Estructural: el handler solo recibe los repositorios de compras y de recepción y el contexto
        // autenticado — no tiene forma de tocar inventario, CxP, contabilidad ni retenciones.
        var dependencies = typeof(DiscardPurchaseDraftHandler)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(p => p.ParameterType)
            .ToList();

        dependencies.Should().BeEquivalentTo(
            [
                typeof(IPurchaseInvoiceRepository),
                typeof(IPurchaseReceptionDocumentRepository),
                typeof(ICurrentTenant),
                typeof(ICurrentBranch),
                typeof(ICurrentUser),
            ]
        );
    }

    [Fact]
    public void Comando_es_branch_scoped()
    {
        new DiscardPurchaseDraftCommand(Guid.NewGuid()).Should().BeAssignableTo<IBranchScopedRequest>();
    }

    // ── Liberar la recepción de origen ───────────────────────────────────────

    [Fact]
    public async Task Borrador_desde_recepcion_libera_la_recepcion_Verified_y_sin_PurchaseId()
    {
        var invoice = Draft(accessKey: AccessKey);
        var reception = Reception(linkedPurchaseId: invoice.Id);
        reception.Status.Should().Be(PurchaseReceptionDocumentStatus.Processed);
        var (handler, repo, _) = Build(invoice, reception);

        var result = await handler.Handle(new DiscardPurchaseDraftCommand(invoice.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        invoice.Status.Should().Be(ERP.Domain.Modules.Purchases.Enums.PurchaseStatus.Discarded);
        reception.Status.Should().Be(PurchaseReceptionDocumentStatus.Verified);
        reception.PurchaseId.Should().BeNull();
        reception.XmlContent.Should().Be("<factura/>", "el XML/evidencia no se toca");
        reception.Lines.Should().ContainSingle();
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Recepcion_vinculada_a_otra_compra_no_se_modifica()
    {
        var invoice = Draft(accessKey: AccessKey);
        var otherPurchaseId = Guid.NewGuid();
        var reception = Reception(linkedPurchaseId: otherPurchaseId);
        var (handler, _, _) = Build(invoice, reception);

        var result = await handler.Handle(new DiscardPurchaseDraftCommand(invoice.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        reception.Status.Should().Be(PurchaseReceptionDocumentStatus.Processed);
        reception.PurchaseId.Should().Be(otherPurchaseId);
    }

    [Fact]
    public async Task Borrador_sin_recepcion_se_descarta_igual_sin_buscar_recepcion()
    {
        var invoice = Draft(accessKey: null);
        var (handler, repo, receptions) = Build(invoice);

        var result = await handler.Handle(new DiscardPurchaseDraftCommand(invoice.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        invoice.Status.Should().Be(ERP.Domain.Modules.Purchases.Enums.PurchaseStatus.Discarded);
        receptions.Verify(
            r => r.GetByAccessKeyAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Confirmada_sigue_rechazada_y_no_libera_ninguna_recepcion()
    {
        var invoice = Draft(accessKey: AccessKey);
        var reception = Reception(linkedPurchaseId: invoice.Id);
        invoice.Confirm(UserId);
        var (handler, repo, _) = Build(invoice, reception);

        var result = await handler.Handle(new DiscardPurchaseDraftCommand(invoice.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        reception.Status.Should().Be(PurchaseReceptionDocumentStatus.Processed);
        reception.PurchaseId.Should().Be(invoice.Id);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
