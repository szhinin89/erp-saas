namespace ERP.Domain.Modules.Retentions.Enums;

/// <summary>ZH-RETENTION-SRI-ANNULMENT-01 — estado de una solicitud de anulación ante el SRI (no es el estado de la retención).</summary>
public enum RetentionAnnulmentStatus
{
    /// <summary>Registrada en el ERP; el usuario todavía no la presentó en SRI en Línea.</summary>
    PendingSubmission = 1,

    /// <summary>Presentada al SRI; el ERP consulta su estado en ConsultaComprobante (01B) hasta que el SRI informe ANULADO.</summary>
    PendingSriResolution = 2,

    /// <summary>El SRI informó ANULADO en ConsultaComprobante (evidencia técnica registrada). Habilita la finalización del origen.</summary>
    Accepted = 3,

    /// <summary>
    /// Reservado (valor persistido estable). 01B retiró la resolución manual: ninguna transición llega
    /// aquí. Un rechazo de la solicitud se refleja como AUTORIZADO en la consulta y se cierra desistiendo.
    /// </summary>
    Rejected = 4,

    /// <summary>Reservado (valor persistido estable); sin transición desde 01B — ver <see cref="Rejected"/>.</summary>
    Expired = 5,

    /// <summary>Desistida: antes de presentarse al SRI, o ya presentada con el SRI confirmando AUTORIZADO.</summary>
    Abandoned = 6,
}
