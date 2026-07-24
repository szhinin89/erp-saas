using ERP.Domain.Modules.SriCatalogs.Enums;

namespace ERP.Domain.Modules.SriCatalogs.Entities;

public class SriIdTypeUsage
{
    public Guid                     Id          { get; set; }
    public string                   IdTypeCode  { get; set; } = null!;
    public IdentificationUsageType  UsageType   { get; set; }
    public bool                     IsActive    { get; set; } = true;
}
