namespace ERP.Domain.Modules.Auxiliary.Entities;

/// <summary>
/// Log inmutable de cada llamada a los web services del SRI.
/// REGLA: solo INSERT — nunca se actualiza ni elimina.
/// Operaciones: RECEIVE | AUTHORIZE | QUERY.
/// </summary>
public class WsLog
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid? DocId { get; set; }
    public string Operation { get; set; } = null!;
    public short Environment { get; set; }
    public string? EndpointUrl { get; set; }
    public string? RequestPayload { get; set; }
    public string? ResponsePayload { get; set; }
    public short? HttpStatus { get; set; }
    public int? DurationMs { get; set; }
    public bool? Success { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorDetail { get; set; }
    public DateTime CalledAt { get; set; }
}
