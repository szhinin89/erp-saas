namespace ERP.Domain.Modules.ElectronicDocuments.Enums;

/// <summary>
/// ZH-RETENTION-SRI-ANNULMENT-01B — estado fiscal OFICIAL de un comprobante autorizado según el SRI,
/// tal como lo informa el WS ConsultaComprobante (<c>estadoAutorizacion</c>, Ficha Técnica
/// Comprobantes Electrónicos Esquema Offline v2.34 §8.4). Es la fuente de verdad del estado fiscal:
/// el usuario nunca lo declara. <see cref="Unknown"/> cuando la consulta no produjo un estado
/// reconocido (fallo de consulta, literal desconocido) — nunca se infiere otro valor.
/// </summary>
public enum SriFiscalStatus
{
    Unknown = 0,

    /// <summary><c>AUTORIZADO</c> — el comprobante sigue vigente.</summary>
    Authorized = 1,

    /// <summary><c>NO AUTORIZADO</c> — significado no caracterizado para comprobantes ya autorizados (ADR-036 §24, Decision Required).</summary>
    NotAuthorized = 2,

    /// <summary><c>PENDIENTE DE ANULAR</c> — el SRI registró la solicitud y aún no la resolvió.</summary>
    PendingAnnulment = 3,

    /// <summary><c>ANULADO</c> — el SRI anuló el comprobante.</summary>
    Annulled = 4,
}
