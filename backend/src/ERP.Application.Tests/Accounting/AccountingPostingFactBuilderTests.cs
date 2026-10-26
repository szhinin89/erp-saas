using ERP.Application.Modules.Accounting.Continuity;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.Expenses.Entities;
using ERP.Domain.Modules.Expenses.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Accounting;

/// <summary>
/// ACCOUNTING-CONTINUITY-01 — el hecho contable se reconstruye desde los importes persistidos en la
/// obligación y la configuración VIGENTE en cada intento; una configuración faltante es un
/// resultado fallido (Failed), nunca una excepción.
/// </summary>
public sealed class AccountingPostingFactBuilderTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly Mock<IExpenseCategoryRepository> _categories = new();
    private readonly Mock<IAccountRepository> _accounts = new();

    private AccountingPostingFactBuilder Builder =>
        new(
            _categories.Object,
            _accounts.Object,
            Mock.Of<ERP.Domain.Modules.Caja.Interfaces.ICashRegisterRepository>(),
            Mock.Of<ERP.Domain.Modules.Finance.Interfaces.ICompanyBankAccountRepository>(),
            Mock.Of<ERP.Domain.Modules.Sales.Interfaces.IPaymentMethodAccountRepository>(),
            Mock.Of<IPostingRuleRepository>(),
            Mock.Of<ERP.Domain.Modules.Inventory.Interfaces.IInventoryAdjustmentReasonRepository>(),
            Mock.Of<ERP.Domain.Modules.Inventory.Interfaces.IInventoryAdjustmentReasonAccountRepository>(),
            Mock.Of<ERP.Domain.Modules.Caja.Interfaces.ICashMovementReasonRepository>()
        );

    private static AccountingPosting Obligation(AccountingPostingPayload payload, string module = "Expenses", string fact = "DocumentConfirmed") =>
        AccountingPosting.CreatePending(
            TenantId,
            CompanyId,
            module,
            fact,
            Guid.NewGuid(),
            null,
            new DateOnly(2026, 9, 3),
            new DateOnly(2026, 9, 3),
            AccountingPostingPayload.CurrentVersion,
            payload.Serialize(),
            UserId,
            null
        );

    private ExpenseCategoryNode Subcategory(Guid? accountId)
    {
        var type = ExpenseCategoryNode.CreateType(TenantId, CompanyId, "ADM", "Administrativos", UserId);
        var category = ExpenseCategoryNode.CreateCategory(TenantId, CompanyId, type, "SRV", "Servicios", UserId);
        var sub = ExpenseCategoryNode.CreateSubcategory(TenantId, CompanyId, category, "INT", "Internet", accountId ?? Guid.NewGuid(), UserId);
        if (accountId is null)
            // Dato legado: subcategoría sin cuenta (el dominio actual ya no permite crearla así).
            typeof(ExpenseCategoryNode).GetProperty(nameof(ExpenseCategoryNode.AccountingAccountId))!.SetValue(sub, null);
        _categories.Setup(r => r.GetByIdAsync(TenantId, sub.Id, It.IsAny<CancellationToken>())).ReturnsAsync(sub);
        return sub;
    }

    private Account ExpenseAccount(bool active = true, AccountType type = AccountType.Expense)
    {
        var account = Account.Create(TenantId, CompanyId, AccountCode.Create("6.1.01.001"), "Internet", null,
            type, AccountNature.Debit, allowsPosting: true, createdBy: UserId);
        if (!active)
            account.Disable(UserId);
        _accounts.Setup(r => r.GetByIdAsync(TenantId, CompanyId, account.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        return account;
    }

    [Fact]
    public async Task Linea_de_gasto_resuelve_la_cuenta_vigente_de_su_subcategoria()
    {
        var account = ExpenseAccount();
        var sub = Subcategory(account.Id);
        var lineId = Guid.NewGuid();
        var payload = new AccountingPostingPayload(100m, 15m, 0m, 0m, 115m,
            ExpenseLines: [new AccountingPostingExpenseLine(lineId, sub.Id, 100m, "Internet")]);

        var result = await Builder.BuildAsync(Obligation(payload), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        var fact = result.Value!;
        fact.SourceModule.Should().Be("Expenses");
        fact.EntryDate.Should().Be(new DateOnly(2026, 9, 3));
        fact.Subtotal.Should().Be(100m);
        fact.TotalVat.Should().Be(15m);
        fact.GrandTotal.Should().Be(115m);
        var allocation = fact.Allocations.Should().ContainSingle().Which;
        allocation.AccountingAccountId.Should().Be(account.Id);
        allocation.Amount.Should().Be(100m);
        allocation.Nature.Should().Be(AccountNature.Debit);
        allocation.SourceLineId.Should().Be(lineId);
    }

    [Fact]
    public async Task Subcategoria_sin_cuenta_es_resultado_fallido_no_excepcion()
    {
        var sub = Subcategory(null);
        var payload = new AccountingPostingPayload(100m, 0m, 0m, 0m, 100m,
            ExpenseLines: [new AccountingPostingExpenseLine(Guid.NewGuid(), sub.Id, 100m, "Internet")]);

        var result = await Builder.BuildAsync(Obligation(payload), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(AccountingPostingFactBuilder.ExpenseAccountNotConfiguredCode);
        result.Error.Should().Contain("Internet");
    }

    [Theory]
    [InlineData(false, AccountType.Expense)]
    [InlineData(true, AccountType.Asset)]
    public async Task Cuenta_inactiva_o_no_de_gasto_es_resultado_fallido(bool active, AccountType type)
    {
        var account = ExpenseAccount(active, type);
        var sub = Subcategory(account.Id);
        var payload = new AccountingPostingPayload(100m, 0m, 0m, 0m, 100m,
            ExpenseLines: [new AccountingPostingExpenseLine(Guid.NewGuid(), sub.Id, 100m, "Internet")]);

        var result = await Builder.BuildAsync(Obligation(payload), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(AccountingPostingFactBuilder.ExpenseAccountInvalidCode);
    }

    [Fact]
    public async Task Linea_de_importe_cero_no_genera_allocation_ni_excepcion()
    {
        var account = ExpenseAccount();
        var sub = Subcategory(account.Id);
        var payload = new AccountingPostingPayload(100m, 0m, 0m, 0m, 100m,
            ExpenseLines:
            [
                new AccountingPostingExpenseLine(Guid.NewGuid(), sub.Id, 100m, "Internet"),
                new AccountingPostingExpenseLine(Guid.NewGuid(), Guid.NewGuid(), 0m, "Cortesía"),
            ]);

        var result = await Builder.BuildAsync(Obligation(payload), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Allocations.Should().ContainSingle();
        result.Value.Subtotal.Should().Be(100m);
    }

    [Fact]
    public async Task Hecho_sin_lineas_de_gasto_conserva_importes_del_payload()
    {
        var payload = new AccountingPostingPayload(103m, 15m, 1m, 2m, 118.48m, TotalIrbpnr: 0.48m);

        var result = await Builder.BuildAsync(Obligation(payload, "Purchases", "InvoiceReceived"), CancellationToken.None);

        var fact = result.Value!;
        (fact.Subtotal, fact.TotalVat, fact.TotalIce, fact.TotalDiscount, fact.GrandTotal, fact.TotalIrbpnr)
            .Should().Be((103m, 15m, 1m, 2m, 118.48m, (decimal?)0.48m));
        fact.Allocations.Should().BeNull();
    }

    [Fact]
    public async Task Version_de_payload_no_soportada_es_resultado_fallido()
    {
        var posting = AccountingPosting.CreatePending(TenantId, CompanyId, "Purchases", "InvoiceReceived",
            Guid.NewGuid(), null, new DateOnly(2026, 9, 3), new DateOnly(2026, 9, 3), 99, "{}", UserId, null);

        var result = await Builder.BuildAsync(posting, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(AccountingPostingFactBuilder.PayloadUnsupportedCode);
    }

    [Fact]
    public async Task InventoryAdjustment_motivo_de_otra_Company_no_resuelve_cuenta()
    {
        // D-1 — el motivo es dato COMPANY: solo la Company dueña resuelve su cuenta; otra Company falla
        // cerrado aunque tenga configuración para ese Id.
        var companyB = Guid.NewGuid();
        var reason = ERP.Domain.Modules.Inventory.Entities.InventoryAdjustmentReason.Create(
            TenantId, CompanyId, "SOBRANTE", "Sobrante", "Ingreso", false, 1, UserId);
        var accountA = Account.Create(TenantId, CompanyId, AccountCode.Create("4.2.01.001"), "Sobrantes A", null,
            AccountType.Income, AccountNature.Credit, allowsPosting: true, createdBy: UserId);
        var accountB = Account.Create(TenantId, companyB, AccountCode.Create("4.2.01.001"), "Sobrantes B", null,
            AccountType.Income, AccountNature.Credit, allowsPosting: true, createdBy: UserId);
        var reasons = new Mock<ERP.Domain.Modules.Inventory.Interfaces.IInventoryAdjustmentReasonRepository>();
        reasons.Setup(r => r.GetByIdAsync(TenantId, reason.Id, It.IsAny<CancellationToken>())).ReturnsAsync(reason);
        var links = new Mock<ERP.Domain.Modules.Inventory.Interfaces.IInventoryAdjustmentReasonAccountRepository>();
        links.Setup(r => r.GetAsync(TenantId, CompanyId, reason.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ERP.Domain.Modules.Inventory.Entities.InventoryAdjustmentReasonAccount.Create(
                TenantId, CompanyId, reason.Id, accountA.Id, UserId));
        links.Setup(r => r.GetAsync(TenantId, companyB, reason.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ERP.Domain.Modules.Inventory.Entities.InventoryAdjustmentReasonAccount.Create(
                TenantId, companyB, reason.Id, accountB.Id, UserId));
        _accounts.Setup(r => r.GetByIdAsync(TenantId, CompanyId, accountA.Id, It.IsAny<CancellationToken>())).ReturnsAsync(accountA);
        _accounts.Setup(r => r.GetByIdAsync(TenantId, companyB, accountB.Id, It.IsAny<CancellationToken>())).ReturnsAsync(accountB);
        var builder = new AccountingPostingFactBuilder(_categories.Object, _accounts.Object,
            Mock.Of<ERP.Domain.Modules.Caja.Interfaces.ICashRegisterRepository>(),
            Mock.Of<ERP.Domain.Modules.Finance.Interfaces.ICompanyBankAccountRepository>(),
            Mock.Of<ERP.Domain.Modules.Sales.Interfaces.IPaymentMethodAccountRepository>(),
            Mock.Of<IPostingRuleRepository>(), reasons.Object, links.Object,
            Mock.Of<ERP.Domain.Modules.Caja.Interfaces.ICashMovementReasonRepository>());

        AccountingPosting AdjustmentIn(Guid companyId) => AccountingPosting.CreatePending(TenantId, companyId,
            "Inventory", "StockAdjustmentIn", Guid.NewGuid(), "AJ-1", new DateOnly(2026, 10, 17), new DateOnly(2026, 10, 17),
            AccountingPostingPayload.CurrentVersion,
            new AccountingPostingPayload(10m, 0m, 0m, 0m, 10m, Destinations:
                [new AccountingPostingDestination(AccountingPostingDestination.InventoryAdjustmentReason, reason.Id, 10m, AccountNature.Credit)])
                .Serialize(),
            UserId, null);

        var factA = await builder.BuildAsync(AdjustmentIn(CompanyId), CancellationToken.None);
        var factB = await builder.BuildAsync(AdjustmentIn(companyB), CancellationToken.None);

        factA.IsSuccess.Should().BeTrue(factA.Error);
        factA.Value!.Allocations.Should().ContainSingle(a => a.AccountingAccountId == accountA.Id && a.Nature == AccountNature.Credit && a.Amount == 10m);
        factB.IsSuccess.Should().BeFalse();
        factB.Code.Should().Be(AccountingPostingFactBuilder.DestinationNotFoundCode);
    }
}
