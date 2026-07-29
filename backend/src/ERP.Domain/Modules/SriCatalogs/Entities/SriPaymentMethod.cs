namespace ERP.Domain.Modules.SriCatalogs.Entities;

public class SriPaymentMethod
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public bool IsActive { get; set; } = true;
}
