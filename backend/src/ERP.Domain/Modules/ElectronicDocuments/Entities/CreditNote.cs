namespace ERP.Domain.Modules.ElectronicDocuments.Entities;

/// <summary>Extensión 1:1 de ElectronicDoc para notas de crédito (doc_type 04).</summary>
public class CreditNote
{
    public Guid    Id                 { get; set; }
    public Guid    CompanyId          { get; set; }
    public Guid?   OrigDocId          { get; set; }
    public string  OrigDocType        { get; set; } = "01";
    public string  OrigEstablishment  { get; set; } = null!;
    public string  OrigEmissionPoint  { get; set; } = null!;
    public string  OrigSequential     { get; set; } = null!;
    public DateOnly OrigIssueDate     { get; set; }
    public string? BuyerIdType        { get; set; }
    public string? BuyerIdNumber      { get; set; }
    public string? BuyerName          { get; set; }
    public Guid?   CustomerId         { get; set; }
    public string  Reason             { get; set; } = null!;

    public ElectronicDoc  ElectronicDoc { get; set; } = null!;
    public ElectronicDoc? OrigDoc       { get; set; }
}
