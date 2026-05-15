using ERP.Domain.Common;

namespace ERP.Domain.Modules.Accounting.Entities;

public sealed class ExpenseCategory : AuditableEntity, ITenantEntity
{
    public const int CategoryMaxLen = 120;

    public string Category       { get; private set; } = null!;
    public Guid   ExpenseAccountId { get; private set; }

    private ExpenseCategory() { }

    public static ExpenseCategory Create(
        Guid   tenantId,
        string category,
        Guid   expenseAccountId,
        Guid   createdBy)
    {
        var cat = category.Trim();
        if (cat.Length == 0)
            throw new ArgumentException("Category cannot be empty.", nameof(category));

        var e = new ExpenseCategory
        {
            TenantId        = tenantId,
            Category        = cat,
            ExpenseAccountId = expenseAccountId,
        };
        e.SetCreated(createdBy);
        return e;
    }

    public void UpdateAccount(Guid expenseAccountId, Guid updatedBy)
    {
        ExpenseAccountId = expenseAccountId;
        SetUpdated(updatedBy);
    }
}
