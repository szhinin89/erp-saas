namespace ERP.Domain.Modules.Auxiliary.Entities;

/// <summary>
/// Registro de devolución automática de IVA (beneficio para clientes finales).
/// Aplica cuando el SRI autoriza devoluciones por compras con tarjeta de débito.
/// </summary>
public class VatRefund
{
    public Guid     Id           { get; set; }
    public Guid     CompanyId    { get; set; }
    public Guid     BusinessPartnerId   { get; set; }
    public Guid     SourceDocumentId { get; set; }
    public decimal  RefundAmount { get; set; }
    public DateOnly? AppliedDate { get; set; }
    /// <summary>Estado: pending | applied | rejected.</summary>
    public string   Status       { get; set; } = "pending";
    public string?  SriReference { get; set; }
    public string?  Notes        { get; set; }
    public DateTime CreatedAt    { get; set; }
}
