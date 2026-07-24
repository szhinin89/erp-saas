using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace ERP.Infrastructure.Tests.Services.Sri;

/// <summary>
/// Genera un certificado P12 autofirmado, solo para pruebas de firma XAdES-BES (Fase 9) — nunca
/// usado para emitir comprobantes reales, ni tiene ninguna relación con un certificado
/// acreditado ante el SRI. Se escribe a un archivo temporal porque <c>XadesBesSigner.Sign</c>
/// recibe una ruta de archivo, no bytes en memoria (contrato no modificado en esta fase).
/// </summary>
internal static class TestP12CertificateFactory
{
    public const string Password = "test-only-p12-password";

    public static string CreateTempP12File() =>
        CreateTempP12File(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));

    /// <summary>Certificado ya vencido (FIRMA-02, auditoría SRI) — vigencia terminó ayer.</summary>
    public static string CreateExpiredTempP12File() =>
        CreateTempP12File(DateTimeOffset.UtcNow.AddYears(-2), DateTimeOffset.UtcNow.AddDays(-1));

    /// <summary>Certificado que todavía no entra en vigencia (FIRMA-02, auditoría SRI).</summary>
    public static string CreateNotYetValidTempP12File() =>
        CreateTempP12File(DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddYears(1));

    private static string CreateTempP12File(DateTimeOffset notBefore, DateTimeOffset notAfter)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=ElectronicDocuments Test Certificate",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        using var cert = request.CreateSelfSigned(notBefore, notAfter);

        var p12Bytes = cert.Export(X509ContentType.Pfx, Password);

        var path = Path.Combine(Path.GetTempPath(), $"edoc-test-cert-{Guid.NewGuid():N}.p12");
        File.WriteAllBytes(path, p12Bytes);
        return path;
    }
}
