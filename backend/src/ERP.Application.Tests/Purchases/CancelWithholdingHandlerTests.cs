using ERP.Application.Common;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.Modules.Payables.Interfaces;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Purchases;

/// <summary>
/// P0-02 Fase 3 — cobertura de orquestación de <see cref="CancelWithholdingHandler"/> tras el
/// endurecimiento con transacción explícita + Lock A por <c>PurchaseInvoiceId</c> (§15.1 del
/// diseño). Las reglas de negocio puras (transición Issued → Cancelled) ya viven en
/// <c>IssuedWithholdingTests</c> (Domain).
///
/// P0-02 Fase 3 (Remediación transaccional 02): el <c>PurchaseInvoiceId</c> se descubre ANTES del
/// lock mediante <c>IPurchaseInvoiceRepository.GetWithholdingPurchaseInvoiceIdAsync</c> —
/// proyección SIN TRACKING que nunca rastrea el <c>IssuedWithholding</c> — y la retención completa
/// solo se recarga (vía <c>GetWithholdingByIdAsync</c>, tracking) DESPUÉS de adquirir el lock,
/// garantizando que <c>wh.Cancel()</c> siempre opera sobre la instancia autoritativa post-lock.
/// </summary>
public sealed class CancelWithholdingHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid PurchaseInvoiceId = Guid.NewGuid();
    private static readonly Guid EmissionPointId = Guid.NewGuid();

    private static IssuedWithholding CreateIssuedWithholding()
    {
        var wh = IssuedWithholding.CreateDraft(
            TenantId,
            CompanyId,
            PurchaseInvoiceId,
            SupplierId,
            EmissionPointId,
            new DateOnly(2026, 7, 30),
            UserId
        );
        wh.AddDetail(
            IssuedWithholdingDetail.Create(
                wh.Id,
                TenantId,
                "IVA",
                "9",
                "Retención 30% IVA",
                100m,
                30m
            )
        );
        wh.Issue("001-001-000000001", UserId);
        return wh;
    }

    private static (
        Mock<IPurchaseInvoiceRepository> repo,
        Mock<IAccountsPayableRepository> payableRepo,
        Mock<IPurchaseReturnRepository> purchaseReturnRepo,
        Mock<IUnitOfWork> uow
    ) BuildMocks()
    {
        var repo = new Mock<IPurchaseInvoiceRepository>();
        var payableRepo = new Mock<IAccountsPayableRepository>();
        var purchaseReturnRepo = new Mock<IPurchaseReturnRepository>();
        var uow = new Mock<IUnitOfWork>();
        return (repo, payableRepo, purchaseReturnRepo, uow);
    }

    private static CancelWithholdingHandler BuildHandler(
        Mock<IPurchaseInvoiceRepository> repo,
        Mock<IAccountsPayableRepository> payableRepo,
        Mock<IPurchaseReturnRepository> purchaseReturnRepo,
        Mock<IUnitOfWork> uow
    )
    {
        var tenant = new Mock<ICurrentTenant>();
        tenant.Setup(t => t.TenantId).Returns(TenantId);
        var company = new Mock<ICurrentCompany>();
        company.Setup(c => c.CompanyId).Returns(CompanyId);
        var user = new Mock<ICurrentUser>();
        user.Setup(u => u.UserId).Returns(UserId);

        return new CancelWithholdingHandler(
            repo.Object,
            payableRepo.Object,
            purchaseReturnRepo.Object,
            uow.Object,
            tenant.Object,
            company.Object,
            user.Object
        );
    }

    /// <summary>Configura descubrimiento sin tracking + recarga autoritativa para una retención dada.</summary>
    private static void SetupWithholding(
        Mock<IPurchaseInvoiceRepository> repo,
        IssuedWithholding wh
    )
    {
        repo.Setup(r =>
                r.GetWithholdingPurchaseInvoiceIdAsync(
                    TenantId,
                    wh.Id,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(wh.PurchaseInvoiceId);
        repo.Setup(r => r.GetWithholdingByIdAsync(TenantId, wh.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(wh);
    }

    [Fact]
    public async Task Anulacion_valida_cancela_la_retencion()
    {
        var (repo, payableRepo, purchaseReturnRepo, uow) = BuildMocks();
        var wh = CreateIssuedWithholding();
        SetupWithholding(repo, wh);
        repo.Setup(r => r.GetByIdAsync(TenantId, PurchaseInvoiceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PurchaseInvoice?)null);

        var handler = BuildHandler(repo, payableRepo, purchaseReturnRepo, uow);
        var result = await handler.Handle(
            new CancelWithholdingCommand(wh.Id, "Emitida por error"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        wh.Status.Should().Be(Domain.Modules.Purchases.Enums.WithholdingStatus.Cancelled);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Anulacion_valida_adquiere_Lock_A_por_el_PurchaseInvoiceId_de_la_retencion()
    {
        var (repo, payableRepo, purchaseReturnRepo, uow) = BuildMocks();
        var wh = CreateIssuedWithholding();
        SetupWithholding(repo, wh);
        repo.Setup(r => r.GetByIdAsync(TenantId, PurchaseInvoiceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PurchaseInvoice?)null);

        var handler = BuildHandler(repo, payableRepo, purchaseReturnRepo, uow);
        await handler.Handle(
            new CancelWithholdingCommand(wh.Id, "Emitida por error"),
            CancellationToken.None
        );

        uow.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        purchaseReturnRepo.Verify(
            r =>
                r.AcquireFinancialLockAsync(
                    TenantId,
                    PurchaseInvoiceId,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Orden obligatorio (§7.3 de la remediación): BeginTransaction siempre se abre primero — el
    /// descubrimiento del PurchaseInvoiceId ocurre YA dentro de la transacción. Cuando la retención
    /// no existe, la transacción abierta se revierte (rollback) antes de retornar NotFound; nunca
    /// se adquiere ningún lock ni se confirma nada.
    /// </summary>
    [Fact]
    public async Task Anulacion_sobre_retencion_inexistente_retorna_NotFound_y_revierte_la_transaccion_abierta()
    {
        var (repo, payableRepo, purchaseReturnRepo, uow) = BuildMocks();
        var missingId = Guid.NewGuid();
        repo.Setup(r =>
                r.GetWithholdingPurchaseInvoiceIdAsync(
                    TenantId,
                    missingId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((Guid?)null);

        var handler = BuildHandler(repo, payableRepo, purchaseReturnRepo, uow);
        var result = await handler.Handle(
            new CancelWithholdingCommand(missingId, "Motivo"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        uow.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        uow.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        purchaseReturnRepo.Verify(
            r =>
                r.AcquireFinancialLockAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task Anulacion_de_retencion_ya_cancelada_retorna_ValidationFailure_y_hace_rollback()
    {
        var (repo, payableRepo, purchaseReturnRepo, uow) = BuildMocks();
        var wh = CreateIssuedWithholding();
        wh.Cancel("Primera anulación", UserId);
        SetupWithholding(repo, wh);

        var handler = BuildHandler(repo, payableRepo, purchaseReturnRepo, uow);
        var result = await handler.Handle(
            new CancelWithholdingCommand(wh.Id, "Segunda anulación"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        uow.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Fase 3, Remediación transaccional 02 ───────────────────────────────

    /// <summary>
    /// Orden transaccional estricto: BeginTransaction → descubrimiento sin tracking → Lock A →
    /// recarga autoritativa (tracking) → Cancel → SaveChanges → Commit.
    /// </summary>
    [Fact]
    public async Task Orden_transaccional_BeginTx_descubrimiento_LockA_recarga_Cancel_SaveChanges_Commit()
    {
        var (repo, payableRepo, purchaseReturnRepo, uow) = BuildMocks();
        var wh = CreateIssuedWithholding();

        var sequence = new MockSequence();
        uow.InSequence(sequence)
            .Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repo.InSequence(sequence)
            .Setup(r =>
                r.GetWithholdingPurchaseInvoiceIdAsync(
                    TenantId,
                    wh.Id,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(wh.PurchaseInvoiceId);
        purchaseReturnRepo
            .InSequence(sequence)
            .Setup(r =>
                r.AcquireFinancialLockAsync(
                    TenantId,
                    wh.PurchaseInvoiceId,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask);
        repo.InSequence(sequence)
            .Setup(r => r.GetWithholdingByIdAsync(TenantId, wh.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(wh);
        repo.Setup(r => r.GetByIdAsync(TenantId, PurchaseInvoiceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PurchaseInvoice?)null);
        repo.InSequence(sequence)
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        uow.InSequence(sequence)
            .Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = BuildHandler(repo, payableRepo, purchaseReturnRepo, uow);
        var result = await handler.Handle(
            new CancelWithholdingCommand(wh.Id, "Motivo"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// Estado concurrente simulado: el descubrimiento (sin tracking) solo confirma el
    /// PurchaseInvoiceId; entre ese momento y la adquisición del lock, otra transacción ya anuló
    /// la retención. La recarga autoritativa posterior al lock devuelve el estado YA cancelado, y
    /// el guard de dominio existente ("Solo se pueden anular retenciones emitidas") rechaza sobre
    /// esa instancia — nunca sobre un estado optimista previo al lock.
    /// </summary>
    [Fact]
    public async Task Estado_ya_cancelado_detectado_solo_en_la_recarga_post_lock_rechaza_y_hace_rollback()
    {
        var (repo, payableRepo, purchaseReturnRepo, uow) = BuildMocks();
        var wh = CreateIssuedWithholding();

        repo.Setup(r =>
                r.GetWithholdingPurchaseInvoiceIdAsync(
                    TenantId,
                    wh.Id,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(wh.PurchaseInvoiceId);
        // La recarga posterior al lock refleja que, mientras tanto, otra transacción ya anuló
        // la retención.
        wh.Cancel("Anulada por otra transacción concurrente", UserId);
        repo.Setup(r => r.GetWithholdingByIdAsync(TenantId, wh.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(wh);

        var handler = BuildHandler(repo, payableRepo, purchaseReturnRepo, uow);
        var result = await handler.Handle(
            new CancelWithholdingCommand(wh.Id, "Segunda anulación concurrente"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Solo se pueden anular retenciones emitidas");
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        uow.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
