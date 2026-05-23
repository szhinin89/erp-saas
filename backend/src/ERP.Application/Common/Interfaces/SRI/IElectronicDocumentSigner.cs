namespace ERP.Application.Common.Interfaces.SRI;

/// <summary>
/// Firma documentos XML con XAdES-BES usando un certificado P12/PKCS#12.
/// Abstrae XadesBesSigner para permitir pruebas unitarias y certificados en memoria
/// (en lugar de archivos físicos).
/// </summary>
public interface IElectronicDocumentSigner
{
    /// <summary>
    /// Firma el XML y devuelve los bytes UTF-8 del XML firmado.
    /// </summary>
    /// <param name="xmlUtf8">XML sin firmar.</param>
    /// <param name="p12FilePath">Ruta al archivo P12 con el certificado digital.</param>
    /// <param name="p12Password">Contraseña del archivo P12.</param>
    byte[] Sign(string xmlUtf8, string p12FilePath, string p12Password);
}
