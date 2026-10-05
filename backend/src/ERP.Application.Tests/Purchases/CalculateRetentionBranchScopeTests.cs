using ERP.Application.Common;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Application.Modules.Retentions.Services;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Purchases;

/// <summary>
/// <see cref="CalculateRetentionHandler"/> (GET /purchases/{id}/retention-preview).
/// ZH-PURCHASES-RETENTION-OWNERSHIP-01: valida la sucursal igual que GetPurchaseByIdHandler.
/// ZH-PURCHASE-RETENTION-CONFIRM-01: opera sobre compras en BORRADOR (la retención se define antes de
/// confirmar) y evalúa la elegibilidad con el mismo <see cref="IRetentionEligibilityService"/> que
/// revalida la emisión — incluida la condición de agente de retención de la empresa, que la vista
/// previa anterior ignoraba.
/// </summary>
public sealed class CalculateRetentionBranchScopeTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static PurchaseInvoice Draft(Guid branchId)
    {
        var invoice = PurchaseInvoice.CreateDraft(
            TenantId,
            CompanyId,
            branchId,
            Guid.NewGuid(),
            "Proveedor",
            "1790012345001",
            "01",
            "001-001-000000001",
            new DateOnly(2026, 9, 1),
            UserId,
            Guid.NewGuid(),
            "Contado",
            1,
            0
        );
        var line = PurchaseInvoiceDetail.Create(
            invoice.Id,
            TenantId,
            "Producto",
            1m,
            100m,
            "4",
            "UNIT"
        );
        line.ApplyTaxes("4", 15m, "IVA 15%", null, 0m, null);
        invoice.ReplaceLines(new[] { line }, UserId);
        return invoice;
    }

    private static RetentionEligibilityResult Eligibility(
        bool canRetainVat,
        params string[] reasons
    ) =>
        new(
            CanRetainVat: canRetainVat,
            CanRetainIncome: false,
            IsSupplierExempt: false,
            HasRetainableBase: true,
            MissingRetentionCode: false,
            IsSupplierRequiredToKeepAccounting: false,
            Candidates: canRetainVat
                ? new[]
                {
                    new RetentionEligibilityCandidate("IVA", "725", "Retención IVA 30%", 30m),
                }
                : Array.Empty<RetentionEligibilityCandidate>(),
            Reasons: reasons
        );

    private static async Task<(
        Result<RetentionPreviewDto> Result,
        Mock<IRetentionEligibilityService> Eligibility
    )> Preview(
        PurchaseInvoice? invoice,
        Guid invoiceId,
        RetentionEligibilityResult? eligibility = null
    )
    {
        var repo = new Mock<IPurchaseInvoiceRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, invoiceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);
        var service = new Mock<IRetentionEligibilityService>();
        service
            .Setup(s =>
                s.EvaluateAsync(
                    TenantId,
                    CompanyId,
                    It.IsAny<Guid>(),
                    It.IsAny<decimal>(),
                    It.IsAny<decimal>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(eligibility ?? Eligibility(true));
        var handler = new CalculateRetentionHandler(
            repo.Object,
            service.Object,
            Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
            Mock.Of<ICurrentCompany>(c => c.CompanyId == CompanyId),
            Mock.Of<ICurrentBranch>(b => b.BranchId == BranchId)
        );
        return (
            await handler.Handle(new CalculateRetentionQuery(invoiceId), CancellationToken.None),
            service
        );
    }

    [Fact]
    public async Task Compra_de_otra_sucursal_responde_el_mismo_NotFound_que_una_inexistente()
    {
        var foreign = Draft(Guid.NewGuid());

        var (crossBranch, eligibility) = await Preview(foreign, foreign.Id);
        var (missing, _) = await Preview(null, Guid.NewGuid());

        crossBranch.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        (crossBranch.Code, crossBranch.Error).Should().Be((missing.Code, missing.Error));
        eligibility.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Borrador_elegible_propone_las_lineas_con_la_base_del_documento_y_los_montos_calculados()
    {
        var own = Draft(BranchId);

        var (result, eligibility) = await Preview(own, own.Id);

        result.IsSuccess.Should().BeTrue(result.Error);
        var line = result.Value!.Lines.Should().ContainSingle().Which;
        line.TaxType.Should().Be("IVA");
        line.RetentionCode.Should().Be("725");
        line.TaxableBase.Should().Be(15m, "la base de IVA es el IVA total de la compra");
        line.AmountRetained.Should().Be(4.5m);
        result.Value.TotalRetained.Should().Be(4.5m);
        result.Value.SkipReason.Should().BeNull();
        // Mismas bases que usa la emisión (PurchaseRetentionSource): IVA total y suma de bases imponibles.
        eligibility.Verify(
            s =>
                s.EvaluateAsync(
                    TenantId,
                    CompanyId,
                    own.SupplierId,
                    15m,
                    100m,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task Empresa_no_agente_de_retencion_no_propone_lineas_y_explica_el_motivo()
    {
        var own = Draft(BranchId);
        const string reason =
            "La empresa no está configurada como agente de retención de IVA (Company.WithholdsVat=false).";

        var (result, _) = await Preview(own, own.Id, Eligibility(false, reason));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Lines.Should().BeEmpty();
        result.Value.TotalRetained.Should().Be(0m);
        result.Value.SkipReason.Should().Be(reason);
    }

    [Fact]
    public async Task Compra_confirmada_se_rechaza_porque_la_retencion_se_define_antes_de_confirmar()
    {
        var confirmed = Draft(BranchId);
        confirmed.Confirm(UserId);

        var (result, eligibility) = await Preview(confirmed, confirmed.Id);

        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        result
            .Error.Should()
            .Be(
                "La retención se define antes de confirmar: solo se calcula sobre compras en borrador."
            );
        eligibility.VerifyNoOtherCalls();
    }
}
