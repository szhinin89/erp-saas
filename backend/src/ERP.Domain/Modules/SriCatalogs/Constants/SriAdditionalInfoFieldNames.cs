namespace ERP.Domain.Modules.SriCatalogs.Constants;

/// <summary>
/// ZH-SRI-ANEXO26-PROVIDER-RUC-01 (ADR-038 D6/D7) — nombres normativos exactos de
/// <c>&lt;campoAdicional nombre="..."&gt;</c> exigidos por la Ficha Técnica SRI. Son literales fijados
/// por la norma (no configurables por tenant/empresa): se declaran una sola vez aquí y solo los usa el
/// contributor de <c>infoAdicional</c> que los emite. Solo incluye nombres con un consumidor real.
/// </summary>
public static class SriAdditionalInfoFieldNames
{
    /// <summary>
    /// Ficha Técnica de Comprobantes Electrónicos Offline v2.34, Anexo 26 (p. 135) — RUC del proveedor
    /// de sistemas / servicios de facturación electrónica (Res. NAC-DGERCGC26-00000027).
    /// </summary>
    public const string SystemProviderRuc = "RUC Proveedor";
}
