using ERP.Domain.Common;
using ERP.Domain.Accounting.Enums;
using ERP.Domain.Accounting.ValueObjects;

namespace ERP.Domain.Accounting.Entities;

public class Account : MasterEntity
{
    public AccountCode Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public AccountType Type { get; private set; }
    public AccountNature Nature { get; private set; }
    public Guid? ParentId { get; private set; }

    private Account() { }

    public static Account Create(
        Guid tenantId,
        string code,
        string name,
        AccountType type,
        AccountNature nature,
        Guid createdBy,
        Guid? parentId = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre de la cuenta no puede estar vacío.");

        var account = new Account
        {
            Id        = Guid.NewGuid(),
            TenantId  = tenantId,
            Code      = new AccountCode(code),
            Name      = name,
            Type      = type,
            Nature    = nature,
            ParentId  = parentId,
        };

        account.SetCreated(createdBy);
        return account;
    }

    public void Update(
        string name,
        AccountType type,
        AccountNature nature,
        Guid updatedBy,
        Guid? parentId = null)
    {
        Name     = name;
        Type     = type;
        Nature   = nature;
        ParentId = parentId;
        SetUpdated(updatedBy);
    }
}
