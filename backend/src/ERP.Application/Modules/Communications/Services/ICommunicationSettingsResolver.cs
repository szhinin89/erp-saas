using ERP.Domain.Modules.Communications.ValueObjects;

namespace ERP.Application.Modules.Communications.Services;

public interface ICommunicationSettingsResolver
{
    /// <summary>
    /// Perfil efectivo para un alcance EXPLÍCITO (ADR-039 D19), sin depender del contexto ambiente:
    /// Company → perfil de esa empresa (OrgSettings) con el fallback de instancia actual;
    /// System → solo perfil de instancia (nunca OrgSettings de una empresa).
    /// </summary>
    Task<CommunicationEmailSettings> ResolveEmailAsync(
        CommunicationScope scope,
        CancellationToken ct = default
    );

    /// <summary>Perfil de la empresa del contexto actual (pantalla de configuración y correo de prueba).</summary>
    Task<CommunicationEmailSettings> ResolveEmailAsync(CancellationToken ct = default);
}
