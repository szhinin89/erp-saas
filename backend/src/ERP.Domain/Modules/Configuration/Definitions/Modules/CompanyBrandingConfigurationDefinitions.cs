using System.Text.RegularExpressions;
using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Enums;

namespace ERP.Domain.Configuration.Definitions.Modules;

/// <summary>
/// Definitions para OrgSettingKeys.CompanyBranding (CONFIG-FOUNDATION-P1-02). Company es owner;
/// RIDE/PDF/reportes son consumidores — ver ICompanyBrandingResolver.
/// </summary>
public static partial class CompanyBrandingConfigurationDefinitions
{
    private const int MaxSloganLength = 200;
    private const int MaxFooterTextLength = 500;

    [GeneratedRegex("^#([0-9A-Fa-f]{3}|[0-9A-Fa-f]{6})$")]
    private static partial Regex HexColorRegex();

    public static IEnumerable<ConfigurationDefinition> All()
    {
        yield return new ConfigurationDefinition
        {
            Key = OrgSettingKeys.CompanyBranding.PrimaryColor,
            Module = "CompanyBranding",
            DataType = ConfigurationDataType.ColorHex,
            AllowedScopes = [OrgScope.Company],
            DefaultScope = OrgScope.Company,
            FallbackStrategy = ConfigurationFallbackStrategy.VisualSafeDefault,
            RequiresAudit = false,
            Validator = value => HexColorRegex().IsMatch(value!),
            DeveloperNotes = "null/blank permitido (sin color configurado). Corrupto en lectura → null + warning, nunca color inválido en PDF.",
        };

        yield return new ConfigurationDefinition
        {
            Key = OrgSettingKeys.CompanyBranding.SecondaryColor,
            Module = "CompanyBranding",
            DataType = ConfigurationDataType.ColorHex,
            AllowedScopes = [OrgScope.Company],
            DefaultScope = OrgScope.Company,
            FallbackStrategy = ConfigurationFallbackStrategy.VisualSafeDefault,
            RequiresAudit = false,
            Validator = value => HexColorRegex().IsMatch(value!),
        };

        yield return new ConfigurationDefinition
        {
            Key = OrgSettingKeys.CompanyBranding.Slogan,
            Module = "CompanyBranding",
            DataType = ConfigurationDataType.String,
            AllowedScopes = [OrgScope.Company],
            DefaultScope = OrgScope.Company,
            FallbackStrategy = ConfigurationFallbackStrategy.VisualSafeDefault,
            RequiresAudit = false,
            Validator = value => value!.Length <= MaxSloganLength,
        };

        yield return new ConfigurationDefinition
        {
            Key = OrgSettingKeys.CompanyBranding.DocumentFooterText,
            Module = "CompanyBranding",
            DataType = ConfigurationDataType.String,
            AllowedScopes = [OrgScope.Company],
            DefaultScope = OrgScope.Company,
            FallbackStrategy = ConfigurationFallbackStrategy.VisualSafeDefault,
            // CONFIG-FOUNDATION-P2-01: decisión explícita — a diferencia de los colores/slogan
            // (puramente visuales), este texto se imprime en RIDE/PDF y otros documentos
            // compatibles; un cambio aquí altera contenido documental, no solo apariencia.
            RequiresAudit = true,
            Validator = value => value!.Length <= MaxFooterTextLength,
        };
    }
}
