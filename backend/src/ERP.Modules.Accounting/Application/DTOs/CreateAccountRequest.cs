using Modules.Accounting.Domain.ValueObjects;

namespace Modules.Accounting.Application.DTOs;

public class CreateAccountRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public AccountType Type { get; set; }
    public AccountNature Nature { get; set; }
    public bool AllowsMovement { get; set; } = true;
    public Guid? ParentId { get; set; }
    public int Level { get; set; } = 1;
}
