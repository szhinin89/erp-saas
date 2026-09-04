using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.Retentions.Services;
using ERP.Domain.Modules.ElectronicDocuments.Enums;

namespace ERP.Application.Modules.ElectronicDocuments.Services;

/// <summary>
/// RETENTIONS-SRI-AUTHORIZATION-WIRING-04D — supplier explícito para Retención: delega
/// íntegramente en <see cref="IRetentionElectronicDocumentXmlService"/> (03E), que ya orquesta
/// <c>RetentionElectronicDocumentDataProvider</c> (03A) → <c>RetentionXmlBuilder</c> (03B) sin
/// pasar por <see cref="ElectronicDocumentData"/> en ningún momento. Nunca consulta
/// <c>RetentionDocument</c> ni ningún repositorio directamente — esa responsabilidad ya vive en
/// el servicio 03E, este supplier solo lo expone bajo el contrato genérico que
/// <see cref="ElectronicDocumentIssuer"/> consume.
/// </summary>
public sealed class RetentionElectronicDocumentXmlSupplier : IElectronicDocumentXmlSupplier
{
    private readonly IRetentionElectronicDocumentXmlService _retentionXmlService;

    public RetentionElectronicDocumentXmlSupplier(
        IRetentionElectronicDocumentXmlService retentionXmlService
    )
    {
        _retentionXmlService = retentionXmlService;
    }

    public ElectronicDocumentType DocumentType => ElectronicDocumentType.Retention;

    public Task<Result<ElectronicDocumentXml>> BuildXmlAsync(
        ElectronicDocumentSourceReference reference,
        CancellationToken cancellationToken = default
    ) => _retentionXmlService.GenerateXmlAsync(reference, cancellationToken);
}
