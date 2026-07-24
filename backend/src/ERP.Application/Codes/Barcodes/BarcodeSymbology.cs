namespace ERP.Application.Codes.Barcodes;

/// <summary>
/// Simbologías de código de barras 1D soportadas por el Building Block Codes. Aditivo por
/// diseño (OCP): agregar una simbología nueva es agregar un valor aquí + una implementación
/// nueva de <see cref="IBarcodeGenerator"/> — nunca modifica las ya existentes ni el contrato
/// de <c>IQrCodeGenerator</c> (2D, building block hermano, sin relación con este).
/// </summary>
public enum BarcodeSymbology
{
    Code128,

    // Futuras (no implementadas todavía — se agregan como nuevo valor + nueva implementación,
    // nunca modificando Code128BarcodeGenerator): Code39, Ean13, Upc, Pdf417, DataMatrix.
}
