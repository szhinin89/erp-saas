using ERP.Application.Common;
using ERP.Application.Modules.Finance.UseCases;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Finance.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Finance;

/// <summary>TREASURY-BANK-ACCOUNTS-01: pruebas de los casos de uso de <see cref="CompanyBankAccount"/>.</summary>
public sealed class CompanyBankAccountUseCasesTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid BankId = Guid.NewGuid();
    private static readonly Guid AccountingAccountId = Guid.NewGuid();

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

    private static Bank ActiveBank(bool isActive = true)
    {
        var bank = Bank.Create(TenantId, "PICHINCHA", "Banco Pichincha", "Pichincha", UserId);
        if (!isActive)
            bank.Disable(UserId);
        return bank;
    }

    private static Account ActiveAccount(bool allowsPosting = true, bool isActive = true)
    {
        var account = Account.Create(
            TenantId,
            CompanyId,
            AccountCode.Create("1.1.01.001"),
            "Bancos",
            null,
            AccountType.Asset,
            AccountNature.Debit,
            allowsPosting,
            UserId
        );
        if (!isActive)
            account.Disable(UserId);
        return account;
    }

    private static CompanyBankAccount ExistingAccount() =>
        CompanyBankAccount.Create(
            TenantId,
            CompanyId,
            BankId,
            BankAccountType.Checking,
            "2200123456",
            "Cuenta corriente Pichincha",
            AccountingAccountId,
            UserId
        );

    // ── Create ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_valido_persiste_una_sola_vez_y_retorna_el_dto()
    {
        var repo = new Mock<ICompanyBankAccountRepository>();
        var banks = new Mock<IBankRepository>();
        var accounts = new Mock<IAccountRepository>();
        banks.Setup(b => b.GetByIdAsync(TenantId, BankId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveBank());
        accounts
            .Setup(a =>
                a.GetByIdAsync(TenantId, CompanyId, AccountingAccountId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(ActiveAccount());
        repo.Setup(r =>
                r.ExistsAsync(
                    TenantId,
                    CompanyId,
                    BankId,
                    (int)BankAccountType.Checking,
                    "2200123456",
                    null,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(false);

        var handler = new CreateCompanyBankAccountHandler(
            repo.Object,
            banks.Object,
            accounts.Object,
            Tenant().Object,
            Company().Object,
            User().Object
        );
        var cmd = new CreateCompanyBankAccountCommand(
            BankId,
            BankAccountType.Checking,
            "2200123456",
            "Cuenta corriente Pichincha",
            AccountingAccountId
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.DisplayName.Should().Be("Cuenta corriente Pichincha");
        result.Value.AccountType.Should().Be(nameof(BankAccountType.Checking));
        repo.Verify(
            r => r.AddAsync(It.IsAny<CompanyBankAccount>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_con_banco_inexistente_retorna_NotFound()
    {
        var repo = new Mock<ICompanyBankAccountRepository>();
        var banks = new Mock<IBankRepository>();
        var accounts = new Mock<IAccountRepository>();
        banks.Setup(b => b.GetByIdAsync(TenantId, BankId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Bank?)null);

        var handler = new CreateCompanyBankAccountHandler(
            repo.Object,
            banks.Object,
            accounts.Object,
            Tenant().Object,
            Company().Object,
            User().Object
        );
        var cmd = new CreateCompanyBankAccountCommand(
            BankId,
            BankAccountType.Checking,
            "2200123456",
            "Cuenta",
            AccountingAccountId
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        repo.Verify(
            r => r.AddAsync(It.IsAny<CompanyBankAccount>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Create_con_banco_inactivo_retorna_ValidationFailure()
    {
        var repo = new Mock<ICompanyBankAccountRepository>();
        var banks = new Mock<IBankRepository>();
        var accounts = new Mock<IAccountRepository>();
        banks.Setup(b => b.GetByIdAsync(TenantId, BankId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveBank(isActive: false));

        var handler = new CreateCompanyBankAccountHandler(
            repo.Object,
            banks.Object,
            accounts.Object,
            Tenant().Object,
            Company().Object,
            User().Object
        );
        var cmd = new CreateCompanyBankAccountCommand(
            BankId,
            BankAccountType.Checking,
            "2200123456",
            "Cuenta",
            AccountingAccountId
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Create_con_cuenta_contable_no_postable_retorna_ValidationFailure()
    {
        var repo = new Mock<ICompanyBankAccountRepository>();
        var banks = new Mock<IBankRepository>();
        var accounts = new Mock<IAccountRepository>();
        banks.Setup(b => b.GetByIdAsync(TenantId, BankId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveBank());
        accounts
            .Setup(a =>
                a.GetByIdAsync(TenantId, CompanyId, AccountingAccountId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(ActiveAccount(allowsPosting: false));

        var handler = new CreateCompanyBankAccountHandler(
            repo.Object,
            banks.Object,
            accounts.Object,
            Tenant().Object,
            Company().Object,
            User().Object
        );
        var cmd = new CreateCompanyBankAccountCommand(
            BankId,
            BankAccountType.Checking,
            "2200123456",
            "Cuenta",
            AccountingAccountId
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Create_duplicado_por_banco_tipo_y_numero_retorna_UniqueViolation()
    {
        var repo = new Mock<ICompanyBankAccountRepository>();
        var banks = new Mock<IBankRepository>();
        var accounts = new Mock<IAccountRepository>();
        banks.Setup(b => b.GetByIdAsync(TenantId, BankId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveBank());
        accounts
            .Setup(a =>
                a.GetByIdAsync(TenantId, CompanyId, AccountingAccountId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(ActiveAccount());
        repo.Setup(r =>
                r.ExistsAsync(
                    TenantId,
                    CompanyId,
                    BankId,
                    (int)BankAccountType.Checking,
                    "2200123456",
                    null,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(true);

        var handler = new CreateCompanyBankAccountHandler(
            repo.Object,
            banks.Object,
            accounts.Object,
            Tenant().Object,
            Company().Object,
            User().Object
        );
        var cmd = new CreateCompanyBankAccountCommand(
            BankId,
            BankAccountType.Checking,
            "2200123456",
            "Cuenta",
            AccountingAccountId
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        repo.Verify(
            r => r.AddAsync(It.IsAny<CompanyBankAccount>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    // ── Update ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_valido_modifica_alias_y_cuenta_contable()
    {
        var entity = ExistingAccount();
        var repo = new Mock<ICompanyBankAccountRepository>();
        var accounts = new Mock<IAccountRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, entity.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);
        var newAccountingAccountId = Guid.NewGuid();
        accounts
            .Setup(a =>
                a.GetByIdAsync(TenantId, CompanyId, newAccountingAccountId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(ActiveAccount());

        var handler = new UpdateCompanyBankAccountHandler(
            repo.Object,
            accounts.Object,
            Tenant().Object,
            Company().Object,
            User().Object
        );
        var cmd = new UpdateCompanyBankAccountCommand(entity.Id, "Nuevo alias", newAccountingAccountId);

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.DisplayName.Should().Be("Nuevo alias");
        result.Value.AccountingAccountId.Should().Be(newAccountingAccountId);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_de_cuenta_inexistente_retorna_NotFound()
    {
        var repo = new Mock<ICompanyBankAccountRepository>();
        var accounts = new Mock<IAccountRepository>();
        var missingId = Guid.NewGuid();
        repo.Setup(r => r.GetByIdAsync(TenantId, missingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompanyBankAccount?)null);

        var handler = new UpdateCompanyBankAccountHandler(
            repo.Object,
            accounts.Object,
            Tenant().Object,
            Company().Object,
            User().Object
        );
        var cmd = new UpdateCompanyBankAccountCommand(missingId, "Alias", AccountingAccountId);

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    // ── Enable/Disable ───────────────────────────────────────────────────

    [Fact]
    public async Task SetActive_false_desactiva_la_cuenta()
    {
        var entity = ExistingAccount();
        var repo = new Mock<ICompanyBankAccountRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, entity.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var handler = new SetCompanyBankAccountActiveHandler(repo.Object, Tenant().Object, User().Object);
        var cmd = new SetCompanyBankAccountActiveCommand(entity.Id, false);

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsActive.Should().BeFalse();
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetActive_true_reactiva_la_cuenta()
    {
        var entity = ExistingAccount();
        entity.Disable(UserId);
        var repo = new Mock<ICompanyBankAccountRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, entity.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var handler = new SetCompanyBankAccountActiveHandler(repo.Object, Tenant().Object, User().Object);
        var cmd = new SetCompanyBankAccountActiveCommand(entity.Id, true);

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsActive.Should().BeTrue();
    }

    // ── Queries ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_existente_retorna_el_dto()
    {
        var entity = ExistingAccount();
        var repo = new Mock<ICompanyBankAccountRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, entity.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var handler = new GetCompanyBankAccountByIdHandler(repo.Object, Tenant().Object);
        var result = await handler.Handle(
            new GetCompanyBankAccountByIdQuery(entity.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.Id.Should().Be(entity.Id);
    }

    [Fact]
    public async Task GetById_inexistente_retorna_NotFound()
    {
        var repo = new Mock<ICompanyBankAccountRepository>();
        var missingId = Guid.NewGuid();
        repo.Setup(r => r.GetByIdAsync(TenantId, missingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompanyBankAccount?)null);

        var handler = new GetCompanyBankAccountByIdHandler(repo.Object, Tenant().Object);
        var result = await handler.Handle(
            new GetCompanyBankAccountByIdQuery(missingId),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task GetList_retorna_los_items_del_repositorio()
    {
        var entity = ExistingAccount();
        var repo = new Mock<ICompanyBankAccountRepository>();
        repo.Setup(r => r.GetListAsync(TenantId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CompanyBankAccount> { entity });

        var handler = new GetCompanyBankAccountListHandler(repo.Object, Tenant().Object);
        var result = await handler.Handle(new GetCompanyBankAccountListQuery(null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(x => x.Id == entity.Id);
    }
}
