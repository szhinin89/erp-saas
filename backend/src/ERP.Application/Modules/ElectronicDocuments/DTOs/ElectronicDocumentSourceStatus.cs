using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;

namespace ERP.Application.Modules.ElectronicDocuments.DTOs;

/// <summary>
/// ZH-RETENTION-ELECTRONIC-LIFECYCLE-01A — estado electrónico compacto que la pantalla del documento
/// de origen muestra (la retención dentro de Compra/Gasto). Lo calcula el backend a partir del
/// estado real del <see cref="ElectronicDocument"/>: el frontend nunca reinterpreta estados crudos.
/// </summary>
public enum ElectronicDocumentSourceStatus
{
    /// <summary>Todavía no salió nada al SRI (sin documento, Draft, Failed o DeadLetter de un fallo previo a la firma).</summary>
    Pending = 1,

    /// <summary>Despachado o recibido por el SRI, esperando resultado.</summary>
    Processing = 2,

    Authorized = 3,
    Rejected = 4,

    /// <summary>
    /// El ERP no puede afirmar si el comprobante llegó al SRI ni con qué resultado (Signed histórico,
    /// Dispatching con una consulta no concluyente, DeadLetter de un envío/consulta): nunca se
    /// reenvía a ciegas; debe conciliarse consultando al SRI (ADR-036 D-4/D-10).
    /// </summary>
    RequiresReconciliation = 5,

    /// <summary>Nunca tuvo intento externo y el origen ya no permite transmitirlo.</summary>
    Discarded = 6,
}

public static class ElectronicDocumentSourceStatusMapper
{
    public static ElectronicDocumentSourceStatus From(ElectronicDocument? document) =>
        document is null
            ? ElectronicDocumentSourceStatus.Pending
            : document.CurrentState switch
            {
                ElectronicDocumentState.Draft
                or ElectronicDocumentState.Failed
                or ElectronicDocumentState.XmlGenerated => ElectronicDocumentSourceStatus.Pending,
                ElectronicDocumentState.Dispatching when document.RetryCount > 0 =>
                    ElectronicDocumentSourceStatus.RequiresReconciliation,
                ElectronicDocumentState.Dispatching
                or ElectronicDocumentState.Sent
                or ElectronicDocumentState.Received => ElectronicDocumentSourceStatus.Processing,
                ElectronicDocumentState.Authorized => ElectronicDocumentSourceStatus.Authorized,
                ElectronicDocumentState.Rejected => ElectronicDocumentSourceStatus.Rejected,
                ElectronicDocumentState.Discarded => ElectronicDocumentSourceStatus.Discarded,
                ElectronicDocumentState.DeadLetter when document.CanBeDiscarded =>
                    ElectronicDocumentSourceStatus.Pending,
                // Signed (histórico: ambiguo), DeadLetter de un envío/consulta, y Cancelled (sin
                // flujo productivo todavía — ZH-RETENTION-SRI-ANNULMENT-01).
                _ => ElectronicDocumentSourceStatus.RequiresReconciliation,
            };
}
