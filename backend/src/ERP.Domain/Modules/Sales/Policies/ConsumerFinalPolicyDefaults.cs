namespace ERP.Domain.Modules.Sales.Policies;

/// <summary>
/// Defaults de <c>ConsumerFinalMaxAmount</c> por régimen tributario (sri_tax_regime.code),
/// usados solo cuando la empresa no configuró un valor manual (ver
/// <see cref="ERP.Domain.Modules.Sales.Interfaces.ISalesFiscalPolicyResolver"/>). Único punto
/// de estos valores en todo el sistema — nunca hardcodear 50/200 en otra capa (Application,
/// API, frontend).
/// </summary>
public static class ConsumerFinalPolicyDefaults
{
    public const decimal GeneralRegimeMaxAmount = 50.00m;
    public const decimal RimpeEmprendedorMaxAmount = 50.00m;
    public const decimal RimpeNegocioPopularMaxAmount = 200.00m;

    /// <summary>Usado cuando el régimen es null o no reconocido — nunca falla, siempre segura.</summary>
    public const decimal FallbackMaxAmount = 50.00m;

    // Códigos sri_tax_regime (ver SriTaxRegimeConfiguration): 01 General, 02 RIMPE
    // Microempresas ("RIMPE Emprendedor"), 03 RIMPE Negocio Popular, 04 Contribuyente Especial.
    private const string RegimeGeneral = "01";
    private const string RegimeRimpeEmprendedor = "02";
    private const string RegimeRimpeNegocioPopular = "03";

    /// <summary>
    /// Resuelve el default por régimen. <c>IsKnownRegime</c> distingue "default de régimen" de
    /// "fallback" (régimen null, no configurado en la empresa, o código no mapeado como
    /// Contribuyente Especial) — ambos casos usan <see cref="FallbackMaxAmount"/>.
    /// </summary>
    public static (decimal Amount, bool IsKnownRegime) ResolveDefault(string? taxRegimeCode)
    {
        var code = taxRegimeCode?.Trim().ToUpperInvariant();
        return code switch
        {
            RegimeGeneral => (GeneralRegimeMaxAmount, true),
            RegimeRimpeEmprendedor => (RimpeEmprendedorMaxAmount, true),
            RegimeRimpeNegocioPopular => (RimpeNegocioPopularMaxAmount, true),
            _ => (FallbackMaxAmount, false),
        };
    }
}
