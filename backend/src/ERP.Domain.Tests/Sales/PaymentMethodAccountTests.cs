using ERP.Domain.Modules.Sales.Entities;
using FluentAssertions;

namespace ERP.Domain.Tests.Sales;

/// <summary>SALES-TRANSFER-ACCOUNTING-CASH-VS-BANK-01</summary>
public sealed class PaymentMethodAccountTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid PaymentMethodId = Guid.NewGuid();
    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void Create_asigna_los_campos_correctamente()
    {
        var link = PaymentMethodAccount.Create(
            TenantId,
            CompanyId,
            PaymentMethodId,
            AccountId,
            UserId
        );

        link.TenantId.Should().Be(TenantId);
        link.CompanyId.Should().Be(CompanyId);
        link.PaymentMethodId.Should().Be(PaymentMethodId);
        link.AccountingAccountId.Should().Be(AccountId);
    }

    [Fact]
    public void Create_rechaza_PaymentMethodId_vacio()
    {
        var act = () =>
            PaymentMethodAccount.Create(TenantId, CompanyId, Guid.Empty, AccountId, UserId);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_rechaza_AccountingAccountId_vacio()
    {
        var act = () =>
            PaymentMethodAccount.Create(TenantId, CompanyId, PaymentMethodId, Guid.Empty, UserId);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ChangeAccount_actualiza_la_cuenta()
    {
        var link = PaymentMethodAccount.Create(
            TenantId,
            CompanyId,
            PaymentMethodId,
            AccountId,
            UserId
        );
        var newAccountId = Guid.NewGuid();

        link.ChangeAccount(newAccountId, UserId);

        link.AccountingAccountId.Should().Be(newAccountId);
    }

    [Fact]
    public void ChangeAccount_rechaza_cuenta_vacia()
    {
        var link = PaymentMethodAccount.Create(
            TenantId,
            CompanyId,
            PaymentMethodId,
            AccountId,
            UserId
        );

        var act = () => link.ChangeAccount(Guid.Empty, UserId);

        act.Should().Throw<ArgumentException>();
    }
}
