using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ERP.Application.Common.Interfaces.SRI;

namespace ERP.Infrastructure.Services.Sri;

/// <summary>
/// Implementación de <see cref="ISriCertificateInspector"/> — misma API de carga
/// (<see cref="X509CertificateLoader"/>) que ya usa <see cref="XadesBesSigner"/> para firmar,
/// aquí solo para leer metadatos (vigencia, subject) sin firmar nada.
/// </summary>
public sealed class SriCertificateInspector : ISriCertificateInspector
{
    public SriCertificateInspectionResult Inspect(byte[] p12Bytes, string p12Password)
    {
        try
        {
            using var cert = X509CertificateLoader.LoadPkcs12(
                p12Bytes, p12Password, X509KeyStorageFlags.Exportable);

            return new SriCertificateInspectionResult(
                FileAccessible: true, PasswordCorrect: true, Loaded: true,
                NotAfterUtc: cert.NotAfter.ToUniversalTime(), Subject: cert.Subject, Issuer: cert.Issuer,
                ErrorMessage: null);
        }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException)
        {
            return new SriCertificateInspectionResult(
                FileAccessible: true, PasswordCorrect: false, Loaded: false,
                NotAfterUtc: null, Subject: null, Issuer: null, ErrorMessage: ex.Message);
        }
    }

    public SriCertificateInspectionResult Inspect(string p12FilePath, string p12Password)
    {
        if (!File.Exists(p12FilePath))
            return new SriCertificateInspectionResult(
                FileAccessible: false, PasswordCorrect: false, Loaded: false,
                NotAfterUtc: null, Subject: null, Issuer: null,
                ErrorMessage: "El archivo no existe en la ruta configurada.");

        try
        {
            return Inspect(File.ReadAllBytes(p12FilePath), p12Password);
        }
        catch (IOException ex)
        {
            return new SriCertificateInspectionResult(
                FileAccessible: false, PasswordCorrect: false, Loaded: false,
                NotAfterUtc: null, Subject: null, Issuer: null, ErrorMessage: ex.Message);
        }
    }
}
