using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;

namespace ERP.Application.Modules.ElectronicDocuments.Services;

/// <summary>
/// Firma electrónicamente un <see cref="ElectronicDocumentXml"/> ya validado contra XSD,
/// usando el único motor de firma del ERP (<c>IElectronicDocumentSigner</c> / XAdES-BES) y la
/// configuración SRI de la empresa (<c>SriSettings</c> — única fuente de verdad; nunca
/// appsettings, constantes ni parámetros mágicos).
///
/// Antes de firmar valida que exista configuración SRI, certificado registrado, contraseña
/// disponible y el archivo del certificado accesible — si falta cualquiera, no intenta firmar
/// y devuelve un <see cref="Result{T}"/> de fallo explícito, nunca lanza una excepción ni
/// inventa un valor.
/// </summary>
public interface IElectronicDocumentSigningService
{
    Task<Result<SignedElectronicDocumentXml>> SignAsync(
        Guid tenantId, Guid companyId, ElectronicDocumentXml xml, CancellationToken ct = default);
}
