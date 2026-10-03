namespace ERP.Domain.Modules.Communications.Enums;

/// <summary>
/// ZH-COMMUNICATIONS-TEMPLATES-01 (ADR-039 D12) — de dónde salió el contenido de una comunicación.
/// </summary>
public enum CommunicationTemplateSource
{
    /// <summary>Template por defecto embebido en el release (versión = versión del default).</summary>
    Default = 1,

    /// <summary>Override activo de la empresa (versión = revisión del override).</summary>
    CompanyOverride = 2,

    /// <summary>Filas anteriores al subsistema de templates: contenido armado en código (sin versión).</summary>
    Legacy = 3,
}
