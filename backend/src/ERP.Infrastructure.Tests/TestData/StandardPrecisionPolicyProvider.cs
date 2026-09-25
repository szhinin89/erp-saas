using ERP.Application.Modules.Companies;
using ERP.Application.Modules.Companies.UseCases.PrecisionPolicy;

namespace ERP.Infrastructure.Tests.TestData;

/// <summary>
/// ERP-PRECISION-OPERATIONAL-05B: proveedor fijo del perfil "Estándar comercial" para las pruebas de
/// integración que construyen a mano handlers que ahora resuelven <see cref="ICompanyPrecisionPolicyProvider"/>.
/// </summary>
public sealed class StandardPrecisionPolicyProvider : ICompanyPrecisionPolicyProvider
{
    public static readonly StandardPrecisionPolicyProvider Instance = new();

    public Task<EffectivePrecisionPolicyDto> GetEffectiveAsync(CancellationToken ct = default) =>
        Task.FromResult(
            new EffectivePrecisionPolicyDto(
                ProfileType: "StandardCommercial",
                SalesUnitPriceDecimals: 2,
                PurchaseUnitPriceDecimals: 4,
                QuantityDecimals: 4,
                PercentageDecimals: 2,
                UnitCostDecimals: 6,
                AverageCostDecimals: 6,
                ConversionFactorDecimals: 6,
                SettlementToleranceAmount: 0.01m,
                IsLocked: false,
                LockedAt: null,
                LockedReason: null,
                MoneyDecimals: 2,
                TaxDecimals: 2,
                AccountingDecimals: 2,
                FiscalPercentageDecimals: ERP.Domain.Common.FiscalPrecision.Percentage,
                WarehouseCapacityDecimals: ERP.Domain.Modules.Inventory.Entities.WarehousePrecision.Capacity,
                CreditInstallmentPercentageDecimals: ERP.Domain.Modules.Finance.Entities.CreditTermsPrecision.InstallmentPercentage,
                PackagingWeightDecimals: ERP.Domain.Modules.Items.Entities.ItemPrecision.PackagingWeight
            )
        );
}
