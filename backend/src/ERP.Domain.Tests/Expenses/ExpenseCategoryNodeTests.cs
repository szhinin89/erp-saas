using ERP.Domain.Modules.Expenses.Entities;
using ERP.Domain.Modules.Expenses.Enums;
using FluentAssertions;

namespace ERP.Domain.Tests.Expenses;

public sealed class ExpenseCategoryNodeTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid ExpenseAccountId = Guid.NewGuid();

    [Fact]
    public void Type_does_not_accept_accounting_account()
    {
        var type = ExpenseCategoryNode.CreateType(
            TenantId,
            CompanyId,
            "ADM",
            "Administrativo",
            UserId
        );

        var act = () => type.ChangeSubcategoryAccount(ExpenseAccountId, UserId);

        act.Should().Throw<InvalidOperationException>().WithMessage("*subcategoría*");
        type.Level.Should().Be(ExpenseCategoryNodeLevel.Type);
        type.AccountingAccountId.Should().BeNull();
    }

    [Fact]
    public void Category_requires_parent_type()
    {
        var type = ExpenseCategoryNode.CreateType(
            TenantId,
            CompanyId,
            "ADM",
            "Administrativo",
            UserId
        );
        var category = ExpenseCategoryNode.CreateCategory(
            TenantId,
            CompanyId,
            type,
            "SERV",
            "Servicios",
            UserId
        );

        var act = () =>
            ExpenseCategoryNode.CreateCategory(
                TenantId,
                CompanyId,
                category,
                "BASIC",
                "Servicios básicos",
                UserId
            );

        act.Should().Throw<ArgumentException>().WithMessage("*padre un tipo*");
    }

    [Fact]
    public void Subcategory_requires_parent_category()
    {
        var type = ExpenseCategoryNode.CreateType(
            TenantId,
            CompanyId,
            "ADM",
            "Administrativo",
            UserId
        );

        var act = () =>
            ExpenseCategoryNode.CreateSubcategory(
                TenantId,
                CompanyId,
                type,
                "WATER",
                "Agua potable",
                ExpenseAccountId,
                UserId
            );

        act.Should().Throw<ArgumentException>().WithMessage("*padre una categoría*");
    }

    [Fact]
    public void Subcategory_requires_accounting_account()
    {
        var category = CreateCategory();

        var act = () =>
            ExpenseCategoryNode.CreateSubcategory(
                TenantId,
                CompanyId,
                category,
                "WATER",
                "Agua potable",
                Guid.Empty,
                UserId
            );

        act.Should().Throw<ArgumentException>().WithMessage("*cuenta contable*");
    }

    private static ExpenseCategoryNode CreateCategory()
    {
        var type = ExpenseCategoryNode.CreateType(
            TenantId,
            CompanyId,
            "ADM",
            "Administrativo",
            UserId
        );
        return ExpenseCategoryNode.CreateCategory(
            TenantId,
            CompanyId,
            type,
            "SERV",
            "Servicios",
            UserId
        );
    }
}
