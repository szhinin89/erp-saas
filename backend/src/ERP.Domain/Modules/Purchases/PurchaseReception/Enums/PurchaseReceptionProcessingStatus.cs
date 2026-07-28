namespace ERP.Domain.Modules.Purchases.PurchaseReception.Enums;

/// <summary>
/// Resultado de interpretar el contenido de un <see cref="Entities.PurchaseReceptionDocument"/> —
/// eje independiente de <see cref="PurchaseReceptionDocumentStatus"/> (ciclo de vida fiscal). Un
/// comprobante puede estar <see cref="PurchaseReceptionDocumentStatus.Verified"/> (autorizado por
/// el SRI, XML conservado) y a la vez tener su detalle en <see cref="Failed"/> — la validez fiscal
/// del comprobante no depende de si pudimos interpretar su detalle.
/// </summary>
public enum PurchaseReceptionProcessingStatus
{
    /// <summary>Documento aún no descargado/procesado (estado Imported).</summary>
    Pending = 0,

    /// <summary>Todas las líneas detectadas en el XML se interpretaron sin problemas.</summary>
    Processed = 1,

    /// <summary>Al menos una línea se interpretó correctamente, pero una o más fallaron.</summary>
    ProcessedWithWarnings = 2,

    /// <summary>Ninguna línea pudo interpretarse (o la cabecera/detalle del XML es ilegible).</summary>
    Failed = 3,
}
