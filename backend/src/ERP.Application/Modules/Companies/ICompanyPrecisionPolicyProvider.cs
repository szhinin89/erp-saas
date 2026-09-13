using ERP.Application.Modules.Companies.UseCases.PrecisionPolicy;

namespace ERP.Application.Modules.Companies;

/// <summary>
/// COMPANY-PRECISION-POLICY-SSOT-01: resuelve la <see cref="EffectivePrecisionPolicyDto"/> de la
/// empresa ACTIVA del request (vía <c>ICurrentTenant</c>/<c>ICurrentCompany</c> — nunca por
/// parámetro), para que Application/consumidores reales (GetPurchaseItemContext,
/// ItemProfitability, Sales/Returns Authorize) dejen de leer el mecanismo legacy
/// <c>IDecimalConfigRepository</c>/org_settings Presentation.
///
/// Fail-closed: si no hay empresa operativa activa, lanza <c>CompanyScopeException.NoCompanyContext()</c>
/// — nunca devuelve un default silencioso.
///
/// Fallback: si la empresa activa no tiene fila en <c>company_precision_policy</c> (caso de una
/// empresa creada después del backfill de la migración), la crea de forma perezosa con el perfil
/// "Estándar comercial" y la persiste antes de devolverla.
/// </summary>
public interface ICompanyPrecisionPolicyProvider
{
    Task<EffectivePrecisionPolicyDto> GetEffectiveAsync(CancellationToken ct = default);
}
