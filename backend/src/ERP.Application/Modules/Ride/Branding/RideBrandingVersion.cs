using System.Security.Cryptography;
using System.Text;
using ERP.Domain.Modules.Ride.ValueObjects;

namespace ERP.Application.Modules.Ride.Branding;

/// <summary>
/// Deriva una versión determinística (SHA-256 hex, 16 caracteres) del contenido de
/// <see cref="RideBranding"/> — dos brandings con el mismo contenido producen el mismo valor;
/// cualquier cambio en logo/colores/pie de página produce uno distinto.
///
/// Fase 8 (ADR-025 §14, decisión confirmada): <c>RidePipeline</c> (Fase 5, protegido esta fase)
/// calcula la huella de cache con una versión de branding constante y solo consulta
/// <c>IRideBrandingProvider</c> después de un cache-miss — nunca antes de decidir. Por eso este
/// valor se verifica a nivel de <c>RideCacheStrategy</c> (que sí compara correctamente huellas
/// distintas), no como invalidación automática end-to-end del pipeline completo — eso requeriría
/// mover esa llamada dentro de <c>RidePipeline</c>, fuera de alcance de esta fase.
/// </summary>
public static class RideBrandingVersion
{
    public static string Compute(RideBranding branding)
    {
        var canonical = string.Join('|',
            branding.LogoStoragePath ?? string.Empty,
            branding.PrimaryColorHex ?? string.Empty,
            branding.SecondaryColorHex ?? string.Empty,
            branding.FooterText ?? string.Empty);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexStringLower(hash)[..16];
    }
}
