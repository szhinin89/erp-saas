using ERP.Domain.Modules.SriCatalogs.Enums;

namespace ERP.Domain.Modules.SriCatalogs.Entities;

/// <summary>
/// ZH-SRI-RETENTION-CATALOG-SSOT-01 (ADR-037 D4/D5) — representación oficial versionada de un concepto de
/// retención (<see cref="SriRetentionCode"/>). La identidad interna (<see cref="SriRetentionCode.Id"/>) y la
/// clave de negocio (<see cref="SriRetentionCode.Code"/>, p. ej. "725") NO son el código del XML: el
/// <c>codigoRetencion</c> oficial vive aquí (<see cref="XmlCode"/>, p. ej. "1" para IVA 30 % según la Tabla 20
/// de la Ficha Técnica v2.34).
///
/// Vigencia normativa por fecha de negocio del documento (ADR-034): <see cref="ValidFrom"/>/<see cref="ValidUntil"/>
/// inclusivos; null = sin límite conocido en ese extremo (mismo vocabulario que <see cref="SriVatRate"/>). Por
/// concepto no puede haber vigencias solapadas. Histórico inmutable (ADR-037 D9): solo se permite cerrar
/// <see cref="ValidUntil"/>; nunca se cambia porcentaje, código ni fuente de una versión existente.
/// No lleva IsEnabled: la habilitación operativa vive en el concepto (<see cref="SriRetentionCode.IsActive"/>).
/// </summary>
public class SriRetentionCodeVersion
{
    public const int XmlCodeMaxLen = 5;
    public const int AtsCodeMaxLen = 10;
    public const int RateRuleTextMaxLen = 300;

    public Guid Id { get; set; }
    public Guid RetentionCodeId { get; set; }

    public DateOnly? ValidFrom { get; set; }
    public DateOnly? ValidUntil { get; set; }

    /// <summary>
    /// Forma de la tarifa (ZH-SRI-RETENTION-INCOME-CATALOG-SSOT-01). <see cref="SriRetentionRateKind.Conditional"/>
    /// nunca lleva <see cref="Percentage"/> y no es resoluble automáticamente.
    /// </summary>
    public SriRetentionRateKind RateKind { get; set; } = SriRetentionRateKind.Fixed;

    /// <summary>Porcentaje oficial de la versión; null cuando la fuente no lo fija o no está verificado (no se exige coincidencia).</summary>
    public decimal? Percentage { get; set; }

    /// <summary>Texto literal de la regla oficial cuando la tarifa es condicional (p. ej. "12 o 14").</summary>
    public string? RateRuleText { get; set; }

    /// <summary><c>codigoRetencion</c> del XML; null = concepto sin representación oficial (no emitible, fail-closed).</summary>
    public string? XmlCode { get; set; }

    /// <summary>Código ATS; null mientras no exista fuente oficial que lo confirme (ADR-037 DR-1).</summary>
    public string? AtsCode { get; set; }

    public Guid NormativeSourceId { get; set; }
}
