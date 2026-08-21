namespace ERP.Domain.Configuration.Entities;

/// <summary>
/// ERP-CORE-CLOSEOUT-09 — Datos del proveedor del sistema de facturación electrónica (quién
/// construyó/mantiene el software), requeridos por obligaciones de proveedor de sistema ante el
/// SRI (p.ej. Resolución NAC-DGERCGC26-00000027). Deliberadamente distinto de <c>Company</c>
/// (el emisor de cada comprobante) y de <c>SriSettings</c> (configuración SRI por empresa
/// emisora) — el proveedor de sistema es un hecho fijo de la instancia/despliegue del ERP, no
/// algo que cada empresa cliente configura. Singleton (Id = 1), sin TenantId/CompanyId, mismo
/// patrón que <see cref="ERP.Domain.Setup.SystemSetupState"/>.
///
/// PRECONDICIÓN NORMATIVA (ver STATUS.md, ERP-CORE-CLOSEOUT-09): esta entidad solo persiste el
/// dato. NO se inyecta todavía en ningún XML de comprobante electrónico — el campo/elemento
/// exacto donde debe declararse (si aplica) requiere confirmar el texto de la resolución/ficha
/// técnica SRI aplicable antes de tocar los XML builders.
/// </summary>
public sealed class SystemProviderSettings
{
    public const int RucLength = 13;
    public const int LegalNameMaxLen = 300;
    public const int CiiuCodeMaxLen = 20;

    public int Id { get; private set; } = 1;
    public string? Ruc { get; private set; }
    public string? LegalName { get; private set; }
    public string? CiiuCode { get; private set; }
    public bool Enabled { get; private set; }
    public DateOnly? EffectiveDate { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    private SystemProviderSettings() { }

    public static SystemProviderSettings CreateNew() => new();

    /// <summary>
    /// Configura/actualiza los datos del proveedor de sistema. <paramref name="enabled"/> solo
    /// puede activarse (true) si RUC/LegalName/CiiuCode ya están completos — fail-closed: nunca
    /// queda "habilitado" con datos incompletos.
    /// </summary>
    public void Configure(
        string? ruc,
        string? legalName,
        string? ciiuCode,
        DateOnly? effectiveDate,
        bool enabled,
        Guid updatedBy
    )
    {
        var normalizedRuc = string.IsNullOrWhiteSpace(ruc) ? null : ruc.Trim();
        var normalizedLegalName = string.IsNullOrWhiteSpace(legalName) ? null : legalName.Trim();
        var normalizedCiiu = string.IsNullOrWhiteSpace(ciiuCode) ? null : ciiuCode.Trim();

        if (normalizedRuc is not null && normalizedRuc.Length != RucLength)
            throw new ArgumentException(
                $"El RUC del proveedor de sistema debe tener {RucLength} dígitos.",
                nameof(ruc)
            );
        if (normalizedRuc is not null && !normalizedRuc.All(char.IsDigit))
            throw new ArgumentException(
                "El RUC del proveedor de sistema debe ser numérico.",
                nameof(ruc)
            );
        if (normalizedLegalName is not null && normalizedLegalName.Length > LegalNameMaxLen)
            throw new ArgumentException(
                $"La razón social no puede superar {LegalNameMaxLen} caracteres.",
                nameof(legalName)
            );
        if (normalizedCiiu is not null && normalizedCiiu.Length > CiiuCodeMaxLen)
            throw new ArgumentException(
                $"El código CIIU no puede superar {CiiuCodeMaxLen} caracteres.",
                nameof(ciiuCode)
            );

        if (enabled && (normalizedRuc is null || normalizedLegalName is null || normalizedCiiu is null))
            throw new InvalidOperationException(
                "No se puede habilitar el proveedor de sistema sin RUC, razón social y CIIU completos."
            );

        Ruc = normalizedRuc;
        LegalName = normalizedLegalName;
        CiiuCode = normalizedCiiu;
        EffectiveDate = effectiveDate;
        Enabled = enabled;
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    /// <summary>True cuando los tres datos obligatorios están completos, independientemente de Enabled.</summary>
    public bool IsFullyConfigured =>
        !string.IsNullOrWhiteSpace(Ruc)
        && !string.IsNullOrWhiteSpace(LegalName)
        && !string.IsNullOrWhiteSpace(CiiuCode);
}
