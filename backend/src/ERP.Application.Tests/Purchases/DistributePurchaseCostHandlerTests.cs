using ERP.Application.Common;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Purchases;

/// <summary>
/// PURCHASE-FREIGHT-DISTRIBUTION-MODAL-01 — DistributePurchaseCostHandler: aplica el prorrateo
/// aditivo revisado en el modal "Distribuir flete/gasto" sobre las líneas incluidas por el usuario.
/// </summary>
public sealed class DistributePurchaseCostHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static PurchaseInvoice CreateDraftWithLines(params (decimal qty, decimal price)[] lines)
    {
        var inv = PurchaseInvoice.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            Guid.NewGuid(),
            "Proveedor Test",
            "1234567890001",
            "01",
            $"001-001-{Random.Shared.Next(100000000, 999999999)}",
            DateOnly.FromDateTime(DateTime.UtcNow),
            UserId,
            Guid.NewGuid(),
            "Contado",
            1,
            0
        );
        var details = lines
            .Select(l => PurchaseInvoiceDetail.Create(inv.Id, TenantId, "Producto", l.qty, l.price, "10", "UNIT"))
            .ToList();
        inv.ReplaceLines(details, UserId);
        return inv;
    }

    private static DistributePurchaseCostHandler BuildHandler(
        PurchaseInvoice? invoice,
        out Mock<IPurchaseInvoiceRepository> repo
    )
    {
        repo = new Mock<IPurchaseInvoiceRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);
        repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        return new DistributePurchaseCostHandler(
            repo.Object,
            Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
            Mock.Of<ICurrentUser>(u => u.UserId == UserId)
        );
    }

    [Fact]
    public async Task Distribuye_flete_entre_lineas_incluidas_y_persiste()
    {
        var inv = CreateDraftWithLines((1m, 100m), (1m, 300m));
        var handler = BuildHandler(inv, out var repo);

        var result = await handler.Handle(
            new DistributePurchaseCostCommand(
                inv.Id,
                "Freight",
                40m,
                inv.Lines.Select(l => l.Id).ToList()
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        inv.Lines[0].FreightAllocated.Should().Be(10m);
        inv.Lines[1].FreightAllocated.Should().Be(30m);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Distribuye_otro_gasto_entre_lineas_incluidas()
    {
        var inv = CreateDraftWithLines((1m, 100m), (1m, 100m));
        var handler = BuildHandler(inv, out _);

        var result = await handler.Handle(
            new DistributePurchaseCostCommand(
                inv.Id,
                "OtherCost",
                20m,
                inv.Lines.Select(l => l.Id).ToList()
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        inv.Lines[0].OtherCostsAllocated.Should().Be(10m);
        inv.Lines[1].OtherCostsAllocated.Should().Be(10m);
    }

    [Fact]
    public async Task Compra_no_encontrada_retorna_error()
    {
        var handler = BuildHandler(null, out var repo);

        var result = await handler.Handle(
            new DistributePurchaseCostCommand(Guid.NewGuid(), "Freight", 10m, new List<Guid> { Guid.NewGuid() }),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("no encontrada");
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Linea_incluida_que_no_pertenece_a_la_compra_retorna_error_de_validacion()
    {
        var inv = CreateDraftWithLines((1m, 100m));
        var handler = BuildHandler(inv, out var repo);

        var result = await handler.Handle(
            new DistributePurchaseCostCommand(inv.Id, "Freight", 10m, new List<Guid> { Guid.NewGuid() }),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task No_toca_lineas_excluidas()
    {
        var inv = CreateDraftWithLines((1m, 100m), (1m, 100m));
        var handler = BuildHandler(inv, out _);

        var result = await handler.Handle(
            new DistributePurchaseCostCommand(inv.Id, "Freight", 10m, new List<Guid> { inv.Lines[0].Id }),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        inv.Lines[0].FreightAllocated.Should().Be(10m);
        inv.Lines[1].FreightAllocated.Should().Be(0m);
    }
}
