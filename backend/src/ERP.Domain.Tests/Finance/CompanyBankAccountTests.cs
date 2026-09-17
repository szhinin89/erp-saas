using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Enums;
using FluentAssertions;

namespace ERP.Domain.Tests.Finance;

public sealed class CompanyBankAccountTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BankId = Guid.NewGuid();
    private static readonly Guid AccountingAccountId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static CompanyBankAccount Create(
        BankAccountType type = BankAccountType.Checking,
        string accountNumber = "2200123456",
        string displayName = "Cuenta corriente Pichincha"
    ) =>
        CompanyBankAccount.Create(
            TenantId,
            CompanyId,
            BankId,
            type,
            accountNumber,
            displayName,
            AccountingAccountId,
            UserId
        );

    [Fact]
    public void Create_con_datos_validos_asigna_todos_los_campos_y_queda_activa()
    {
        var account = Create();

        account.TenantId.Should().Be(TenantId);
        account.CompanyId.Should().Be(CompanyId);
        account.BankId.Should().Be(BankId);
        account.AccountType.Should().Be(BankAccountType.Checking);
        account.AccountNumber.Should().Be("2200123456");
        account.DisplayName.Should().Be("Cuenta corriente Pichincha");
        account.AccountingAccountId.Should().Be(AccountingAccountId);
        account.IsActive.Should().BeTrue();
        account.CreatedBy.Should().Be(UserId);
    }

    [Fact]
    public void Create_recorta_espacios_de_numero_y_alias()
    {
        var account = Create(accountNumber: "  2200123456  ", displayName: "  Cuenta principal  ");

        account.AccountNumber.Should().Be("2200123456");
        account.DisplayName.Should().Be("Cuenta principal");
    }

    [Fact]
    public void Create_sin_banco_lanza()
    {
        var act = () =>
            CompanyBankAccount.Create(
                TenantId,
                CompanyId,
                Guid.Empty,
                BankAccountType.Checking,
                "2200123456",
                "Cuenta",
                AccountingAccountId,
                UserId
            );

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_sin_numero_de_cuenta_lanza()
    {
        var act = () => Create(accountNumber: "   ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_sin_alias_lanza()
    {
        var act = () => Create(displayName: "   ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_sin_cuenta_contable_lanza()
    {
        var act = () =>
            CompanyBankAccount.Create(
                TenantId,
                CompanyId,
                BankId,
                BankAccountType.Checking,
                "2200123456",
                "Cuenta",
                Guid.Empty,
                UserId
            );

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Update_modifica_alias_y_cuenta_contable()
    {
        var account = Create();
        var newAccountingAccountId = Guid.NewGuid();

        account.Update("Nuevo alias", newAccountingAccountId, UserId);

        account.DisplayName.Should().Be("Nuevo alias");
        account.AccountingAccountId.Should().Be(newAccountingAccountId);
        account.UpdatedBy.Should().Be(UserId);
    }

    [Fact]
    public void Update_no_modifica_banco_tipo_ni_numero()
    {
        var account = Create();

        account.Update("Nuevo alias", Guid.NewGuid(), UserId);

        account.BankId.Should().Be(BankId);
        account.AccountType.Should().Be(BankAccountType.Checking);
        account.AccountNumber.Should().Be("2200123456");
    }

    [Fact]
    public void Update_sin_alias_lanza()
    {
        var account = Create();

        var act = () => account.Update("   ", Guid.NewGuid(), UserId);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Update_sin_cuenta_contable_lanza()
    {
        var account = Create();

        var act = () => account.Update("Nuevo alias", Guid.Empty, UserId);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Disable_desactiva_la_cuenta()
    {
        var account = Create();

        account.Disable(UserId);

        account.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Enable_reactiva_la_cuenta()
    {
        var account = Create();
        account.Disable(UserId);

        account.Enable(UserId);

        account.IsActive.Should().BeTrue();
    }
}
