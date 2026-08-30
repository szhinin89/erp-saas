using ERP.Application.Common;
using ERP.Application.Modules.Accounting.UseCases.Accounts;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Accounting.ValueObjects;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Accounting;

/// <summary>
/// ACCOUNTING-CHART-OF-ACCOUNTS-02 — cobertura de las brechas completadas sobre Account: validación
/// de padre (existencia/ciclos), UpdateAccountCommand, GetAccountByCodeQuery, bloqueo de
/// desactivación cuando la cuenta está referenciada por una PostingRule activa, y cálculo de
/// Level/ParentAccountCode/ParentAccountName en el DTO.
/// </summary>
public sealed class AccountUseCasesTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid CreatedBy = Guid.NewGuid();

    private static Account NewAccount(string code, string name, Guid? parentId = null) =>
        Account.Create(
            TenantId,
            CompanyId,
            AccountCode.Create(code),
            name,
            parentId,
            AccountType.Asset,
            AccountNature.Debit,
            true,
            CreatedBy
        );

    private sealed class Mocks
    {
        public Mock<IAccountRepository> Accounts { get; } = new();
        public Mock<IPostingRuleRepository> PostingRules { get; } = new();
        public Mock<ICurrentTenant> Tenant { get; } = new();
        public Mock<ICurrentCompany> Company { get; } = new();
        public Mock<ICurrentUser> User { get; } = new();

        public Mocks()
        {
            Tenant.Setup(t => t.TenantId).Returns(TenantId);
            Company.Setup(c => c.CompanyId).Returns(CompanyId);
            User.Setup(u => u.UserId).Returns(CreatedBy);
            PostingRules
                .Setup(r => r.GetByCompanyAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<PostingRule>());
        }
    }

    [Fact]
    public async Task GetAccounts_calcula_Level_y_datos_del_padre()
    {
        var root = NewAccount("1", "Activo");
        var child = NewAccount("1.1", "Activo Corriente", root.Id);
        var grandchild = NewAccount("1.1.01", "Caja", child.Id);

        var m = new Mocks();
        m.Accounts
            .Setup(r => r.GetByCompanyAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Account> { root, child, grandchild });

        var handler = new GetAccountsHandler(m.Accounts.Object, m.Tenant.Object, m.Company.Object);
        var result = await handler.Handle(new GetAccountsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var dtos = result.Value!.ToDictionary(x => x.Id);
        dtos[root.Id].Level.Should().Be(0);
        dtos[root.Id].ParentAccountCode.Should().BeNull();
        dtos[child.Id].Level.Should().Be(1);
        dtos[child.Id].ParentAccountCode.Should().Be("1");
        dtos[grandchild.Id].Level.Should().Be(2);
        dtos[grandchild.Id].ParentAccountName.Should().Be("Activo Corriente");
    }

    [Fact]
    public async Task CreateAccount_rechaza_padre_inexistente()
    {
        var m = new Mocks();
        m.Accounts
            .Setup(r => r.FindByCodeAsync(TenantId, CompanyId, "1.1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Account?)null);
        m.Accounts
            .Setup(r => r.GetByCompanyAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Account>());

        var handler = new CreateAccountHandler(
            m.Accounts.Object,
            m.Tenant.Object,
            m.Company.Object,
            m.User.Object,
            Mock.Of<ERP.Application.Common.Persistence.IDatabaseExceptionTranslator>()
        );
        var result = await handler.Handle(
            new CreateAccountCommand("1.1", "x", Guid.NewGuid(), AccountType.Asset, AccountNature.Debit, true),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        m.Accounts.Verify(r => r.AddAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// ACCOUNTING-CHART-CANONICAL-HIERARCHY-01: el ParentAccountId debe coincidir con el padre
    /// canónico implicado por el código — no basta con que exista, sino que sea el prefijo
    /// inmediato correcto (aquí "2" existe pero "1.1" implica padre "1", no "2").
    /// </summary>
    [Fact]
    public async Task CreateAccount_rechaza_padre_que_no_coincide_con_el_codigo()
    {
        var unrelated = NewAccount("2", "Pasivo");
        var m = new Mocks();
        m.Accounts
            .Setup(r => r.FindByCodeAsync(TenantId, CompanyId, "1.1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Account?)null);
        m.Accounts
            .Setup(r => r.GetByCompanyAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Account> { unrelated });

        var handler = new CreateAccountHandler(
            m.Accounts.Object,
            m.Tenant.Object,
            m.Company.Object,
            m.User.Object,
            Mock.Of<ERP.Application.Common.Persistence.IDatabaseExceptionTranslator>()
        );
        var result = await handler.Handle(
            new CreateAccountCommand("1.1", "x", unrelated.Id, AccountType.Asset, AccountNature.Debit, true),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        m.Accounts.Verify(r => r.AddAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAccount_rechaza_codigo_raiz_con_padre_indicado()
    {
        var m = new Mocks();
        m.Accounts
            .Setup(r => r.FindByCodeAsync(TenantId, CompanyId, "9", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Account?)null);
        m.Accounts
            .Setup(r => r.GetByCompanyAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Account>());

        var handler = new CreateAccountHandler(
            m.Accounts.Object,
            m.Tenant.Object,
            m.Company.Object,
            m.User.Object,
            Mock.Of<ERP.Application.Common.Persistence.IDatabaseExceptionTranslator>()
        );
        var result = await handler.Handle(
            new CreateAccountCommand("9", "x", Guid.NewGuid(), AccountType.Asset, AccountNature.Debit, false),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAccount_rechaza_padre_que_no_coincide_con_el_codigo()
    {
        var unrelated = NewAccount("2", "Pasivo");
        var account = NewAccount("1.1", "Activo Corriente");

        var m = new Mocks();
        m.Accounts
            .Setup(r => r.GetByIdAsync(TenantId, CompanyId, account.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        m.Accounts
            .Setup(r => r.GetByCompanyAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Account> { unrelated, account });

        var handler = new UpdateAccountHandler(m.Accounts.Object, m.Tenant.Object, m.Company.Object, m.User.Object);
        var result = await handler.Handle(
            new UpdateAccountCommand(account.Id, "Activo Corriente", unrelated.Id, false),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        account.ParentAccountId.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAccount_rechaza_quitar_padre_de_cuenta_con_codigo_compuesto()
    {
        var parent = NewAccount("1", "Activo");
        var account = NewAccount("1.1", "Activo Corriente", parent.Id);

        var m = new Mocks();
        m.Accounts
            .Setup(r => r.GetByIdAsync(TenantId, CompanyId, account.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        m.Accounts
            .Setup(r => r.GetByCompanyAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Account> { parent, account });

        var handler = new UpdateAccountHandler(m.Accounts.Object, m.Tenant.Object, m.Company.Object, m.User.Object);
        // Intenta dejar la cuenta "1.1" sin padre — inválido, su código exige padre "1".
        var result = await handler.Handle(
            new UpdateAccountCommand(account.Id, "Activo Corriente", null, false),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAccount_rechaza_ciclo_al_reparentar()
    {
        var root = NewAccount("1", "Activo");
        var child = NewAccount("1.1", "Activo Corriente", root.Id);

        var m = new Mocks();
        m.Accounts
            .Setup(r => r.GetByIdAsync(TenantId, CompanyId, root.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(root);
        m.Accounts
            .Setup(r => r.GetByCompanyAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Account> { root, child });

        var handler = new UpdateAccountHandler(m.Accounts.Object, m.Tenant.Object, m.Company.Object, m.User.Object);
        // Intenta poner a "root" como hijo de su propio hijo "child" — ciclo.
        var result = await handler.Handle(
            new UpdateAccountCommand(root.Id, "Activo", child.Id, true),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        root.ParentAccountId.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAccount_rechaza_auto_padre()
    {
        var account = NewAccount("1", "Activo");
        var m = new Mocks();
        m.Accounts
            .Setup(r => r.GetByIdAsync(TenantId, CompanyId, account.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        m.Accounts
            .Setup(r => r.GetByCompanyAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Account> { account });

        var handler = new UpdateAccountHandler(m.Accounts.Object, m.Tenant.Object, m.Company.Object, m.User.Object);
        var result = await handler.Handle(
            new UpdateAccountCommand(account.Id, "Activo", account.Id, true),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAccount_correcto_actualiza_nombre_padre_y_allowsPosting()
    {
        var parent = NewAccount("1", "Activo");
        var account = NewAccount("1.1", "Viejo nombre");

        var m = new Mocks();
        m.Accounts
            .Setup(r => r.GetByIdAsync(TenantId, CompanyId, account.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        m.Accounts
            .Setup(r => r.GetByCompanyAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Account> { parent, account });

        var handler = new UpdateAccountHandler(m.Accounts.Object, m.Tenant.Object, m.Company.Object, m.User.Object);
        var result = await handler.Handle(
            new UpdateAccountCommand(account.Id, "Nuevo nombre", parent.Id, false),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("Nuevo nombre");
        result.Value.ParentAccountId.Should().Be(parent.Id);
        result.Value.AllowsPosting.Should().BeFalse();
        m.Accounts.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DisableAccount_bloqueado_si_regla_activa_la_referencia()
    {
        var account = NewAccount("4.1.01", "Ventas");
        var rule = PostingRule.Create(TenantId, CompanyId, "Sales", "InvoiceIssued", null, account.Id, null, CreatedBy);

        var m = new Mocks();
        m.Accounts
            .Setup(r => r.GetByIdAsync(TenantId, CompanyId, account.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        m.PostingRules
            .Setup(r => r.GetByCompanyAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostingRule> { rule });

        var handler = new DisableAccountHandler(
            m.Accounts.Object,
            m.PostingRules.Object,
            m.Tenant.Object,
            m.Company.Object,
            m.User.Object
        );
        var result = await handler.Handle(new DisableAccountCommand(account.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.Conflict);
        account.IsActive.Should().BeTrue();
        m.Accounts.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DisableAccount_permite_desactivar_si_ninguna_regla_activa_la_usa()
    {
        var account = NewAccount("4.1.01", "Ventas");
        var m = new Mocks();
        m.Accounts
            .Setup(r => r.GetByIdAsync(TenantId, CompanyId, account.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        m.Accounts
            .Setup(r => r.GetByCompanyAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Account> { account });

        var handler = new DisableAccountHandler(
            m.Accounts.Object,
            m.PostingRules.Object,
            m.Tenant.Object,
            m.Company.Object,
            m.User.Object
        );
        var result = await handler.Handle(new DisableAccountCommand(account.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        account.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task GetAccountByCode_delega_en_FindByCodeAsync()
    {
        var account = NewAccount("1.1.01", "Caja");
        var m = new Mocks();
        m.Accounts
            .Setup(r => r.FindByCodeAsync(TenantId, CompanyId, "1.1.01", It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        m.Accounts
            .Setup(r => r.GetByCompanyAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Account> { account });

        var handler = new GetAccountByCodeHandler(m.Accounts.Object, m.Tenant.Object, m.Company.Object);
        var result = await handler.Handle(new GetAccountByCodeQuery("1.1.01"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Id.Should().Be(account.Id);
    }
}
