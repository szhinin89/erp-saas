using ERP.Domain.Modules.ElectronicDocuments.Enums;

namespace ERP.Application.Modules.ElectronicDocuments.Services;

/// <summary>
/// SRI-TOGGLE-02/04/06 — estado del interruptor <c>electronic_documents.emission_enabled</c> de UNA empresa.
/// Ausente, vacío o ilegible = deshabilitada (fail-closed). <see cref="EnabledSinceUtc"/> es el instante de
/// la última habilitación efectiva (último registro auditado coherente de la key; si no, la última escritura
/// de la fila): todo pendiente con actividad anterior solo se reanuda por una acción explícita de un usuario.
/// </summary>
public sealed record ElectronicEmissionState(bool Enabled, DateTime? EnabledSinceUtc)
{
    public static readonly ElectronicEmissionState Disabled = new(false, null);

    /// <summary>
    /// ADR-048 — empresa suspendida o deshabilitada, leída en la misma lectura (y, en un reclamo, con la fila de la
    /// empresa bloqueada FOR SHARE hasta el commit). Solo pausa la TRANSMISIÓN NUEVA al SRI; no interviene en
    /// <see cref="AllowsSriAccess"/> (consultas, conciliación y reintentos de resultado pendiente siguen).
    /// </summary>
    public ERP.Domain.Modules.Company.Entities.CompanyPauseReason CompanyPause { get; init; } =
        ERP.Domain.Modules.Company.Entities.CompanyPauseReason.None;

    public bool CompanyPaused => CompanyPause != ERP.Domain.Modules.Company.Entities.CompanyPauseReason.None;

    /// <summary>
    /// SRI-TOGGLE-06 — ¿Puede este pendiente llegar al SRI (transmisión, reintento, consulta de autorización,
    /// historial R12 o ConsultaComprobante)? Deshabilitada = nunca (pausa total). Un usuario (acción
    /// explícita) solo necesita la emisión habilitada. Un proceso automático (<paramref name="automatic"/>,
    /// usuario <see cref="Guid.Empty"/>) además exige actividad posterior a la habilitación: la última
    /// actividad del pendiente (<paramref name="lastActivityUtc"/>: último intento del comprobante o última
    /// consulta de la anulación; solo avanza si una llamada pasó este control) o, sin ella, el instante
    /// persistido de su origen (<paramref name="originActivityUtc"/>: autorización/emisión del origen,
    /// presentación de la anulación). Nunca la creación del registro: un Draft creado tarde (recuperación)
    /// no convierte en "posterior" un origen anterior. Sin actividad conocida, un proceso automático no
    /// llama al SRI. Misma regla, en SQL, en <c>ElectronicEmissionSwitchQuery</c> (candidatos de los jobs).
    /// </summary>
    public bool AllowsSriAccess(DateTime? lastActivityUtc, DateTime? originActivityUtc, bool automatic) =>
        Enabled
        && (
            !automatic
            || EnabledSinceUtc is { } since
                && (lastActivityUtc ?? originActivityUtc) is { } activity
                && activity >= since
        );
}

/// <summary>
/// SRI-TOGGLE-02/04/06 — interruptor de emisión electrónica por empresa: deshabilitado, ninguna llamada al
/// SRI sobre comprobantes propios de la empresa (envío ni consulta). Siempre por <c>TenantId</c>+<c>CompanyId</c>
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
    /// SRI-TOGGLE-07 — ámbito de llamada al SRI de la empresa: toma el lock compartido de la empresa a nivel de
    /// SESIÓN (sobre la conexión del contexto, abierta hasta liberar el ámbito) y lo sostiene hasta disponerlo.
    /// Todo control del interruptor seguido de una llamada al SRI se hace dentro de un ámbito: un cambio del
    /// interruptor (lock exclusivo) espera a que terminen las llamadas en curso, y ninguna llamada iniciada
    /// después de confirmarse el apagado puede ver "habilitada". Reentrante por empresa dentro del mismo scope.
    /// No requiere transacción; no se sostiene ningún lock de filas de negocio.
    /// </summary>
    Task<IAsyncDisposable> EnterSriCallAsync(Guid tenantId, Guid companyId, CancellationToken ct = default);

    /// <summary>
    /// Lock exclusivo de la empresa para cambiar el interruptor. Exige una transacción activa y debe tomarse
    /// antes de escribir el valor (y su registro auditado) en esa misma transacción.
    /// </summary>
    Task LockForChangeAsync(Guid tenantId, Guid companyId, CancellationToken ct = default);
}
