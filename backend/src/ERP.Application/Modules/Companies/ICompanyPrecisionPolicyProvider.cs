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
/// Sin fallback: si la empresa activa no tiene fila en <c>company_precision_policy</c> lanza
/// <see cref="CompanyPrecisionPolicyMissingException"/> — nunca crea ni inventa una política al leer.
/// La fila la crea el bootstrap de empresa (CompanyProvisioningService) y la migración de backfill.
/// </summary>
public interface ICompanyPrecisionPolicyProvider
{
    Task<EffectivePrecisionPolicyDto> GetEffectiveAsync(CancellationToken ct = default);
}

/// <summary>La empresa no tiene <c>CompanyPrecisionPolicy</c>: error de datos/aprovisionamiento, no un caso a "arreglar" en lectura.</summary>
public sealed class CompanyPrecisionPolicyMissingException : InvalidOperationException
{
    public CompanyPrecisionPolicyMissingException(Guid companyId)
        : base($"La empresa {companyId} no tiene configuración de precisión (company_precision_policy).") { }
}
