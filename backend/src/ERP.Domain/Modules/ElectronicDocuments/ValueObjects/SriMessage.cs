namespace ERP.Domain.Modules.ElectronicDocuments.ValueObjects;

/// <summary>
/// Un nodo <c>&lt;mensaje&gt;</c> estructurado tal como lo devuelve el SRI (Ficha Técnica,
/// respuestas de recepción/autorización) — <see cref="Code"/>/<see cref="MessageType"/>/
/// <see cref="Message"/>/<see cref="AdditionalInfo"/> se persisten y exponen exactamente como
/// llegaron, nunca resumidos, traducidos ni reescritos. <see cref="MessageType"/> refleja el
/// literal de <c>&lt;tipo&gt;</c> (p.ej. "ERROR", "ADVERTENCIA") — nunca normalizado a un enum
/// cerrado, para no requerir cambios de código ante un tipo nuevo del SRI. Vive en Domain (no en
/// Application) porque <see cref="Entities.ElectronicDocument.MarkRejected"/> y
/// <see cref="Events.ElectronicDocumentRejectedEvent"/> lo transportan directamente — Domain
/// nunca depende de Application.
/// </summary>
public sealed record SriMessage(
    string? Code,
    string MessageType,
    string Message,
    string? AdditionalInfo
);
