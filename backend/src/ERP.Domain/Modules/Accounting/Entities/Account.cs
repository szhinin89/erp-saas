using ERP.Domain.Common;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.ValueObjects;

namespace ERP.Domain.Modules.Accounting.Entities;

public class Account : MasterEntity, ICompanyScopedEntity
{
    public Guid CompanyId { get; private set; }
    public AccountCode Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public AccountType Type { get; private set; }
    public AccountNature Nature { get; private set; }
    public Guid? ParentId { get; private set; }

    /// <summary>Si es false, la cuenta es de agrupaciÃ³n y no debe usarse en partidas de asientos.</summary>
    public bool AllowsMovements { get; private set; } = true;

    private Account() { }

    public static Account Create(
        Guid subscriberId,
        Guid companyId,
        string code,
        string name,
        AccountType type,
        AccountNature nature,
        Guid createdBy,
        Guid? parentId = null,
        bool allowsMovements = true)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre de la cuenta no puede estar vacÃ­o.");

        var account = new Account
        {
            Id                = Guid.NewGuid(),
            SubscriberId      = subscriberId,
            CompanyId = companyId,
            Code              = new AccountCode(code),
            Name              = name,
            Type              = type,
            Nature            = nature,
            ParentId          = parentId,
            AllowsMovements   = allowsMovements,
        };

        account.SetCreated(createdBy);
        return account;
    }

    public void Update(
        string name,
        AccountType type,
        AccountNature nature,
        Guid updatedBy,
        Guid? parentId = null,
        bool? allowsMovements = null)
    {
        Name     = name;
        Type     = type;
        Nature   = nature;
        ParentId = parentId;
        if (allowsMovements.HasValue)
            AllowsMovements = allowsMovements.Value;
        SetUpdated(updatedBy);
    }
}
