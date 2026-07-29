namespace ERP.Domain.Modules.Ride.ValueObjects;

/// <summary>
/// Presentación visual del RIDE, resuelta por <c>IRideBrandingProvider</c> (ADR-025 §12) —
/// nunca por este VO, que solo la envuelve. Todos los campos son opcionales: un tenant sin
/// branding configurado es un estado válido, no un error.
/// </summary>
public sealed record RideBranding
{
    public string? LogoStoragePath { get; }
    public string? PrimaryColorHex { get; }
    public string? SecondaryColorHex { get; }
    public string? FooterText { get; }

    private RideBranding(
        string? logoStoragePath,
        string? primaryColorHex,
        string? secondaryColorHex,
        string? footerText
    )
    {
        LogoStoragePath = logoStoragePath;
        PrimaryColorHex = primaryColorHex;
        SecondaryColorHex = secondaryColorHex;
        FooterText = footerText;
    }

    public static RideBranding Create(
        string? logoStoragePath = null,
        string? primaryColorHex = null,
        string? secondaryColorHex = null,
        string? footerText = null
    ) =>
        new(
            string.IsNullOrWhiteSpace(logoStoragePath) ? null : logoStoragePath.Trim(),
            string.IsNullOrWhiteSpace(primaryColorHex) ? null : primaryColorHex.Trim(),
            string.IsNullOrWhiteSpace(secondaryColorHex) ? null : secondaryColorHex.Trim(),
            string.IsNullOrWhiteSpace(footerText) ? null : footerText.Trim()
        );

    /// <summary>Ausencia total de branding configurado — estado válido, no un error (ver resumen del tipo).</summary>
    public static RideBranding Empty() => new(null, null, null, null);
}
