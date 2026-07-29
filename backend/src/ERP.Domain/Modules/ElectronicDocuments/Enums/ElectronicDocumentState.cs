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
}
