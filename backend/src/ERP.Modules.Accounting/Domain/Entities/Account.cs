using ERP.Shared.Domain;
using Modules.Accounting.Domain.ValueObjects;

namespace Modules.Accounting.Domain.Entities;

public class Account : BaseEntity
{
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public AccountType Type { get; private set; }
    public AccountNature Nature { get; private set; }
    public Guid? ParentId { get; private set; }
    public int Level { get; private set; }
    public bool AllowsMovement { get; private set; }

    private Account() { }

    public static Account Create(
        Guid tenantId,
        string code,
        string name,
        AccountType type,
        AccountNature nature,
        bool allowsMovement = true,
        Guid? parentId = null,
        int level = 1)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Code es requerido");
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name es requerido");

        var account = new Account
        {
            TenantId = tenantId,
            Code = code.Trim(),
            Name = name.Trim(),
            Type = type,
            Nature = nature,
            AllowsMovement = allowsMovement,
            ParentId = parentId,
            Level = level
        };
        return account;
    }

    public void Update(string name, bool allowsMovement)
    {
        Name = name.Trim();
        AllowsMovement = allowsMovement;
        SetUpdated();
    }
}
