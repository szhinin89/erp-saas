using ERP.Domain.Modules.Purchases.PurchaseReception.Models;

namespace ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;

/// <summary>
/// Obtiene el XML autorizado del SRI para una clave de acceso. No conoce EF Core, HTTP concreto
/// ni persistencia — la implementación (Infrastructure) reutiliza el cliente SOAP de autorización
/// ya construido para ElectronicDocuments, nunca una segunda integración SRI.
/// </summary>
public interface ISriReceptionXmlProvider
{
    Task<SriReceptionXmlQueryResult> GetAuthorizedXmlAsync(
        Guid tenantId,
        Guid companyId,
        string accessKey,
        CancellationToken cancellationToken = default
    );
}
