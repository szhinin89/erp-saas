using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Services;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Purchases.PurchaseReception;

/// <summary>
/// FASE 5 PURCHASE-UX — estado real de la compra relacionada con una recepción (NONE / DRAFT /
/// CONFIRMED / CANCELLED / DISCARDED), única resolución compartida por importación y descargas.
/// </summary>
public sealed class PurchaseReceptionPurchaseStateResolverTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private const string AccessKey = "0107202601179135268800120150270001617400016174011";

    private static PurchaseInvoice Draft()
    {
        var invoice = PurchaseInvoice.CreateDraft(
            TenantId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "DINADEC S.A.",
            "0990011223001",
            "01",
            "034-005-002226390",
            new DateOnly(2026, 10, 12),
            UserId,
            Guid.NewGuid(),
            "Contado",
            1,
            0,
            accessKey: AccessKey
        );
        invoice.ReplaceLines(
            [PurchaseInvoiceDetail.Create(invoice.Id, TenantId, "FANTA", 50m, 6m, "0", "UNIT")],
            UserId
        );
        return invoice;
    }

    private static Mock<IPurchaseInvoiceRepository> Repo(
        PurchaseInvoice? active,
        HistoricalPurchaseRef? latestCancelled
    )
    {
        var repo = new Mock<IPurchaseInvoiceRepository>();
        repo.Setup(r => r.GetByAccessKeyAsync(TenantId, AccessKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(active);
        repo.Setup(r =>
                r.GetLatestHistoricalByAccessKeyAsync(TenantId, AccessKey, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(latestCancelled);
        return repo;
    }

    private static Task<PurchaseReceptionPurchaseInfo> Resolve(Mock<IPurchaseInvoiceRepository> repo) =>
        PurchaseReceptionPurchaseStateResolver.ResolveAsync(repo.Object, TenantId, AccessKey);

    [Fact]
    public async Task NONE_sin_ninguna_compra()
    {
        var info = await Resolve(Repo(active: null, latestCancelled: null));

        info.State.Should().Be(PurchaseReceptionPurchaseState.None);
        info.PurchaseId.Should().BeNull();
        info.CancelledPurchaseId.Should().BeNull();
    }

    [Fact]
    public async Task DRAFT_compra_activa_en_borrador_sin_consultar_canceladas()
    {
        var draft = Draft();
        var repo = Repo(active: draft, latestCancelled: null);

        var info = await Resolve(repo);

        info.State.Should().Be(PurchaseReceptionPurchaseState.Draft);
        info.PurchaseId.Should().Be(draft.Id);
        info.CancelledPurchaseId.Should().BeNull();
        repo.Verify(
            r => r.GetLatestHistoricalByAccessKeyAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "con compra activa no se consulta el historial"
        );
    }

    [Fact]
    public async Task CONFIRMED_compra_activa_confirmada()
    {
        var confirmed = Draft();
        confirmed.Confirm(UserId);

        var info = await Resolve(Repo(active: confirmed, latestCancelled: null));

        info.State.Should().Be(PurchaseReceptionPurchaseState.Confirmed);
        info.PurchaseId.Should().Be(confirmed.Id);
    }

    [Fact]
    public async Task CANCELLED_sin_activa_y_ultima_anulada_fue_confirmada()
    {
        var cancelledId = Guid.NewGuid();

        var info = await Resolve(Repo(active: null, new HistoricalPurchaseRef(cancelledId, Status: ERP.Domain.Modules.Purchases.Enums.PurchaseStatus.Cancelled)));

        info.State.Should().Be(PurchaseReceptionPurchaseState.Cancelled);
        info.CancelledPurchaseId.Should().Be(cancelledId);
    }

    [Fact]
    public async Task DISCARDED_sin_activa_y_ultima_anulada_nunca_confirmada()
    {
        var discardedId = Guid.NewGuid();

        var info = await Resolve(Repo(active: null, new HistoricalPurchaseRef(discardedId, Status: ERP.Domain.Modules.Purchases.Enums.PurchaseStatus.Discarded)));

        info.State.Should().Be(PurchaseReceptionPurchaseState.Discarded);
        info.CancelledPurchaseId.Should().Be(discardedId);
    }

    [Fact]
    public async Task Sin_clave_de_acceso_es_NONE_sin_consultar()
    {
        var repo = new Mock<IPurchaseInvoiceRepository>();

        var info = await PurchaseReceptionPurchaseStateResolver.ResolveAsync(repo.Object, TenantId, " ");

        info.State.Should().Be(PurchaseReceptionPurchaseState.None);
        repo.VerifyNoOtherCalls();
    }
}
