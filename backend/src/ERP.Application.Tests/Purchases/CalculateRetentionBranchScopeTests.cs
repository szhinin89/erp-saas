using ERP.Application.Common;
using ERP.Application.Modules.Purchases.Services;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Purchases;

/// <summary>
/// ZH-PURCHASES-RETENTION-OWNERSHIP-01 — <see cref="CalculateRetentionHandler"/> (GET
/// /purchases/{id}/retention-preview) valida la sucursal de la compra igual que
/// GetPurchaseByIdHandler: antes solo filtraba tenant+company y exponía el cálculo de retención
/// (proveedor, bases, códigos) de compras de otra sucursal de la misma empresa.
/// </summary>
public sealed class CalculateRetentionBranchScopeTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();

    private static PurchaseInvoice Draft(Guid branchId) =>
        PurchaseInvoice.CreateDraft(
            TenantId, CompanyId, branchId, Guid.NewGuid(), "Proveedor", "1790012345001", "01",
            "001-001-000000001", new DateOnly(2026, 9, 1), Guid.NewGuid(), Guid.NewGuid(), "Contado", 1, 0
        );

    private static async Task<Result<RetentionPreviewDto>> Preview(PurchaseInvoice? invoice, Guid invoiceId)
    {
        var repo = new Mock<IPurchaseInvoiceRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, invoiceId, It.IsAny<CancellationToken>())).ReturnsAsync(invoice);
        var handler = new CalculateRetentionHandler(
            repo.Object,
            Mock.Of<IBusinessPartnerRoleRepository>(),
            Mock.Of<ISupplierRetentionDefaultRepository>(),
            Mock.Of<IRetentionCodeResolver>(),
            Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
            Mock.Of<ICurrentBranch>(b => b.BranchId == BranchId)
        );
        return await handler.Handle(new CalculateRetentionQuery(invoiceId), CancellationToken.None);
    }

    [Fact]
    public async Task Compra_de_otra_sucursal_responde_el_mismo_NotFound_que_una_inexistente()
    {
        var foreign = Draft(Guid.NewGuid());

        var crossBranch = await Preview(foreign, foreign.Id);
        var missing = await Preview(null, Guid.NewGuid());

        crossBranch.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        (crossBranch.Code, crossBranch.Error).Should().Be((missing.Code, missing.Error));
    }

    [Fact]
    public async Task Compra_de_la_sucursal_actual_pasa_la_validacion_de_sucursal()
    {
        var own = Draft(BranchId);

        var result = await Preview(own, own.Id);

        // Llega a la regla siguiente (solo compras confirmadas), es decir, superó la de sucursal.
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        result.Error.Should().Be("Solo se pueden calcular retenciones de compras confirmadas.");
    }
}
