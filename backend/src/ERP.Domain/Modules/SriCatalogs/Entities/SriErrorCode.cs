namespace ERP.Domain.Modules.SriCatalogs.Entities;

public class SriErrorCode
{
    public string  Code        { get; set; } = null!;
    public string  ErrorType   { get; set; } = null!;  // ERROR | WARNING | INFO
    public string  Name        { get; set; } = null!;
    public string? Description { get; set; }
}
