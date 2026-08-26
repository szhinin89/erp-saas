using ERP.Domain.Common;
using ERP.Domain.Modules.Expenses.Enums;

namespace ERP.Domain.Modules.Expenses.Entities;

public sealed class ExpenseCategoryNode : AuditableEntity, ITenantScopedEntity, ICompanyOperationalEntity
{
    public const int CodeMaxLen = 30;
    public const int NameMaxLen = 150;
    public const int DescriptionMaxLen = 500;

    public Guid CompanyId { get; private set; }
    public Guid? ParentId { get; private set; }
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public ExpenseCategoryNodeLevel Level { get; private set; }
    public Guid? AccountingAccountId { get; private set; }
    public bool IsActive { get; private set; } = true;

    private ExpenseCategoryNode() { }

    public static ExpenseCategoryNode CreateType(
        Guid tenantId,
        Guid companyId,
        string code,
        string name,
        Guid createdBy,
        string? description = null
    ) =>
        Create(
            tenantId,
            companyId,
            code,
            name,
            ExpenseCategoryNodeLevel.Type,
            createdBy,
            parentId: null,
            accountingAccountId: null,
            description
        );

    public static ExpenseCategoryNode CreateCategory(
        Guid tenantId,
        Guid companyId,
        ExpenseCategoryNode parentType,
        string code,
        string name,
        Guid createdBy,
        string? description = null
    )
    {
        ArgumentNullException.ThrowIfNull(parentType);
        if (parentType.Level != ExpenseCategoryNodeLevel.Type)
            throw new ArgumentException(
                "La categoría de gasto debe tener como padre un tipo de gasto.",
                nameof(parentType)
            );
        EnsureSameScope(tenantId, companyId, parentType, nameof(parentType));

        return Create(
            tenantId,
            companyId,
            code,
            name,
            ExpenseCategoryNodeLevel.Category,
            createdBy,
            parentType.Id,
            accountingAccountId: null,
            description
        );
    }

    public static ExpenseCategoryNode CreateSubcategory(
        Guid tenantId,
        Guid companyId,
        ExpenseCategoryNode parentCategory,
        string code,
        string name,
        Guid accountingAccountId,
        Guid createdBy,
        string? description = null
    )
    {
        ArgumentNullException.ThrowIfNull(parentCategory);
        if (parentCategory.Level != ExpenseCategoryNodeLevel.Category)
            throw new ArgumentException(
                "La subcategoría de gasto debe tener como padre una categoría de gasto.",
                nameof(parentCategory)
            );
        EnsureSameScope(tenantId, companyId, parentCategory, nameof(parentCategory));

        return Create(
            tenantId,
            companyId,
            code,
            name,
            ExpenseCategoryNodeLevel.Subcategory,
            createdBy,
            parentCategory.Id,
            accountingAccountId,
            description
        );
    }

    private static ExpenseCategoryNode Create(
        Guid tenantId,
        Guid companyId,
        string code,
        string name,
        ExpenseCategoryNodeLevel level,
        Guid createdBy,
        Guid? parentId,
        Guid? accountingAccountId,
        string? description
    )
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("El tenant es obligatorio.", nameof(tenantId));
        if (companyId == Guid.Empty)
            throw new ArgumentException("La empresa es obligatoria.", nameof(companyId));
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("El código es obligatorio.", nameof(code));
        if (code.Trim().Length > CodeMaxLen)
            throw new ArgumentException($"El código no puede superar {CodeMaxLen} caracteres.", nameof(code));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre es obligatorio.", nameof(name));
        if (name.Trim().Length > NameMaxLen)
            throw new ArgumentException($"El nombre no puede superar {NameMaxLen} caracteres.", nameof(name));

        ValidateHierarchy(level, parentId, accountingAccountId);

        var node = new ExpenseCategoryNode
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            ParentId = parentId,
            Code = code.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            Description = NormalizeDescription(description),
            Level = level,
            AccountingAccountId = accountingAccountId,
            IsActive = true,
        };
        node.SetCreated(createdBy);
        return node;
    }

    public void Rename(string code, string name, Guid updatedBy, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("El código es obligatorio.", nameof(code));
        if (code.Trim().Length > CodeMaxLen)
            throw new ArgumentException($"El código no puede superar {CodeMaxLen} caracteres.", nameof(code));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre es obligatorio.", nameof(name));
        if (name.Trim().Length > NameMaxLen)
            throw new ArgumentException($"El nombre no puede superar {NameMaxLen} caracteres.", nameof(name));

        Code = code.Trim().ToUpperInvariant();
        Name = name.Trim();
        Description = NormalizeDescription(description);
        SetUpdated(updatedBy);
    }

    public void ChangeSubcategoryAccount(Guid accountingAccountId, Guid updatedBy)
    {
        if (Level != ExpenseCategoryNodeLevel.Subcategory)
            throw new InvalidOperationException("Solo una subcategoría puede tener cuenta contable.");
        if (accountingAccountId == Guid.Empty)
            throw new ArgumentException("La cuenta contable es obligatoria.", nameof(accountingAccountId));

        AccountingAccountId = accountingAccountId;
        SetUpdated(updatedBy);
    }

    public void SetActive(bool isActive, Guid updatedBy)
    {
        if (IsActive == isActive)
            return;

        IsActive = isActive;
        SetUpdated(updatedBy);
    }

    private static void ValidateHierarchy(
        ExpenseCategoryNodeLevel level,
        Guid? parentId,
        Guid? accountingAccountId
    )
    {
        if (level == ExpenseCategoryNodeLevel.Type)
        {
            if (parentId.HasValue)
                throw new ArgumentException("El tipo de gasto no puede tener padre.", nameof(parentId));
            if (accountingAccountId.HasValue)
                throw new ArgumentException(
                    "El tipo de gasto no puede tener cuenta contable.",
                    nameof(accountingAccountId)
                );
            return;
        }

        if (level == ExpenseCategoryNodeLevel.Category)
        {
            if (!parentId.HasValue || parentId.Value == Guid.Empty)
                throw new ArgumentException("La categoría de gasto requiere un tipo padre.", nameof(parentId));
            if (accountingAccountId.HasValue)
                throw new ArgumentException(
                    "La categoría de gasto no puede tener cuenta contable.",
                    nameof(accountingAccountId)
                );
            return;
        }

        if (!parentId.HasValue || parentId.Value == Guid.Empty)
            throw new ArgumentException("La subcategoría de gasto requiere una categoría padre.", nameof(parentId));
        if (!accountingAccountId.HasValue || accountingAccountId.Value == Guid.Empty)
            throw new ArgumentException(
                "La subcategoría de gasto requiere cuenta contable.",
                nameof(accountingAccountId)
            );
    }

    private static void EnsureSameScope(
        Guid tenantId,
        Guid companyId,
        ExpenseCategoryNode parent,
        string paramName
    )
    {
        if (parent.TenantId != tenantId || parent.CompanyId != companyId)
            throw new ArgumentException(
                "El nodo padre debe pertenecer al mismo tenant y empresa.",
                paramName
            );
    }

    private static string? NormalizeDescription(string? description) =>
        description?.Trim() is { Length: > 0 } value ? value : null;
}
