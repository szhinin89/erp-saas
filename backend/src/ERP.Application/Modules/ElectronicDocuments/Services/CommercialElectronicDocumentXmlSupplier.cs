using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.AdditionalInfo;
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
/// comerciales cambian.
///
/// ZH-SRI-ANEXO26-PROVIDER-RUC-01 (ADR-038 D6) — orquestador de ensamblado fiscal de Factura/NC:
/// entre el provider y el builder compone la información adicional con
/// <see cref="IElectronicDocumentAdditionalInfoComposer"/> (campos propios del documento + campos
/// normativos como el RUC Proveedor). Si la composición falla, no hay XML.
/// </summary>
public sealed class CommercialElectronicDocumentXmlSupplier : IElectronicDocumentXmlSupplier
{
    private readonly IElectronicDocumentDataProvider _dataProvider;
    private readonly IElectronicDocumentXmlBuilder _xmlBuilder;
    private readonly IElectronicDocumentAdditionalInfoComposer _additionalInfoComposer;

    public CommercialElectronicDocumentXmlSupplier(
        ElectronicDocumentType documentType,
        IElectronicDocumentDataProvider dataProvider,
        IElectronicDocumentXmlBuilder xmlBuilder,
        IElectronicDocumentAdditionalInfoComposer additionalInfoComposer
    )
    {
        DocumentType = documentType;
        _dataProvider = dataProvider;
        _xmlBuilder = xmlBuilder;
        _additionalInfoComposer = additionalInfoComposer;
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

        var data = dataResult.Value!;
        var additionalInfo = await _additionalInfoComposer.ComposeAsync(
            new AdditionalInfoCompositionContext(
                DocumentType,
                reference.TenantId,
                reference.CompanyId,
                data.Emission.IssueDate,
                data.Issuer
            ),
            data.AdditionalInfo,
            cancellationToken
        );
        if (!additionalInfo.IsSuccess)
            return Result<ElectronicDocumentXml>.ValidationFailure(
                additionalInfo.Error ?? "No se pudo componer la información adicional del comprobante.",
                additionalInfo.Code
            );

        return _xmlBuilder.Build(data with { AdditionalInfo = additionalInfo.Value });
    }
}
