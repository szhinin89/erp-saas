namespace ERP.Domain.Configuration.Enums;

/// <summary>
/// COMPANY-PRECISION-POLICY-SSOT-01: perfil de precisión operativa elegido por la empresa.
/// Standard/HighPrecision fijan todos los campos a valores predefinidos (ver
/// <see cref="ERP.Domain.Configuration.Entities.CompanyPrecisionPolicy"/>); Custom permite
/// que la empresa configure cada campo dentro de su rango permitido.
/// Nunca controla precisión fiscal/contable — eso es <see cref="ERP.Domain.Common.FiscalPrecision"/>.
/// </summary>
public enum PrecisionProfileType
{
    StandardCommercial = 1,
    HighPrecision = 2,
    Custom = 3,
}
