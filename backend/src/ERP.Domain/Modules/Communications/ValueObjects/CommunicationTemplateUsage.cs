using ERP.Domain.Modules.Communications.Enums;

namespace ERP.Domain.Modules.Communications.ValueObjects;

/// <summary>
/// ZH-COMMUNICATIONS-TEMPLATES-01 — qué template produjo el contenido persistido de una comunicación
/// (auditoría): "se envió con TemplateKey X, versión Y, fuente Default/CompanyOverride". Default:
/// versión del default embebido; CompanyOverride: revisión del override de la empresa.
/// </summary>
public sealed record CommunicationTemplateUsage
{
    public const int KeyMaxLen = 100;

    public CommunicationTemplateUsage(string key, int version, CommunicationTemplateSource source)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Trim().Length > KeyMaxLen)
            throw new ArgumentException("TemplateKey inválida.", nameof(key));
        if (version < 1)
            throw new ArgumentOutOfRangeException(
                nameof(version),
                "La versión del template empieza en 1."
            );
        if (
            source
            is not (
                CommunicationTemplateSource.Default
                or CommunicationTemplateSource.CompanyOverride
            )
        )
            throw new ArgumentException(
                "Una comunicación nueva se renderiza con un default o un override de empresa.",
                nameof(source)
            );

        Key = key.Trim();
        Version = version;
        Source = source;
    }

    public string Key { get; }
    public int Version { get; }
    public CommunicationTemplateSource Source { get; }
}
