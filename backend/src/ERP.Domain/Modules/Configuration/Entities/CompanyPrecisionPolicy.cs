using ERP.Domain.Common;
using ERP.Domain.Configuration.Enums;

namespace ERP.Domain.Configuration.Entities;

/// <summary>
/// COMPANY-PRECISION-POLICY-SSOT-01: SSOT dinámico de precisión numérica OPERATIVA por empresa
/// (cuántos decimales captura/calcula la empresa para precios unitarios, cantidades, costos,
/// porcentajes y tolerancia de cuadre). Es la ÚNICA fuente de precisión operativa por empresa; sus
/// keys, rangos y perfiles predefinidos viven en <see cref="PrecisionPolicyDefinitions"/>.
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
    ) => Create(tenantId, companyId, PrecisionProfileType.StandardCommercial, PrecisionPolicyDefinitions.Standard, createdBy);

    public static CompanyPrecisionPolicy CreateHighPrecision(
        Guid tenantId,
        Guid companyId,
        Guid createdBy
    ) => Create(tenantId, companyId, PrecisionProfileType.HighPrecision, PrecisionPolicyDefinitions.HighPrecision, createdBy);

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
            PrecisionProfileType.StandardCommercial => PrecisionPolicyDefinitions.Standard,
            PrecisionProfileType.HighPrecision => PrecisionPolicyDefinitions.HighPrecision,
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
    /// <summary>Lanza <see cref="ArgumentOutOfRangeException"/> si algún campo excede su rango (ver
    /// <see cref="PrecisionPolicyDefinitions"/>, única fuente de rangos).</summary>
    public void EnsureWithinRange()
    {
        Check(SalesUnitPriceDecimals, PrecisionPolicyDefinitions.SalesUnitPriceDecimals);
        Check(PurchaseUnitPriceDecimals, PrecisionPolicyDefinitions.PurchaseUnitPriceDecimals);
        Check(QuantityDecimals, PrecisionPolicyDefinitions.QuantityDecimals);
        Check(PercentageDecimals, PrecisionPolicyDefinitions.PercentageDecimals);
        Check(UnitCostDecimals, PrecisionPolicyDefinitions.UnitCostDecimals);
        Check(AverageCostDecimals, PrecisionPolicyDefinitions.AverageCostDecimals);
        Check(ConversionFactorDecimals, PrecisionPolicyDefinitions.ConversionFactorDecimals);
        Check(SettlementToleranceAmount, PrecisionPolicyDefinitions.SettlementToleranceAmount);
    }

    private static void Check(decimal value, string key)
    {
        var def = PrecisionPolicyDefinitions.Get(key);
        if (value < def.Min || value > def.Max)
            throw new ArgumentOutOfRangeException(
                key,
                value,
                FormattableString.Invariant($"Debe estar entre {def.Min} y {def.Max}.")
            );
    }
}
