using ERP.Application.Common;
using ERP.Application.Modules.Pricing.Services;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Purchases;

/// <summary>
/// Hallazgo ALTO auditoría de aislamiento (Sales/Purchases cross-branch): ApplyGlobalDiscountCommand,
/// AllocateFreightCommand, RecalculatePurchaseCommand, LoadPvpSnapshotsCommand y UpdateLinePvpCommand
/// están marcados IBranchScopedRequest, pero ese marker solo exige sucursal activa autorizada — no
/// que la compra cargada pertenezca a esa sucursal. Cada handler debe rechazar con NotFound (nunca
/// revelar existencia cross-branch) cuando la compra pertenece a otra sucursal, y seguir funcionando
/// normalmente cuando pertenece a la sucursal activa.
/// </summary>
public sealed class PurchaseCostAndMarginBranchScopeTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid PaymentTermId = Guid.NewGuid();

    private static PurchaseInvoice CreateDraftWithOneLine(Guid branchId)
    {
        var inv = PurchaseInvoice.CreateDraft(
            TenantId,
            CompanyId,
            branchId,
            SupplierId,
            "Proveedor Test",
            "1234567890001",
            "01",
            $"001-001-{Random.Shared.Next(100000000, 999999999)}",
            DateOnly.FromDateTime(DateTime.UtcNow),
            UserId,
            PaymentTermId,
            "Contado",
            1,
            0
        );
        var line = PurchaseInvoiceDetail.Create(
            inv.Id,
            TenantId,
            "Producto",
            quantity: 1m,
            unitPrice: 100m,
            vatCode: "10",
            uomCode: "UNIT"
        );
        inv.ReplaceLines([line], UserId);
        return inv;
    }

    private static Mock<IPurchaseInvoiceRepository> BuildRepo(PurchaseInvoice inv)
    {
        var repo = new Mock<IPurchaseInvoiceRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, inv.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inv);
        repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        return repo;
    }

    // ── ApplyGlobalDiscountHandler ──────────────────────────────────────

    [Fact]
    public async Task ApplyGlobalDiscount_compra_de_otra_sucursal_retorna_NotFound()
    {
        var inv = CreateDraftWithOneLine(BranchId);
        var repo = BuildRepo(inv);
        var handler = new ApplyGlobalDiscountHandler(
            repo.Object,
            Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
            Mock.Of<ICurrentBranch>(b => b.BranchId == Guid.NewGuid()),
            Mock.Of<ICurrentUser>(u => u.UserId == UserId)
        );

        var result = await handler.Handle(
            new ApplyGlobalDiscountCommand(inv.Id, 10m),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }

    [Fact]
    public async Task ApplyGlobalDiscount_compra_de_la_misma_sucursal_funciona()
    {
        var inv = CreateDraftWithOneLine(BranchId);
        var repo = BuildRepo(inv);
        var handler = new ApplyGlobalDiscountHandler(
            repo.Object,
            Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
            Mock.Of<ICurrentBranch>(b => b.BranchId == BranchId),
            Mock.Of<ICurrentUser>(u => u.UserId == UserId)
        );

        var result = await handler.Handle(
            new ApplyGlobalDiscountCommand(inv.Id, 10m),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
    }

    // ── AllocateFreightHandler ───────────────────────────────────────────

    [Fact]
    public async Task AllocateFreight_compra_de_otra_sucursal_retorna_NotFound()
    {
        var inv = CreateDraftWithOneLine(BranchId);
        var repo = BuildRepo(inv);
        var handler = new AllocateFreightHandler(
            repo.Object,
            Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
            Mock.Of<ICurrentBranch>(b => b.BranchId == Guid.NewGuid()),
            Mock.Of<ICurrentUser>(u => u.UserId == UserId)
        );

        var result = await handler.Handle(new AllocateFreightCommand(inv.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }

    [Fact]
    public async Task AllocateFreight_compra_de_la_misma_sucursal_funciona()
    {
        var inv = CreateDraftWithOneLine(BranchId);
        var repo = BuildRepo(inv);
        var handler = new AllocateFreightHandler(
            repo.Object,
            Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
            Mock.Of<ICurrentBranch>(b => b.BranchId == BranchId),
            Mock.Of<ICurrentUser>(u => u.UserId == UserId)
        );

        var result = await handler.Handle(new AllocateFreightCommand(inv.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
    }

    // ── RecalculatePurchaseHandler ───────────────────────────────────────

    [Fact]
    public async Task Recalculate_compra_de_otra_sucursal_retorna_NotFound()
    {
        var inv = CreateDraftWithOneLine(BranchId);
        var repo = BuildRepo(inv);
        var handler = new RecalculatePurchaseHandler(
            repo.Object,
            Mock.Of<ERP.Application.Modules.Purchases.Services.ISriTaxResolver>(),
            Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
            Mock.Of<ICurrentBranch>(b => b.BranchId == Guid.NewGuid()),
            Mock.Of<ICurrentUser>(u => u.UserId == UserId)
        );

        var result = await handler.Handle(new RecalculatePurchaseCommand(inv.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }

    // ── LoadPvpSnapshotsHandler ──────────────────────────────────────────

    [Fact]
    public async Task LoadPvpSnapshots_compra_de_otra_sucursal_retorna_NotFound()
    {
        var inv = CreateDraftWithOneLine(BranchId);
        var repo = BuildRepo(inv);
        var handler = new LoadPvpSnapshotsHandler(
            repo.Object,
            Mock.Of<IPricingResolver>(),
            Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
            Mock.Of<ICurrentBranch>(b => b.BranchId == Guid.NewGuid()),
            Mock.Of<ICurrentUser>(u => u.UserId == UserId)
        );

        var result = await handler.Handle(new LoadPvpSnapshotsCommand(inv.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }

    [Fact]
    public async Task LoadPvpSnapshots_compra_de_la_misma_sucursal_funciona()
    {
        var inv = CreateDraftWithOneLine(BranchId);
        var repo = BuildRepo(inv);
        var pricing = new Mock<IPricingResolver>();
        pricing
            .Setup(p => p.ResolveAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                ERP.Application.Common.Result<
                    ERP.Application.Modules.Pricing.DTOs.PricingResult
                >.Failure(ApiResponseCodes.Common.NotFound, "sin precio")
            );
        var handler = new LoadPvpSnapshotsHandler(
            repo.Object,
            pricing.Object,
            Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
            Mock.Of<ICurrentBranch>(b => b.BranchId == BranchId),
            Mock.Of<ICurrentUser>(u => u.UserId == UserId)
        );

        var result = await handler.Handle(new LoadPvpSnapshotsCommand(inv.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
    }

    // ── UpdateLinePvpHandler ─────────────────────────────────────────────

    [Fact]
    public async Task UpdateLinePvp_compra_de_otra_sucursal_retorna_NotFound()
    {
        var inv = CreateDraftWithOneLine(BranchId);
        var repo = BuildRepo(inv);
        var handler = new UpdateLinePvpHandler(
            repo.Object,
            Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
            Mock.Of<ICurrentBranch>(b => b.BranchId == Guid.NewGuid()),
            Mock.Of<ICurrentUser>(u => u.UserId == UserId)
        );

        var result = await handler.Handle(
            new UpdateLinePvpCommand(inv.Id, inv.Lines[0].Id, 15m),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }

    [Fact]
    public async Task UpdateLinePvp_compra_de_la_misma_sucursal_funciona()
    {
        var inv = CreateDraftWithOneLine(BranchId);
        var repo = BuildRepo(inv);
        var handler = new UpdateLinePvpHandler(
            repo.Object,
            Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
            Mock.Of<ICurrentBranch>(b => b.BranchId == BranchId),
            Mock.Of<ICurrentUser>(u => u.UserId == UserId)
        );

        var result = await handler.Handle(
            new UpdateLinePvpCommand(inv.Id, inv.Lines[0].Id, 15m),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
    }
}
