namespace ERP.Application.Codes;

/// <summary>
/// Building Block transversal del ERP: genera códigos QR en PNG a partir de un contenido
/// arbitrario. No conoce Ride, comprobantes electrónicos ni ningún otro dominio consumidor —
/// cualquier módulo (Inventory, Assets, POS, Logistics, RRHH, etc.) puede depender de esta
/// abstracción directamente.
///
/// Decisión de diseño (Fase 11, auditoría de cierre): este contrato permanece deliberadamente
/// específico de QR — no se generaliza a un <c>ICodeGenerator</c>/<c>ISymbologyGenerator</c> con
/// un enum de simbología. QR/Data Matrix/Aztec (códigos 2D) comparten razonablemente la forma
/// "contenido + escala → imagen", pero los códigos de barras 1D (Code128, EAN13, UPC, Code39)
/// tienen parámetros propios sin equivalente aquí (ancho de barra, alto, dígito verificador,
/// texto legible opcional) — forzarlos por este mismo contrato dejaría campos sin sentido para
/// unos u otros (violación de ISP) o exigiría un DTO con campos mutuamente excluyentes según el
/// formato (mala señal de diseño). Cuando se necesite una simbología nueva, se agrega una
/// interfaz hermana (p. ej. <c>IBarcodeGenerator</c> con su propio request/result) en este mismo
/// namespace — aditivo, sin tocar este contrato ni a sus consumidores (OCP). No se crea esa
/// interfaz hoy sin un consumidor real: sería código muerto.
/// </summary>
public interface IQrCodeGenerator
{
    QrGenerationResult Generate(QrGenerationRequest request);
}
