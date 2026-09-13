using ERP.Domain.Common;
using ERP.Domain.Configuration.Enums;

namespace ERP.Domain.Configuration.Entities;

/// <summary>
/// COMPANY-PRECISION-POLICY-SSOT-01: SSOT dinámico de precisión numérica OPERATIVA por empresa
/// (cuántos decimales captura/calcula la empresa para precios unitarios, cantidades, costos,
/// porcentajes y tolerancia de cuadre). Reemplaza como fuente de cálculo a
/// <c>OrgSettingKeys.Presentation</c> (namespace <c>presentation.decimal.*</c>, LEGACY — ver
/// <see cref="ERP.Domain.Configuration.Constants.OrgSettingKeys.Presentation"/>).
///
/// NUNCA reemplaza ni expone los valores fiscales/contables fijos del sistema — esos siguen
/// siendo <see cref="ERP.Domain.Common.FiscalPrecision"/> (Tax/Total/Percentage-fiscal/Accounting),
/// que no son configurables por empresa y no tienen fila en esta tabla.
///
/// Una vez que la empresa tiene operación real (documentos autorizados/confirmados, movimientos
/// de stock, pagos, asientos contabilizados), la policy se bloquea (<see cref="IsLocked"/>) y todo
/// intento de PUT posterior es rechazado — no existe flujo de "unlock" en este ticket.
/// </summary>
public sealed class CompanyPrecisionPolicy : AuditableEntity, ICompanyScopedEntity
{
    public const int SalesUnitPriceMin = 2;
    public const int SalesUnitPriceMax = 8;
    public const int PurchaseUnitPriceMin = 2;
    public const int PurchaseUnitPriceMax = 8;
    public const int QuantityMin = 0;
    public const int QuantityMax = 6;
    public const int PercentageMin = 2;
    public const int PercentageMax = 6;
    public const int UnitCostMin = 2;
    public const int UnitCostMax = 8;
    public const int AverageCostMin = 2;
    public const int AverageCostMax = 8;
    public const int ConversionFactorMin = 2;
    public const int ConversionFactorMax = 8;

    public const decimal SettlementToleranceMin = 0.00m;
    public const decimal SettlementToleranceMax = 0.02m;

    public Guid CompanyId { get; private set; }
    public PrecisionProfileType ProfileType { get; private set; }

    public short SalesUnitPriceDecimals { get; private set; }
    public short PurchaseUnitPriceDecimals { get; private set; }
    public short QuantityDecimals { get; private set; }
    public short PercentageDecimals { get; private set; }
    public short UnitCostDecimals { get; private set; }
    public short AverageCostDecimals { get; private set; }
    public short ConversionFactorDecimals { get; private set; }
    public decimal SettlementToleranceAmount { get; private set; }

    public bool IsLocked { get; private set; }
    public DateTime? LockedAt { get; private set; }
    public string? LockedReason { get; private set; }

    private CompanyPrecisionPolicy() { }

    public static CompanyPrecisionPolicy CreateStandardCommercial(
        Guid tenantId,
        Guid companyId,
        Guid createdBy
    ) => Create(tenantId, companyId, PrecisionProfileType.StandardCommercial, StandardCommercialDefaults(), createdBy);

    public static CompanyPrecisionPolicy CreateHighPrecision(
        Guid tenantId,
        Guid companyId,
        Guid createdBy
    ) => Create(tenantId, companyId, PrecisionProfileType.HighPrecision, HighPrecisionDefaults(), createdBy);

    public static CompanyPrecisionPolicy CreateCustom(
        Guid tenantId,
        Guid companyId,
        PrecisionPolicyValues values,
        Guid createdBy
    )
    {
        values.EnsureWithinRange();
        return Create(tenantId, companyId, PrecisionProfileType.Custom, values, createdBy);
    }

    private static CompanyPrecisionPolicy Create(
        Guid tenantId,
        Guid companyId,
        PrecisionProfileType profile,
        PrecisionPolicyValues values,
        Guid createdBy
    )
    {
        var e = new CompanyPrecisionPolicy
        {
            TenantId = tenantId,
            CompanyId = companyId,
            ProfileType = profile,
        };
        e.ApplyValues(values);
        e.SetCreated(createdBy);
        return e;
    }

    /// <summary>
    /// Aplica un nuevo perfil/valores. El llamador (Application) es responsable de rechazar el
    /// update ANTES de invocar esto si <see cref="IsLocked"/> es true o si detectó operación real
    /// (ver GetUpdateGuard) — este método de dominio no vuelve a evaluar esas reglas de
    /// infraestructura, solo aplica el nuevo estado y valida rangos.
    /// </summary>
    public void UpdateProfile(
        PrecisionProfileType profile,
        PrecisionPolicyValues customValues,
        Guid updatedBy
    )
    {
        var values = profile switch
        {
            PrecisionProfileType.StandardCommercial => StandardCommercialDefaults(),
            PrecisionProfileType.HighPrecision => HighPrecisionDefaults(),
            PrecisionProfileType.Custom => customValues,
            _ => throw new ArgumentOutOfRangeException(nameof(profile)),
        };
        values.EnsureWithinRange();
        ProfileType = profile;
        ApplyValues(values);
        SetUpdated(updatedBy);
    }

    /// <summary>
    /// Marca la policy como bloqueada porque se detectó operación real en la empresa. Idempotente:
    /// si ya estaba bloqueada, no reescribe LockedAt/LockedReason originales.
    /// </summary>
    public void Lock(string reason, Guid lockedBy)
    {
        if (IsLocked)
            return;

        IsLocked = true;
        LockedAt = DateTime.UtcNow;
        LockedReason = reason;
        SetUpdated(lockedBy);
    }

    private void ApplyValues(PrecisionPolicyValues v)
    {
        SalesUnitPriceDecimals = v.SalesUnitPriceDecimals;
        PurchaseUnitPriceDecimals = v.PurchaseUnitPriceDecimals;
        QuantityDecimals = v.QuantityDecimals;
        PercentageDecimals = v.PercentageDecimals;
        UnitCostDecimals = v.UnitCostDecimals;
        AverageCostDecimals = v.AverageCostDecimals;
        ConversionFactorDecimals = v.ConversionFactorDecimals;
        SettlementToleranceAmount = v.SettlementToleranceAmount;
    }

    public static PrecisionPolicyValues StandardCommercialDefaults() =>
        new(
            SalesUnitPriceDecimals: 2,
            PurchaseUnitPriceDecimals: 4,
            QuantityDecimals: 4,
            PercentageDecimals: 2,
            UnitCostDecimals: 6,
            AverageCostDecimals: 6,
            ConversionFactorDecimals: 6,
            SettlementToleranceAmount: 0.01m
        );

    public static PrecisionPolicyValues HighPrecisionDefaults() =>
        new(
            SalesUnitPriceDecimals: 4,
            PurchaseUnitPriceDecimals: 6,
            QuantityDecimals: 6,
            PercentageDecimals: 4,
            UnitCostDecimals: 6,
            AverageCostDecimals: 6,
            ConversionFactorDecimals: 8,
            SettlementToleranceAmount: 0.01m
        );
}

/// <summary>
/// Value object inmutable con los 7 campos de decimales + tolerancia de cuadre. Usado tanto para
/// los perfiles predefinidos como para el perfil Personalizado.
/// </summary>
public sealed record PrecisionPolicyValues(
    short SalesUnitPriceDecimals,
    short PurchaseUnitPriceDecimals,
    short QuantityDecimals,
    short PercentageDecimals,
    short UnitCostDecimals,
    short AverageCostDecimals,
    short ConversionFactorDecimals,
    decimal SettlementToleranceAmount
)
{
    /// <summary>Lanza <see cref="ArgumentOutOfRangeException"/> si algún campo excede su rango permitido.</summary>
    public void EnsureWithinRange()
    {
        Check(SalesUnitPriceDecimals, CompanyPrecisionPolicy.SalesUnitPriceMin, CompanyPrecisionPolicy.SalesUnitPriceMax, nameof(SalesUnitPriceDecimals));
        Check(PurchaseUnitPriceDecimals, CompanyPrecisionPolicy.PurchaseUnitPriceMin, CompanyPrecisionPolicy.PurchaseUnitPriceMax, nameof(PurchaseUnitPriceDecimals));
        Check(QuantityDecimals, CompanyPrecisionPolicy.QuantityMin, CompanyPrecisionPolicy.QuantityMax, nameof(QuantityDecimals));
        Check(PercentageDecimals, CompanyPrecisionPolicy.PercentageMin, CompanyPrecisionPolicy.PercentageMax, nameof(PercentageDecimals));
        Check(UnitCostDecimals, CompanyPrecisionPolicy.UnitCostMin, CompanyPrecisionPolicy.UnitCostMax, nameof(UnitCostDecimals));
        Check(AverageCostDecimals, CompanyPrecisionPolicy.AverageCostMin, CompanyPrecisionPolicy.AverageCostMax, nameof(AverageCostDecimals));
        Check(ConversionFactorDecimals, CompanyPrecisionPolicy.ConversionFactorMin, CompanyPrecisionPolicy.ConversionFactorMax, nameof(ConversionFactorDecimals));

        if (
            SettlementToleranceAmount < CompanyPrecisionPolicy.SettlementToleranceMin
            || SettlementToleranceAmount > CompanyPrecisionPolicy.SettlementToleranceMax
        )
            throw new ArgumentOutOfRangeException(
                nameof(SettlementToleranceAmount),
                SettlementToleranceAmount,
                $"Debe estar entre {CompanyPrecisionPolicy.SettlementToleranceMin} y {CompanyPrecisionPolicy.SettlementToleranceMax}."
            );
    }

    private static void Check(short value, int min, int max, string field)
    {
        if (value < min || value > max)
            throw new ArgumentOutOfRangeException(field, value, $"Debe estar entre {min} y {max}.");
    }
}
