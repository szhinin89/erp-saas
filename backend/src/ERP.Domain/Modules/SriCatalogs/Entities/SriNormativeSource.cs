namespace ERP.Domain.Modules.SriCatalogs.Entities;

/// <summary>
/// ZH-SRI-RETENTION-CATALOG-SSOT-01 (ADR-037 D10) — fuente normativa que respalda una versión de catálogo
/// SRI: documento oficial, versión, sección y migración del ERP que la incorporó. Dato interno del sistema
/// (ADR-037 D14): global, sin TenantId/CompanyId, sin CRUD; solo cambia por migraciones de ZH Technologies.
/// No lleva IsEnabled: es un registro de trazabilidad, no un valor seleccionable.
/// </summary>
public class SriNormativeSource
{
    public const int DocumentMaxLen = 60;
    public const int VersionMaxLen = 20;
    public const int SectionMaxLen = 200;
    public const int ReferenceUrlMaxLen = 500;
    public const int Sha256Len = 64;
    public const int MigrationMaxLen = 150;
    public const int NotesMaxLen = 1000;

    public Guid Id { get; set; }

    /// <summary>Tipo de documento normativo (p. ej. <c>FICHA_TECNICA_OFFLINE</c>, <c>CATALOGO_ATS</c>).</summary>
    public string Document { get; set; } = null!;

    /// <summary>Versión del documento (p. ej. <c>2.34</c>); null si la fuente no publica versión.</summary>
    public string? Version { get; set; }

    /// <summary>Sección/tabla/anexo concreto (p. ej. <c>Tabla 20 — Retención del IVA</c>).</summary>
    public string? Section { get; set; }

    public DateOnly? PublishedOn { get; set; }
    public string? ReferenceUrl { get; set; }

    /// <summary>SHA-256 (hex, minúsculas) del archivo oficial auditado, cuando se dispone de él.</summary>
    public string? DocumentSha256 { get; set; }

    /// <summary>Nombre de la migración EF que incorporó esta fuente (release del ERP).</summary>
    public string IntroducedInMigration { get; set; } = null!;

    public string? Notes { get; set; }
}
