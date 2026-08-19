namespace ERP.Domain.Configuration.Definitions;

/// <summary>
/// CONFIG-FOUNDATION-P1-03: qué ocurre cuando una key no tiene valor configurado en ningún scope
/// de su cadena de precedencia (ver docs/architecture/configuration-engine-target-architecture.md
/// Fase 3). Es documentación de la estrategia — el resolver tipado de cada dominio sigue siendo
/// quien la aplica; esta enum no ejecuta lógica por sí sola.
/// </summary>
public enum ConfigurationFallbackStrategy
{
    /// <summary>Cae a una constante fija de plataforma (ej. SriSettings.FallbackDocTypeCode).</summary>
    SystemDefault,

    /// <summary>Se calcula desde otra regla de negocio (ej. régimen tributario de la empresa).</summary>
    DomainDefault,

    /// <summary>Sin valor configurado y sin default seguro — exige selección manual del usuario.</summary>
    RequireManualSelection,

    /// <summary>Ausencia de valor es un error — no debería ocurrir en operación normal.</summary>
    Error,

    /// <summary>Setting visual/no crítico: ausencia o valor corrupto cae a un default seguro sin bloquear.</summary>
    VisualSafeDefault,
}
