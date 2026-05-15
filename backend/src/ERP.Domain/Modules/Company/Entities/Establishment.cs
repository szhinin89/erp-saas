namespace ERP.Domain.Modules.Company.Entities;

/// <summary>
/// Establecimiento SRI (sucursal emisora).
/// Código de 3 dígitos (001-999) único por empresa.
/// </summary>
public class Establishment
{
    public Guid    Id        { get; set; }
    public Guid    CompanyId { get; set; }
    public string  Code      { get; set; } = null!;
    public string  Name      { get; set; } = null!;
    public string  Address   { get; set; } = null!;
    public string? Phone     { get; set; }
    public bool    IsMain    { get; set; } = false;
    public bool    IsActive  { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public Company Company { get; set; } = null!;
    public ICollection<EmissionPoint> EmissionPoints { get; set; } = [];
}
