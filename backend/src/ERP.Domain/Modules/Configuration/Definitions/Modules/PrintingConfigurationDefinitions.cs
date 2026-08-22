using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Enums;

namespace ERP.Domain.Configuration.Definitions.Modules;

/// <summary>
/// CONFIG-DYNAMIC-OPERATIONS-01: Definitions para OrgSettingKeys.Printing. Mode/Copies tienen
/// efecto real conectado (Fase B, ver printAgentClient.ts + SalesIssueModal.tsx). PaperWidth se
/// guarda y expone pero deliberadamente NO tiene efecto — ver doc comment en
/// OrgSettingKeys.Printing. El resto (logo/access key/cashier/cash drawer) es Fase C.
/// </summary>
public static class PrintingConfigurationDefinitions
{
    private static readonly string[] ReceiptModes = ["AskBeforePrint", "AlwaysPrint", "NeverAutoPrint"];
    private static readonly string[] PaperWidths = ["80mm", "58mm"];

    public static IEnumerable<ConfigurationDefinition> All()
    {
        yield return Enum(
            OrgSettingKeys.Printing.SalesReceiptMode,
            ReceiptModes,
            "AskBeforePrint"
        );
        yield return Int(OrgSettingKeys.Printing.SalesReceiptCopies, value => IsIntInRange(value, 1, 3), "1");
        yield return Enum(OrgSettingKeys.Printing.SalesReceiptPaperWidth, PaperWidths, "80mm") with
        {
            DeveloperNotes =
                "Deliberadamente sin resolver/consumir: el ancho de papel real ya se configura por "
                + "impresora en ZH Print Agent (PrinterInfo.PaperWidthMm, /admin local). Ver "
                + "OrgSettingKeys.Printing.",
        };
        yield return Bool(OrgSettingKeys.Printing.SalesReceiptIncludeLogo, "false");
        yield return Bool(OrgSettingKeys.Printing.SalesReceiptIncludeAccessKey, "true");
        yield return Bool(OrgSettingKeys.Printing.SalesReceiptIncludeCashier, "true");
        yield return Bool(OrgSettingKeys.Printing.SalesReceiptOpenCashDrawer, "false");
    }

    private static ConfigurationDefinition Bool(string key, string defaultValue) =>
        new()
        {
            Key = key,
            Module = "Printing",
            DataType = ConfigurationDataType.Bool,
            AllowedScopes = [OrgScope.Company],
            DefaultScope = OrgScope.Company,
            DefaultValue = defaultValue,
            FallbackStrategy = ConfigurationFallbackStrategy.SystemDefault,
            RequiresAudit = true,
            Validator = value => bool.TryParse(value, out _),
        };

    private static ConfigurationDefinition Int(
        string key,
        Func<string?, bool> validator,
        string defaultValue
    ) =>
        new()
        {
            Key = key,
            Module = "Printing",
            DataType = ConfigurationDataType.Int,
            AllowedScopes = [OrgScope.Company],
            DefaultScope = OrgScope.Company,
            DefaultValue = defaultValue,
            FallbackStrategy = ConfigurationFallbackStrategy.SystemDefault,
            RequiresAudit = true,
            Validator = validator,
        };

    private static ConfigurationDefinition Enum(string key, string[] allowedValues, string defaultValue) =>
        new()
        {
            Key = key,
            Module = "Printing",
            DataType = ConfigurationDataType.String,
            AllowedScopes = [OrgScope.Company],
            DefaultScope = OrgScope.Company,
            DefaultValue = defaultValue,
            FallbackStrategy = ConfigurationFallbackStrategy.SystemDefault,
            RequiresAudit = true,
            Validator = value => Array.IndexOf(allowedValues, value) >= 0,
        };

    private static bool IsIntInRange(string? value, int min, int max) =>
        int.TryParse(value, out var parsed) && parsed >= min && parsed <= max;
}
