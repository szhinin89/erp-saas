using ERP.Application.Common;
using ERP.Application.Modules.Expenses.UseCases.Categories;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.Expenses.Entities;
using ERP.Domain.Modules.Expenses.Enums;
using ERP.Domain.Modules.Expenses.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Expenses;

public sealed class ExpenseCategoryUseCasesTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid AccountId = Guid.NewGuid();

    private static Mock<ICurrentTenant> Tenant()
    {
        var m = new Mock<ICurrentTenant>();
        m.SetupGet(t => t.TenantId).Returns(TenantId);
        return m;
    }

    private static Mock<ICurrentCompany> Company()
    {
        var m = new Mock<ICurrentCompany>();
        m.SetupGet(c => c.CompanyId).Returns(CompanyId);
        return m;
    }

    private static Mock<ICurrentUser> User()
    {
        var m = new Mock<ICurrentUser>();
        m.SetupGet(u => u.UserId).Returns(UserId);
        return m;
    }

    private static Account ExpenseAccount(
        bool allowsPosting = true,
        bool isActive = true,
        AccountType accountType = AccountType.Expense,
        Guid? companyId = null
    )
    {
        var account = Account.Create(
            TenantId,
            companyId ?? CompanyId,
            AccountCode.Create("6.1.01.001"),
            "Gasto administrativo",
            null,
            accountType,
            AccountNature.Debit,
            allowsPosting,
            UserId
        );

        if (!isActive)
            account.Disable(UserId);

        return account;
    }

    private static ExpenseCategoryNode TypeNode() =>
        ExpenseCategoryNode.CreateType(TenantId, CompanyId, "ADM", "Administrativos", UserId);

    private static ExpenseCategoryNode CategoryNode(ExpenseCategoryNode parentType) =>
        ExpenseCategoryNode.CreateCategory(
            TenantId,
            CompanyId,
            parentType,
            "OFFICE",
            "Oficina",
            UserId
        );

    private static ExpenseCategoryNode SubcategoryNode(ExpenseCategoryNode parentCategory) =>
        ExpenseCategoryNode.CreateSubcategory(
            TenantId,
            CompanyId,
            parentCategory,
            "PAPER",
            "Papeleria",
            AccountId,
            UserId
        );

    private static Mock<IExpenseCategoryRepository> CategoryRepo()
    {
        var repo = new Mock<IExpenseCategoryRepository>();
        repo.Setup(r =>
                r.CodeExistsAsync(
                    TenantId,
                    CompanyId,
                    It.IsAny<Guid?>(),
                    It.IsAny<ExpenseCategoryNodeLevel>(),
                    It.IsAny<string>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(false);
        repo.Setup(r =>
                r.NameExistsAsync(
                    TenantId,
                    CompanyId,
                    It.IsAny<Guid?>(),
                    It.IsAny<ExpenseCategoryNodeLevel>(),
                    It.IsAny<string>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(false);
        return repo;
    }

    private static Mock<IAccountRepository> AccountRepo(Account? account = null)
    {
        var accounts = new Mock<IAccountRepository>();
        if (account is not null)
        {
            accounts
                .Setup(a =>
                    a.GetByIdAsync(TenantId, CompanyId, AccountId, It.IsAny<CancellationToken>())
                )
                .ReturnsAsync(account);
        }

        return accounts;
    }

    private static CreateExpenseCategoryNodeHandler CreateHandler(
        IExpenseCategoryRepository repo,
        IAccountRepository? accounts = null
    ) =>
        new(
            repo,
            accounts ?? AccountRepo().Object,
            Tenant().Object,
            Company().Object,
            User().Object
        );

    [Fact]
    public async Task Create_Type_valido_persiste_en_Draft_de_catalogo()
    {
        var repo = CategoryRepo();
        var handler = CreateHandler(repo.Object);

        var result = await handler.Handle(
            new CreateExpenseCategoryNodeCommand(
                "ADM",
                "Administrativos",
                ExpenseCategoryNodeLevel.Type,
                null,
                null
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.Level.Should().Be(ExpenseCategoryNodeLevel.Type);
        result.Value.ParentId.Should().BeNull();
        result.Value.AccountingAccountId.Should().BeNull();
        repo.Verify(
            r =>
                r.AddAsync(
                    It.Is<ExpenseCategoryNode>(n =>
                        n.Level == ExpenseCategoryNodeLevel.Type
                        && n.ParentId == null
                        && n.AccountingAccountId == null
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_Type_con_cuenta_contable_se_bloquea()
    {
        var repo = CategoryRepo();
        var handler = CreateHandler(repo.Object);

        var result = await handler.Handle(
            new CreateExpenseCategoryNodeCommand(
                "ADM",
                "Administrativos",
                ExpenseCategoryNodeLevel.Type,
                null,
                AccountId
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_Category_bajo_Type_activo_persiste()
    {
        var type = TypeNode();
        var repo = CategoryRepo();
        repo.Setup(r => r.GetByIdAsync(TenantId, type.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(type);
        var handler = CreateHandler(repo.Object);

        var result = await handler.Handle(
            new CreateExpenseCategoryNodeCommand(
                "OFFICE",
                "Oficina",
                ExpenseCategoryNodeLevel.Category,
                type.Id,
                null
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.Level.Should().Be(ExpenseCategoryNodeLevel.Category);
        result.Value.ParentId.Should().Be(type.Id);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_Category_bajo_Category_se_bloquea()
    {
        var type = TypeNode();
        var category = CategoryNode(type);
        var repo = CategoryRepo();
        repo.Setup(r => r.GetByIdAsync(TenantId, category.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);
        var handler = CreateHandler(repo.Object);

        var result = await handler.Handle(
            new CreateExpenseCategoryNodeCommand(
                "TRAVEL",
                "Viajes",
                ExpenseCategoryNodeLevel.Category,
                category.Id,
                null
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_Category_bajo_Subcategory_se_bloquea()
    {
        var type = TypeNode();
        var category = CategoryNode(type);
        var subcategory = SubcategoryNode(category);
        var repo = CategoryRepo();
        repo.Setup(r =>
                r.GetByIdAsync(TenantId, subcategory.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(subcategory);
        var handler = CreateHandler(repo.Object);

        var result = await handler.Handle(
            new CreateExpenseCategoryNodeCommand(
                "TRAVEL",
                "Viajes",
                ExpenseCategoryNodeLevel.Category,
                subcategory.Id,
                null
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_Subcategory_con_cuenta_valida_persiste()
    {
        var type = TypeNode();
        var category = CategoryNode(type);
        var repo = CategoryRepo();
        repo.Setup(r => r.GetByIdAsync(TenantId, category.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);
        var accounts = AccountRepo(ExpenseAccount());
        var handler = CreateHandler(repo.Object, accounts.Object);

        var result = await handler.Handle(
            new CreateExpenseCategoryNodeCommand(
                "PAPER",
                "Papeleria",
                ExpenseCategoryNodeLevel.Subcategory,
                category.Id,
                AccountId
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.Level.Should().Be(ExpenseCategoryNodeLevel.Subcategory);
        result.Value.AccountingAccountId.Should().Be(AccountId);
        accounts.Verify(
            a => a.GetByIdAsync(TenantId, CompanyId, AccountId, It.IsAny<CancellationToken>()),
            Times.Once
        );
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_Subcategory_sin_cuenta_se_bloquea()
    {
        var type = TypeNode();
        var category = CategoryNode(type);
        var repo = CategoryRepo();
        repo.Setup(r => r.GetByIdAsync(TenantId, category.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);
        var handler = CreateHandler(repo.Object);

        var result = await handler.Handle(
            new CreateExpenseCategoryNodeCommand(
                "PAPER",
                "Papeleria",
                ExpenseCategoryNodeLevel.Subcategory,
                category.Id,
                null
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_Subcategory_con_cuenta_no_postable_se_bloquea()
    {
        var result = await CreateSubcategoryWithAccountAsync(
            ExpenseAccount(allowsPosting: false)
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
    }

    [Fact]
    public async Task Create_Subcategory_con_cuenta_inactiva_se_bloquea()
    {
        var result = await CreateSubcategoryWithAccountAsync(ExpenseAccount(isActive: false));

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
    }

    [Fact]
    public async Task Create_Subcategory_con_cuenta_no_Expense_se_bloquea()
    {
        var result = await CreateSubcategoryWithAccountAsync(
            ExpenseAccount(accountType: AccountType.Asset)
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
    }

    [Fact]
    public async Task Create_Subcategory_con_cuenta_de_otra_empresa_se_bloquea()
    {
        var result = await CreateSubcategoryWithAccountAsync(account: null);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }

    private static async Task<Result<ERP.Application.Modules.Expenses.DTOs.ExpenseCategoryNodeDto>> CreateSubcategoryWithAccountAsync(
        Account? account
    )
    {
        var type = TypeNode();
        var category = CategoryNode(type);
        var repo = CategoryRepo();
        repo.Setup(r => r.GetByIdAsync(TenantId, category.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);
        var handler = CreateHandler(repo.Object, AccountRepo(account).Object);

        var result = await handler.Handle(
            new CreateExpenseCategoryNodeCommand(
                "PAPER",
                "Papeleria",
                ExpenseCategoryNodeLevel.Subcategory,
                category.Id,
                AccountId
            ),
            CancellationToken.None
        );

        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        return result;
    }
}
