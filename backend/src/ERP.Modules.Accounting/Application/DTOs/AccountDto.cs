using Modules.Accounting.Domain.ValueObjects;

namespace Modules.Accounting.Application.DTOs;

public class AccountDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public AccountType Type { get; set; }
    public AccountNature Nature { get; set; }
    public Guid? ParentId { get; set; }
    public int Level { get; set; }
    public bool AllowsMovement { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
