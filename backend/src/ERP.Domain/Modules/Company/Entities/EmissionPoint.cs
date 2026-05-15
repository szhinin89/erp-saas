namespace ERP.Domain.Modules.Company.Entities;

/// <summary>
/// Punto de emisión SRI (001-999) dentro de un establecimiento.
/// </summary>
public class EmissionPoint
{
    public Guid    Id              { get; set; }
    public Guid    CompanyId       { get; set; }
    public Guid    EstablishmentId { get; set; }
    public string  Code            { get; set; } = null!;
    public string? Name            { get; set; }
    public bool    IsActive        { get; set; } = true;
    public DateTime CreatedAt      { get; set; }

    public Company       Company       { get; set; } = null!;
    public Establishment Establishment { get; set; } = null!;
    public ICollection<DocumentSequence> Sequences { get; set; } = [];
}
