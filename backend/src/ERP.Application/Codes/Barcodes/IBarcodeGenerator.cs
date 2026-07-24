namespace ERP.Application.Codes.Barcodes;

/// <summary>
/// Building Block transversal del ERP: genera códigos de barra 1D en PNG a partir de un
/// contenido arbitrario. Hermano de <c>ERP.Application.Codes.IQrCodeGenerator</c> (2D) — mismo
/// namespace padre <c>Codes</c>, contrato independiente, sin modificar ni depender del contrato
/// QR existente. Cualquier módulo (Inventario, Activos, POS, Logística, etc.) puede depender de
/// esta abstracción directamente para cualquier simbología ya soportada.
/// </summary>
public interface IBarcodeGenerator
{
    BarcodeGenerationResult Generate(BarcodeGenerationRequest request);
}
