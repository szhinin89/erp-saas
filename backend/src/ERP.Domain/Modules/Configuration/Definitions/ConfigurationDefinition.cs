using ERP.Domain.Configuration.Enums;

namespace ERP.Domain.Configuration.Definitions;

/// <summary>
/// CONFIG-FOUNDATION-P1-03: ConfigurationDefinition CORE — el contrato de negocio puro de una
/// key de <c>org_settings</c>. Es lo único que un resolver o el guardrail de escritura necesitan
/// para saber si un valor es válido; no sabe nada de cómo se presenta o quién puede editarla.
///
/// Deliberadamente NO contiene: nombre visible, descripción para usuario, help text, i18n key,
/// orden de UI, pestaña, permiso requerido, ni ningún otro dato de presentación o de acceso —
/// eso vive en capas superiores (Presentation metadata / Access metadata, fuera de Domain, no
/// implementadas todavía — ver Fase 4 de docs/architecture/configuration-engine-target-architecture.md).
/// Si algún día un resolver necesita "el nombre visible" de una key, es señal de que se está
/// mezclando una responsabilidad que no le corresponde a esta clase.
/// </summary>
public sealed record ConfigurationDefinition
{
    /// <summary>Key exacta tal como se persiste en org_settings.key (lowercase, con puntos).</summary>
    public required string Key { get; init; }

    /// <summary>Namespace/módulo de negocio dueño de la key (ej. "Invoice", "Sales", "Presentation").</summary>
    public required string Module { get; init; }

    public required ConfigurationDataType DataType { get; init; }

    /// <summary>Scopes en los que esta key puede persistirse. Nunca vacío.</summary>
    public required IReadOnlyCollection<OrgScope> AllowedScopes { get; init; }

    /// <summary>Scope que un escritor debe usar si no se especifica otro explícitamente.</summary>
    public required OrgScope DefaultScope { get; init; }

    /// <summary>Valor por defecto documentado (referencia para desarrolladores) — el resolver de negocio decide si aplicarlo, esta clase no lo aplica.</summary>
    public string? DefaultValue { get; init; }

    public required ConfigurationFallbackStrategy FallbackStrategy { get; init; }

    /// <summary>Si el valor contiene datos sensibles (credenciales, PII) — reservado para ConfigurationChangeLog futuro, aún no implementado.</summary>
    public bool IsSensitive { get; init; }

    /// <summary>Si un cambio de esta key debe auditarse — reservado para ConfigurationChangeLog futuro, aún no implementado.</summary>
    public bool RequiresAudit { get; init; }

    /// <summary>Si el valor resuelto debe congelarse en el documento que lo consume — reservado para snapshots futuros, aún no implementados.</summary>
    public bool RequiresSnapshot { get; init; }

    /// <summary>
    /// Si un usuario con permiso puede editar esta key directamente (vs. una key derivada/interna
    /// que solo el sistema escribe). Flag de negocio puro — NO es una regla de autorización de
    /// endpoint (eso es Access metadata, fuera de Domain).
    /// </summary>
    public bool IsUserEditable { get; init; } = true;

    /// <summary>
    /// Valida un valor crudo (ya normalizado: null/blank tratado como "no configurado", siempre
    /// válido). Null implica que basta con el chequeo de DataType — no hay regla adicional.
    /// </summary>
    public Func<string?, bool>? Validator { get; init; }

    /// <summary>Nota técnica para desarrolladores (por qué existe esta key, qué la consume) — nunca texto visible al usuario final.</summary>
    public string? DeveloperNotes { get; init; }

    public SettingDataType PersistedDataType => DataType.ToPersistedType();

    /// <summary>
    /// True si <paramref name="value"/> es válido para esta definition: siempre válido si es
    /// null/blank (ausencia de configuración es un estado válido, ver arquitectura), si no,
    /// delega en <see cref="Validator"/> (si existe).
    /// </summary>
    public bool IsValidValue(string? value) =>
        string.IsNullOrWhiteSpace(value) || (Validator?.Invoke(value) ?? true);
}
