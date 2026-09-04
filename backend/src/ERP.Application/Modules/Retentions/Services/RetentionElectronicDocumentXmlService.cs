using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Application.Modules.ElectronicDocuments.XmlBuilders;

namespace ERP.Application.Modules.Retentions.Services;

/// <summary>
/// RETENTIONS-ELECTRONIC-WIRING-03E — punto de entrada único y explícito para generar el XML
/// <c>comprobanteRetencion</c> de una retención ya <c>Issued</c>: orquesta
/// <see cref="IRetentionElectronicDocumentDataProvider"/> (03A) → <see cref="IRetentionXmlBuilder"/>
/// (03B) en dos pasos, sin lógica propia de negocio.
///
/// Deliberadamente NO implementa ningún contrato genérico de <c>ElectronicDocuments</c>
/// (<c>IElectronicDocumentXmlBuilder</c>/su resolver siguen fijos a <c>ElectronicDocumentData</c>,
/// la forma comercial de Factura/Nota de Crédito) — ver la decisión de wiring documentada en el
/// ADR de esta fase. Este servicio es el pipeline paralelo, pequeño y explícito para Retención:
/// no firma, no envía al SRI, no persiste el XML como autorizado (eso pertenece a una fase
/// posterior de autorización) — solo produce el <see cref="ElectronicDocumentXml"/> en memoria.
/// </summary>
public interface IRetentionElectronicDocumentXmlService
{
    Task<Result<ElectronicDocumentXml>> GenerateXmlAsync(
        ElectronicDocumentSourceReference reference,
        CancellationToken ct = default
    );
}

public sealed class RetentionElectronicDocumentXmlService : IRetentionElectronicDocumentXmlService
{
    private readonly IRetentionElectronicDocumentDataProvider _dataProvider;
    private readonly IRetentionXmlBuilder _xmlBuilder;

    public RetentionElectronicDocumentXmlService(
        IRetentionElectronicDocumentDataProvider dataProvider,
        IRetentionXmlBuilder xmlBuilder
    )
    {
        _dataProvider = dataProvider;
        _xmlBuilder = xmlBuilder;
    }

    public async Task<Result<ElectronicDocumentXml>> GenerateXmlAsync(
        ElectronicDocumentSourceReference reference,
        CancellationToken ct = default
    )
    {
        var dataResult = await _dataProvider.GetDataAsync(reference, ct);
        if (!dataResult.IsSuccess)
            return Result<ElectronicDocumentXml>.Failure(
                dataResult.Error ?? "No se pudo construir el modelo electrónico de la retención.",
                dataResult.Code
            );

        return _xmlBuilder.Build(dataResult.Value!);
    }
}
