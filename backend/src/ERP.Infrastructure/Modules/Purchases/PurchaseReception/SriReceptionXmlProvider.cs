using ERP.Application.Common.Interfaces.SRI;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Models;

namespace ERP.Infrastructure.Modules.Purchases.PurchaseReception;

/// <summary>
/// Adaptador delgado sobre <see cref="ISriAuthorizationClient"/> — el mismo cliente SOAP de
/// autorización ya construido para ElectronicDocuments. No implementa SOAP, polling, WSDL ni
/// manejo de certificados propio: solo resuelve la URL configurada de la empresa
/// (<see cref="ISriSettingsRepository"/>) y traduce <see cref="SriAuthorizationResult"/> al
/// modelo de Domain de PurchaseReception.
/// </summary>
public sealed class SriReceptionXmlProvider : ISriReceptionXmlProvider
{
    private readonly ISriAuthorizationClient _authorizationClient;
    private readonly ISriSettingsRepository _sriSettingsRepo;

    public SriReceptionXmlProvider(
        ISriAuthorizationClient authorizationClient,
        ISriSettingsRepository sriSettingsRepo
    )
    {
        _authorizationClient = authorizationClient;
        _sriSettingsRepo = sriSettingsRepo;
    }

    public async Task<SriReceptionXmlQueryResult> GetAuthorizedXmlAsync(
        Guid tenantId,
        Guid companyId,
        string accessKey,
        CancellationToken cancellationToken = default
    )
    {
        var settings = await _sriSettingsRepo.GetByCompanyIdAsync(companyId, cancellationToken);
        if (settings is null)
        {
            return new SriReceptionXmlQueryResult(
                Authorized: false,
                AuthorizationNumber: null,
                AuthorizationDate: null,
                XmlContent: null,
                ErrorMessage: "La empresa no tiene configuración SRI (WSDL) registrada."
            );
        }

        var result = await _authorizationClient.CheckAsync(
            accessKey,
            settings.WsdlUrl,
            cancellationToken
        );

        return new SriReceptionXmlQueryResult(
            result.Authorized,
            result.AuthorizationNumber,
            result.AuthorizationDate,
            result.DocumentXml,
            result.ErrorMessage
        );
    }
}
