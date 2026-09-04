using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.XmlBuilders;
using ERP.Domain.Modules.ElectronicDocuments.Enums;

namespace ERP.Application.Modules.ElectronicDocuments.Services;

/// <summary>
/// RETENTIONS-SRI-AUTHORIZATION-WIRING-04D — reproduce exactamente el camino comercial actual
/// (Provider → <see cref="ElectronicDocumentData"/> → Builder → <see cref="ElectronicDocumentXml"/>)
/// detrás del contrato <see cref="IElectronicDocumentXmlSupplier"/>. No se registra en DI por
/// tipo — <see cref="ElectronicDocumentXmlSupplierResolver"/> lo instancia al vuelo como
/// fallback, ya parametrizado con el <see cref="IElectronicDocumentDataProvider"/>/
/// <see cref="IElectronicDocumentXmlBuilder"/> ya resueltos para ese tipo (RETENTIONS-SRI-AUTHORIZATION-WIRING-DESIGN-04B,
/// sección F). Ni <c>InvoiceXmlBuilder</c>, ni <c>CreditNoteXmlBuilder</c>, ni sus providers
/// comerciales cambian — este supplier solo envuelve las mismas dos llamadas que
/// <c>ElectronicDocumentIssuer.RunPipelineAsync</c> hacía directamente antes de esta fase.
/// </summary>
public sealed class CommercialElectronicDocumentXmlSupplier : IElectronicDocumentXmlSupplier
{
    private readonly IElectronicDocumentDataProvider _dataProvider;
    private readonly IElectronicDocumentXmlBuilder _xmlBuilder;

    public CommercialElectronicDocumentXmlSupplier(
        ElectronicDocumentType documentType,
        IElectronicDocumentDataProvider dataProvider,
        IElectronicDocumentXmlBuilder xmlBuilder
    )
    {
        DocumentType = documentType;
        _dataProvider = dataProvider;
        _xmlBuilder = xmlBuilder;
    }

    public ElectronicDocumentType DocumentType { get; }

    public async Task<Result<ElectronicDocumentXml>> BuildXmlAsync(
        ElectronicDocumentSourceReference reference,
        CancellationToken cancellationToken = default
    )
    {
        var dataResult = await _dataProvider.GetDataAsync(reference, cancellationToken);
        if (!dataResult.IsSuccess)
            return Result<ElectronicDocumentXml>.Failure(
                dataResult.Error ?? "No se pudo obtener el modelo común del documento de origen.",
                dataResult.Code
            );

        return _xmlBuilder.Build(dataResult.Value!);
    }
}
