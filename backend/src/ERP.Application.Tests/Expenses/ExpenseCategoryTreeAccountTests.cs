using ERP.Application.Common;
using ERP.Application.Modules.Expenses.DTOs;
using ERP.Application.Modules.Expenses.UseCases.Categories;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.Expenses.Entities;
using ERP.Domain.Modules.Expenses.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Expenses;

/// <summary>
/// EXPENSE-BACKEND-01 — el árbol de categorías (permiso <c>expenses.catalog.view</c>) expone solo el
/// código y nombre de la cuenta vinculada a cada subcategoría, para que el formulario de gastos no
/// dependa de <c>GET /accounting/accounts</c> (<c>accounting.view</c>). No expone otras cuentas.
/// </summary>
public sealed class ExpenseCategoryTreeAccountTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static Account NewAccount(string code, string name, Guid? companyId = null) =>
        Account.Create(
            TenantId,
            companyId ?? CompanyId,
            AccountCode.Create(code),
            name,
            null,
            AccountType.Expense,
            AccountNature.Debit,
            true,
            UserId
        );

    private static ExpenseCategoryNode[] TreeWith(Guid? subcategoryAccountId)
    {
        var type = ExpenseCategoryNode.CreateType(TenantId, CompanyId, "ADM", "Administrativos", UserId);
        var category = ExpenseCategoryNode.CreateCategory(
            TenantId,
            CompanyId,
            type,
            "OFF",
            "Oficina",
            UserId
        );
        var sub = ExpenseCategoryNode.CreateSubcategory(
            TenantId,
            CompanyId,
            category,
            "PAP",
            "Papeleria",
            subcategoryAccountId ?? Guid.NewGuid(),
            UserId
        );
        return [type, category, sub];
    }

    private static (ListExpenseCategoryTreeHandler Handler, Mock<IAccountRepository> Accounts) Build(
        ExpenseCategoryNode[] nodes,
        params Account[] companyAccounts
    )
    {
        var categories = new Mock<IExpenseCategoryRepository>();
        categories
            .Setup(r => r.GetTreeAsync(TenantId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(nodes);
        var accounts = new Mock<IAccountRepository>();
        accounts
            .Setup(a => a.GetByCompanyAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(companyAccounts);
        var handler = new ListExpenseCategoryTreeHandler(
            categories.Object,
            accounts.Object,
            Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
            Mock.Of<ICurrentCompany>(c => c.CompanyId == CompanyId)
        );
        return (handler, accounts);
    }

    private static IEnumerable<ExpenseCategoryTreeNodeDto> Flatten(
        IEnumerable<ExpenseCategoryTreeNodeDto> nodes
    ) => nodes.SelectMany(n => new[] { n }.Concat(Flatten(n.Children)));

    [Fact]
    public async Task Expone_codigo_y_nombre_de_la_cuenta_vinculada_a_la_subcategoria()
    {
        var linked = NewAccount("6.1.01.001", "Gasto administrativo");
        var (handler, _) = Build(TreeWith(linked.Id), linked);

        var result = await handler.Handle(new ListExpenseCategoryTreeQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        var sub = Flatten(result.Value!).Single(n => n.Level == ERP.Domain.Modules.Expenses.Enums.ExpenseCategoryNodeLevel.Subcategory);
        sub.AccountingAccountId.Should().Be(linked.Id);
        sub.AccountingAccountCode.Should().Be("6.1.01.001");
        sub.AccountingAccountName.Should().Be("Gasto administrativo");
    }

    [Fact]
    public async Task No_expone_ninguna_otra_cuenta_del_plan()
    {
        var linked = NewAccount("6.1.01.001", "Gasto administrativo");
        var other = NewAccount("1.1.01.001", "Caja general SECRETA");
        var (handler, _) = Build(TreeWith(linked.Id), linked, other);

        var result = await handler.Handle(new ListExpenseCategoryTreeQuery(), CancellationToken.None);

        var serialized = System.Text.Json.JsonSerializer.Serialize(result.Value);
        serialized.Should().NotContain("1.1.01.001").And.NotContain("SECRETA");
        Flatten(result.Value!)
            .Where(n => n.AccountingAccountCode is not null)
            .Should()
            .ContainSingle();
    }

    [Fact]
    public async Task Una_cuenta_que_no_es_de_la_empresa_activa_no_se_resuelve()
    {
        // La subcategoría apunta a una cuenta que no figura en el plan de la empresa activa
        // (p. ej. de otra empresa): no se filtra su código ni su nombre.
        var foreign = NewAccount("6.9.99.999", "Cuenta de otra empresa", companyId: Guid.NewGuid());
        var (handler, accounts) = Build(TreeWith(foreign.Id));

        var result = await handler.Handle(new ListExpenseCategoryTreeQuery(), CancellationToken.None);

        var sub = Flatten(result.Value!).Single(n => n.AccountingAccountId == foreign.Id);
        sub.AccountingAccountCode.Should().BeNull();
        sub.AccountingAccountName.Should().BeNull();
        accounts.Verify(
            a => a.GetByCompanyAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Consulta_las_cuentas_solo_de_la_empresa_activa()
    {
        var linked = NewAccount("6.1.01.001", "Gasto administrativo");
        var (handler, accounts) = Build(TreeWith(linked.Id), linked);

        await handler.Handle(new ListExpenseCategoryTreeQuery(), CancellationToken.None);

        accounts.Verify(
            a => a.GetByCompanyAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()),
            Times.Once
        );
        accounts.Verify(
            a => a.GetByCompanyAsync(It.Is<Guid>(t => t != TenantId), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Sin_cuentas_vinculadas_no_consulta_el_plan_de_cuentas()
    {
        var type = ExpenseCategoryNode.CreateType(TenantId, CompanyId, "ADM", "Administrativos", UserId);
        var (handler, accounts) = Build([type]);

        var result = await handler.Handle(new ListExpenseCategoryTreeQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        accounts.Verify(
            a => a.GetByCompanyAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }
}
