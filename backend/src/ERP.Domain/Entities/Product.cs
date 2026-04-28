namespace ERP.Domain.Entities;
public class Product
{
    // Mapea a uuid
    public Guid Id { get; set; } 
    // Mapea a text
    public string Name { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    // Mapea a numeric
    public decimal Price { get; set; }
    // Mapea a int4
    public int Stock { get; set; }
    // Mapea a timestamptz
    public DateTime CreatedAt { get; set; }
    // El signo '?' indica que es nullable (como se ve en tu tabla)
    public DateTime? UpdatedAt { get; set; } 
}