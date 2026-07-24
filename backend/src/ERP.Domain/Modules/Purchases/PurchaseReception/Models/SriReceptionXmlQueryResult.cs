namespace ERP.Domain.Modules.Purchases.PurchaseReception.Models;

/// <summary>
/// Resultado de consultar el XML autorizado de un comprobante en el SRI por clave de acceso.
/// Espejo simplificado (solo primitivos, sin tipos de Application/Infrastructure) de lo que
/// <c>ISriAuthorizationClient</c> ya expone — el Domain nunca conoce ese contrato ni SOAP/HTTP.
/// </summary>
public sealed record SriReceptionXmlQueryResult(
    bool Authorized,
    string? AuthorizationNumber,
    DateTime? AuthorizationDate,
    string? XmlContent,
    string? ErrorMessage);
