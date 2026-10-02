namespace ERP.Domain.Modules.ElectronicDocuments.Enums;

/// <summary>
/// Ciclo de vida del documento electrónico. Draft es el único estado alcanzable en esta fase —
/// las transiciones posteriores (XmlGenerated..Cancelled) las introducirán las fases que
/// implementen generación de XML, firma y comunicación con el SRI.
/// </summary>
public enum ElectronicDocumentState
{
    Draft = 1,
    XmlGenerated = 2,
    Signed = 3,
    Sent = 4,
    Received = 5,
    Authorized = 6,
    Rejected = 7,
    DeadLetter = 8,
    Cancelled = 9,

    /// <summary>
    /// Falló alguna etapa previa a la firma/persistencia definitiva (proveedor de datos,
    /// construcción de XML, validación XSD, firma) o el almacenamiento del XML. El documento
    /// sí existe (creado en Draft antes de correr el pipeline) para que el fallo sea visible y
    /// reintentable en el Monitor — nunca desaparece silenciosamente. El motivo queda en
    /// <c>ElectronicDocument.LastError</c>.
    /// </summary>
    Failed = 10,

    /// <summary>
    /// ADR-036 (D-3) — el ERP persistió que va a realizar/intentar una transmisión externa a
    /// Recepción del SRI. Se guarda ANTES de la llamada de red, bajo el lock del documento de origen
    /// (solo para orígenes con <c>IElectronicDocumentSourceLifecycleGuard</c> registrado — hoy
    /// Retenciones). Desde aquí el comprobante PUEDE estar en el SRI: nunca se reenvía
    /// automáticamente; el único paso siguiente permitido es consultar su autorización.
    /// </summary>
    Dispatching = 11,

    /// <summary>
    /// ADR-036 (D-2) — terminal: hay certeza de que este documento nunca tuvo un intento de
    /// transmisión externa (Draft/Failed, o DeadLetter que venía de ellos) y su origen ya no
    /// permite procesarlo. Nunca se reintenta, reactiva, regenera, firma ni envía.
    /// </summary>
    Discarded = 12,

    /// <summary>
    /// ADR-036 (D-7, ZH-RETENTION-SRI-ANNULMENT-01) — existe una solicitud de anulación ante el SRI
    /// que todavía no se confirmó como ANULADO. El comprobante SIGUE siendo fiscalmente válido: no se
    /// reintenta, no se reenvía y su origen no se revierte. Solo sale a <see cref="Cancelled"/>
    /// (ANULADO confirmado, con evidencia) o de vuelta a <see cref="Authorized"/> (rechazada / sin
    /// efecto / desistida).
    /// </summary>
    AnnulmentPending = 13,
}
