using ERP.Application.Modules.Companies;
using ERP.Application.Modules.Companies.UseCases.PrecisionPolicy;
using Moq;

namespace ERP.Application.Tests.TestSupport;

/// <summary>
/// COMPANY-PRECISION-POLICY-SSOT-01: doble de prueba compartido para
/// <see cref="ICompanyPrecisionPolicyProvider"/>. El default de <see cref="SettlementToleranceAmount"/>
/// (0.02m) preserva el comportamiento histórico de <c>SalesSettlementPolicy.Tolerance</c> para no
/// alterar el resultado de tests de Authorize preexistentes que no son sobre precisión.
/// </summary>
public static class PrecisionPolicyTestDouble
{
    public const decimal DefaultSettlementTolerance = 0.02m;

    public static EffectivePrecisionPolicyDto DefaultDto(
        decimal settlementTolerance = DefaultSettlementTolerance
    ) =>
        new(
            ProfileType: "StandardCommercial",
            SalesUnitPriceDecimals: 2,
            PurchaseUnitPriceDecimals: 4,
            QuantityDecimals: 4,
            PercentageDecimals: 2,
            UnitCostDecimals: 6,
            AverageCostDecimals: 6,
            ConversionFactorDecimals: 6,
            SettlementToleranceAmount: settlementTolerance,
            IsLocked: false,
            LockedAt: null,
            LockedReason: null,
            MoneyDecimals: 2,
            TaxDecimals: 2,
            AccountingDecimals: 2,
            FiscalPercentageDecimals: ERP.Domain.Common.FiscalPrecision.Percentage
        );

    public static ICompanyPrecisionPolicyProvider Mock(
        decimal settlementTolerance = DefaultSettlementTolerance
    )
    {
        var dto = DefaultDto(settlementTolerance);
        var mock = new Mock<ICompanyPrecisionPolicyProvider>();
        mock.Setup(p => p.GetEffectiveAsync(It.IsAny<CancellationToken>())).ReturnsAsync(dto);
        return mock.Object;
    }
}
