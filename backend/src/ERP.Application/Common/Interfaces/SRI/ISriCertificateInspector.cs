namespace ERP.Application.Common.Interfaces.SRI;

/// <summary>Resultado de inspeccionar un certificado P12 sin firmar nada — solo diagnóstico.</summary>
public sealed record SriCertificateInspectionResult(
    bool FileAccessible,
    bool PasswordCorrect,
    bool Loaded,
    DateTime? NotAfterUtc,
    string? Subject,
    string? Issuer,
    string? ErrorMessage);

/// <summary>
/// Inspecciona un certificado P12/PKCS#12 (existencia, contraseña, vigencia, subject) para
/// fines de diagnóstico — nunca firma, nunca escribe. Complementa a
/// <see cref="IElectronicDocumentSigner"/> (que sí firma) sin duplicar su lógica: ambos usan
/// el mismo mecanismo de carga de certificado, cada uno con su propia responsabilidad.
/// </summary>
public interface ISriCertificateInspector
{
    /// <summary>Inspecciona un certificado ya cargado en memoria (vía <see cref="IFileStorage"/>) — no depende del filesystem local.</summary>
    SriCertificateInspectionResult Inspect(byte[] p12Bytes, string p12Password);

    /// <summary>Conveniencia sobre un archivo físico — delega en <see cref="Inspect(byte[], string)"/>.</summary>
    SriCertificateInspectionResult Inspect(string p12FilePath, string p12Password);
}
