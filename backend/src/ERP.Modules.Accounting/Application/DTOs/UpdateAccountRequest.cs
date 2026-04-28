namespace Modules.Accounting.Application.DTOs;

public class UpdateAccountRequest
{
    public string Name { get; set; } = string.Empty;
    public bool AllowsMovement { get; set; }
}
