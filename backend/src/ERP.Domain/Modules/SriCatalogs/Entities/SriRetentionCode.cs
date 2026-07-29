namespace ERP.Domain.Modules.SriCatalogs.Entities;

public class SriRetentionCode
{
    public Guid Id { get; set; }
    public string TaxType { get; set; } = null!; // IVA | RENTA | ISD
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public decimal Percentage { get; set; }
    public string AppliesTo { get; set; } = "SUPPLIER";
    public bool IsActive { get; set; } = true;
}
