namespace ERP.Domain.Modules.Company.Entities;

public class GeneralParameter
{
    public Guid    Id          { get; set; }
    public Guid    CompanyId   { get; set; }
    public string  Key         { get; set; } = null!;
    public string? Value       { get; set; }
    public string? Description { get; set; }

    public Company Company { get; set; } = null!;
}
