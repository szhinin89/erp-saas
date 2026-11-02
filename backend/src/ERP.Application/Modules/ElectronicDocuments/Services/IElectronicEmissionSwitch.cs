using ERP.Domain.Modules.ElectronicDocuments.Enums;

namespace ERP.Application.Modules.ElectronicDocuments.Services;

/// <summary>
/// SRI-TOGGLE-02/04 — estado del interruptor <c>electronic_documents.emission_enabled</c> de UNA empresa.
/// Ausente, vacío o ilegible = deshabilitada (fail-closed). <see cref="EnabledSinceUtc"/> es el instante de
/// la última habilitación efectiva (último registro auditado coherente de la key; si no, la última escritura
/// de la fila): todo comprobante con actividad anterior solo se reanuda por una acción explícita de un usuario.
/// </summary>
public sealed record ElectronicEmissionState(bool Enabled, DateTime? EnabledSinceUtc)
{
    public static readonly ElectronicEmissionState Disabled = new(false, null);

    /// <summary>
    /// ¿Puede iniciarse una transmisión nueva? Un usuario (reanudación explícita) solo necesita la emisión
    /// habilitada. Un proceso automático (<paramref name="automatic"/>, usuario <see cref="Guid.Empty"/>)
    /// además exige actividad posterior a la habilitación: el último intento del comprobante (solo existe
    /// si un intento pasó este control) o, sin intentos, el instante persistido del origen
    /// (<paramref name="sourceActivityUtc"/>: autorización/emisión). Nunca la creación del comprobante: un
    /// Draft creado tarde (recuperación) no convierte en "posterior" un origen anterior. Sin actividad
    /// conocida, un proceso automático no transmite. Misma regla, en SQL, en
    /// <c>ElectronicEmissionSwitchQuery.WhereAutomaticTransmissionAllowed</c> (candidatos del job).
    /// </summary>
    public bool AllowsTransmission(DateTime? lastAttemptUtc, DateTime? sourceActivityUtc, bool automatic) =>
        Enabled
        && (
            !automatic
            || EnabledSinceUtc is { } since
                && (lastAttemptUtc ?? sourceActivityUtc) is { } activity
                && activity >= since
        );
}

/// <summary>
/// SRI-TOGGLE-02/04 — interruptor de emisión electrónica por empresa. Siempre por <c>TenantId</c>+<c>CompanyId</c>
/// explícitos del comprobante (nunca del body ni de otra empresa). Serialización: toda lectura toma un lock
/// compartido de la empresa y todo cambio un lock exclusivo ANTES de fechar su registro auditado; así una
/// lectura que vio "deshabilitada" siempre precede a la fecha de la habilitación siguiente.
/// </summary>
public interface IElectronicEmissionSwitch
{
    /// <param name="lockForShare">
    /// true dentro de la transacción de un reclamo de envío: el lock compartido se sostiene hasta su commit,
    /// de modo que un apagado no se confirma mientras el reclamo esté abierto. false: lectura puntual (con el
    /// mismo lock, en una transacción corta propia si no hay una activa).
    /// </param>
    Task<ElectronicEmissionState> GetAsync(
        Guid tenantId,
        Guid companyId,
        bool lockForShare,
        CancellationToken ct = default
    );

    /// <summary>
    /// Instante persistido de negocio del origen (autorización de factura/NC, emisión de retención), de la
    /// misma empresa. null si el origen no se conoce: un proceso automático no transmite.
    /// </summary>
    Task<DateTime?> GetSourceActivityUtcAsync(
        Guid tenantId,
        Guid companyId,
        string sourceModule,
        ElectronicDocumentType documentType,
        Guid sourceEntityId,
        CancellationToken ct = default
    );

    /// <summary>
    /// Lock exclusivo de la empresa para cambiar el interruptor. Exige una transacción activa y debe tomarse
    /// antes de escribir el valor (y su registro auditado) en esa misma transacción.
    /// </summary>
    Task LockForChangeAsync(Guid tenantId, Guid companyId, CancellationToken ct = default);
}
