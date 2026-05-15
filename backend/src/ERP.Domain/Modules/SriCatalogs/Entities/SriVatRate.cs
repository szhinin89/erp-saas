namespace ERP.Domain.Modules.SriCatalogs.Entities;

public class SriVatRate
{
    public string    Code       { get; set; } = null!;
    public string    Name       { get; set; } = null!;
    public decimal   Percentage { get; set; }
    public bool      IsActive   { get; set; } = true;
    public DateOnly? ValidFrom  { get; set; }
    public DateOnly? ValidUntil { get; set; }
}
