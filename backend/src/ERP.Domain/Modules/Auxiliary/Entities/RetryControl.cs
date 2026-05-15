namespace ERP.Domain.Modules.Auxiliary.Entities;

/// <summary>
/// Control de reintentos automáticos de autorización SRI.
/// Un job background consulta NextRetryAt y reintenta hasta MaxRetries.
/// Cuando IsExhausted = true, requiere intervención manual.
/// </summary>
public class RetryControl
{
    public Guid     Id            { get; set; }
    public Guid     CompanyId     { get; set; }
    public Guid     DocId         { get; set; }
    public short    RetryCount    { get; set; } = 0;
    public short    MaxRetries    { get; set; } = 5;
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? NextRetryAt   { get; set; }
    public string?  LastError     { get; set; }
    public bool     IsExhausted   { get; set; } = false;
    public DateTime CreatedAt     { get; set; }
}
