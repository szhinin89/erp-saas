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
/// de padre (existencia/ciclos), UpdateAccountCommand, GetAccountByCodeQuery y cálculo de
/// Level/ParentAccountCode/ParentAccountName en el DTO.
/// ACCOUNTING-CHART-INTEGRITY-01 — I1–I4, subcuentas bajo cuentas con movimiento o con asientos
/// publicados, referencias vigentes de las ocho familias y orden lock → lecturas.
/// </summary>
public sealed class AccountUseCasesTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid CreatedBy = Guid.NewGuid();

    private static Account NewAccount(
        string code,
        string name,
        Guid? parentId = null,
        bool allowsPosting = true,
        bool active = true
    )
    {
        var account = Account.Create(
            TenantId,
            CompanyId,
            AccountCode.Create(code),
            name,
            parentId,
            AccountType.Asset,
            AccountNature.Debit,
            allowsPosting,
            CreatedBy
        );
        if (!active)
            account.Disable(CreatedBy);
        return account;
    }

    private sealed class Mocks
    {
        public Mock<IAccountRepository> Accounts { get; } = new();
        public Mock<IAccountUsageReader> Usage { get; } = new();
        public Mock<IUnitOfWork> Uow { get; } = new();
        public Mock<ICurrentTenant> Tenant { get; } = new();
        public Mock<ICurrentCompany> Company { get; } = new();
        public Mock<ICurrentUser> User { get; } = new();
        public List<string> Calls { get; } = new();

        public Mocks()
        {
            Tenant.Setup(t => t.TenantId).Returns(TenantId);
            Company.Setup(c => c.CompanyId).Returns(CompanyId);
            User.Setup(u => u.UserId).Returns(CreatedBy);
            Uow.Setup(u =>
                    u.ExecuteInTransactionAsync(
                        It.IsAny<Func<CancellationToken, Task>>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .Returns<Func<CancellationToken, Task>, CancellationToken>(
                    async (operation, ct) =>
                    {
                        Calls.Add("tx");
                        await operation(ct);
                    }
                );
            Accounts
                .Setup(r => r.AcquireChartLockAsync(CompanyId, It.IsAny<CancellationToken>()))
                .Callback(() => Calls.Add("lock"))
                .Returns(Task.CompletedTask);
            Usage
                .Setup(r =>
                    r.GetConfigurationReferencesAsync(
                        It.IsAny<Guid>(),
                        It.IsAny<Guid>(),
                        It.IsAny<Guid>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(Array.Empty<AccountConfigurationReference>());
        }

        public void Chart(params Account[] accounts)
        {
            Accounts
                .Setup(r => r.GetByCompanyAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
                .Callback(() => Calls.Add("read"))
                .ReturnsAsync(accounts.ToList());
            foreach (var account in accounts)
                Accounts
                    .Setup(r =>
                        r.GetByIdAsync(TenantId, CompanyId, account.Id, It.IsAny<CancellationToken>())
                    )
                    .Callback(() => Calls.Add("read"))
                    .ReturnsAsync(account);
        }

        public void References(Guid accountId, params AccountConfigurationReference[] references) =>
            Usage
                .Setup(r =>
                    r.GetConfigurationReferencesAsync(
                        TenantId,
                        CompanyId,
                        accountId,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(references);

        public void PostedLines(Guid accountId) =>
            Usage
                .Setup(r =>
                    r.HasPostedJournalLinesAsync(
                        TenantId,
                        CompanyId,
                        accountId,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(true);

        public CreateAccountHandler Create() =>
            new(
                Accounts.Object,
                Usage.Object,
                Uow.Object,
                Tenant.Object,
                Company.Object,
                User.Object,
                Mock.Of<ERP.Application.Common.Persistence.IDatabaseExceptionTranslator>()
            );

        public UpdateAccountHandler Update() =>
            new(Accounts.Object, Usage.Object, Uow.Object, Tenant.Object, Company.Object, User.Object);

        public EnableAccountHandler Enable() =>
            new(Accounts.Object, Uow.Object, Tenant.Object, Company.Object, User.Object);

        public DisableAccountHandler Disable() =>
            new(Accounts.Object, Usage.Object, Uow.Object, Tenant.Object, Company.Object, User.Object);
    }

    private static CreateAccountCommand CreateCommand(string code, Guid? parentId, bool allowsPosting = true) =>
        new(code, "Nueva", parentId, AccountType.Asset, AccountNature.Debit, allowsPosting);

    public static TheoryData<AccountReferenceFamily, string> ReferenceFamilies() =>
        new()
        {
            { AccountReferenceFamily.PostingRule, "una Regla de Contabilización activa (Sales/InvoiceIssued)" },
            { AccountReferenceFamily.PaymentMethod, "la forma de pago 'Sales/InvoiceIssued'" },
            { AccountReferenceFamily.CashRegister, "la caja 'Sales/InvoiceIssued'" },
            { AccountReferenceFamily.CashMovementReason, "el motivo de caja 'Sales/InvoiceIssued'" },
            { AccountReferenceFamily.ExpenseCategory, "la categoría de gasto 'Sales/InvoiceIssued'" },
            { AccountReferenceFamily.CompanyBankAccount, "la cuenta bancaria 'Sales/InvoiceIssued'" },
            { AccountReferenceFamily.InventoryAdjustmentReason, "el motivo de ajuste de inventario 'Sales/InvoiceIssued'" },
        };

    // ── Casos existentes (regresión) ────────────────────────────────────

    [Fact]
    public async Task GetAccounts_calcula_Level_y_datos_del_padre()
    {
        var root = NewAccount("1", "Activo");
        var child = NewAccount("1.1", "Activo Corriente", root.Id);
        var grandchild = NewAccount("1.1.01", "Caja", child.Id);

        var m = new Mocks();
        m.Chart(root, child, grandchild);

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
        m.Chart();

        var result = await m.Create().Handle(CreateCommand("1.1", Guid.NewGuid()), CancellationToken.None);

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
        var unrelated = NewAccount("2", "Pasivo", allowsPosting: false);
        var m = new Mocks();
        m.Chart(unrelated);

        var result = await m.Create().Handle(CreateCommand("1.1", unrelated.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        m.Accounts.Verify(r => r.AddAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAccount_rechaza_codigo_raiz_con_padre_indicado()
    {
        var m = new Mocks();
        m.Chart();

        var result = await m.Create().Handle(CreateCommand("9", Guid.NewGuid(), false), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAccount_rechaza_padre_que_no_coincide_con_el_codigo()
    {
        var unrelated = NewAccount("2", "Pasivo");
        var account = NewAccount("1.1", "Activo Corriente");
        var m = new Mocks();
        m.Chart(unrelated, account);

        var result = await m.Update().Handle(
            new UpdateAccountCommand(account.Id, "Activo Corriente", unrelated.Id, false),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        account.ParentAccountId.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAccount_rechaza_quitar_padre_de_cuenta_con_codigo_compuesto()
    {
        var parent = NewAccount("1", "Activo", allowsPosting: false);
        var account = NewAccount("1.1", "Activo Corriente", parent.Id);
        var m = new Mocks();
        m.Chart(parent, account);

        // Intenta dejar la cuenta "1.1" sin padre — inválido, su código exige padre "1".
        var result = await m.Update().Handle(
            new UpdateAccountCommand(account.Id, "Activo Corriente", null, false),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAccount_rechaza_ciclo_al_reparentar()
    {
        var root = NewAccount("1", "Activo", allowsPosting: false);
        var child = NewAccount("1.1", "Activo Corriente", root.Id);
        var m = new Mocks();
        m.Chart(root, child);

        // Intenta poner a "root" como hijo de su propio hijo "child" — ciclo.
        var result = await m.Update().Handle(
            new UpdateAccountCommand(root.Id, "Activo", child.Id, false),
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
        m.Chart(account);

        var result = await m.Update().Handle(
            new UpdateAccountCommand(account.Id, "Activo", account.Id, true),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAccount_correcto_actualiza_nombre_padre_y_allowsPosting()
    {
        var parent = NewAccount("1", "Activo", allowsPosting: false);
        var account = NewAccount("1.1", "Viejo nombre");
        var m = new Mocks();
        m.Chart(parent, account);

        var result = await m.Update().Handle(
            new UpdateAccountCommand(account.Id, "Nuevo nombre", parent.Id, false),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("Nuevo nombre");
        result.Value.ParentAccountId.Should().Be(parent.Id);
        result.Value.AllowsPosting.Should().BeFalse();
        m.Accounts.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        m.Usage.Verify(
            r => r.GetConfigurationReferencesAsync(TenantId, CompanyId, account.Id, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task DisableAccount_bloqueado_si_regla_activa_la_referencia()
    {
        var account = NewAccount("4.1.01", "Ventas");
        var m = new Mocks();
        m.Chart(account);
        m.References(account.Id, new AccountConfigurationReference(AccountReferenceFamily.PostingRule, "Sales/InvoiceIssued"));

        var result = await m.Disable().Handle(new DisableAccountCommand(account.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.Conflict);
        result.Error.Should().StartWith(
            "No se puede desactivar: la cuenta está referenciada por una Regla de Contabilización activa"
        );
        account.IsActive.Should().BeTrue();
        m.Accounts.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DisableAccount_permite_desactivar_si_ninguna_regla_activa_la_usa()
    {
        var account = NewAccount("4.1.01", "Ventas");
        var m = new Mocks();
        m.Chart(account);

        var result = await m.Disable().Handle(new DisableAccountCommand(account.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        account.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task GetAccountByCode_delega_en_FindByCodeAsync()
    {
        var account = NewAccount("1.1.01", "Caja");
        var m = new Mocks();
        m.Accounts.Setup(r => r.FindByCodeAsync(TenantId, CompanyId, "1.1.01", It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        m.Chart(account);

        var handler = new GetAccountByCodeHandler(m.Accounts.Object, m.Tenant.Object, m.Company.Object);
        var result = await handler.Handle(new GetAccountByCodeQuery("1.1.01"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Id.Should().Be(account.Id);
    }

    // ── Crear subcuentas ────────────────────────────────────────────────

    [Fact]
    public async Task CreateAccount_rechaza_subcuenta_bajo_cuenta_que_admite_movimiento()
    {
        var parent = NewAccount("1.1.01", "Caja");
        var m = new Mocks();
        m.Chart(parent);

        var result = await m.Create().Handle(CreateCommand("1.1.01.001", parent.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        result.Error.Should().Contain("'1.1.01' admite movimientos");
        m.Accounts.Verify(r => r.AddAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAccount_rechaza_subcuenta_bajo_cuenta_con_asientos_publicados()
    {
        // Agrupadora sin movimiento hoy, pero con asientos publicados (incluidos reversos) de antes.
        var parent = NewAccount("1.1.01", "Caja", allowsPosting: false);
        var m = new Mocks();
        m.Chart(parent);
        m.PostedLines(parent.Id);

        var result = await m.Create().Handle(CreateCommand("1.1.01.001", parent.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.Conflict);
        result.Error.Should().Contain("tiene asientos publicados");
        m.Accounts.Verify(r => r.AddAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAccount_permite_subcuenta_bajo_agrupadora_sin_asientos_y_toma_el_lock_primero()
    {
        var parent = NewAccount("1.1", "Activo Corriente", allowsPosting: false);
        var m = new Mocks();
        m.Chart(parent);
        m.Accounts.Setup(r => r.FindByCodeAsync(TenantId, CompanyId, "1.1.01", It.IsAny<CancellationToken>()))
            .Callback(() => m.Calls.Add("read"))
            .ReturnsAsync((Account?)null);

        var result = await m.Create().Handle(CreateCommand("1.1.01", parent.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        m.Calls.Take(2).Should().Equal("tx", "lock");
        m.Calls.Skip(2).Should().OnlyContain(c => c == "read");
        m.Usage.Verify(
            r => r.HasPostedJournalLinesAsync(TenantId, CompanyId, parent.Id, It.IsAny<CancellationToken>()),
            Times.Once
        );
        m.Accounts.Verify(r => r.AddAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAccount_I4_rechaza_cuenta_bajo_padre_inactivo()
    {
        var parent = NewAccount("1.1", "Activo Corriente", allowsPosting: false, active: false);
        var m = new Mocks();
        m.Chart(parent);

        var result = await m.Create().Handle(CreateCommand("1.1.01", parent.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.Conflict);
        result.Error.Should().Be("No se puede crear la cuenta: una cuenta superior está inactiva. Actívela primero.");
        parent.IsActive.Should().BeFalse("no se reactiva el padre automáticamente");
        m.Accounts.Verify(r => r.AddAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()), Times.Never);
        m.Accounts.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAccount_I4_rechaza_cuenta_con_abuelo_inactivo()
    {
        var root = NewAccount("1", "Activo", allowsPosting: false, active: false);
        var parent = NewAccount("1.1", "Activo Corriente", root.Id, allowsPosting: false);
        var m = new Mocks();
        m.Chart(root, parent);

        var result = await m.Create().Handle(CreateCommand("1.1.01", parent.Id), CancellationToken.None);

        result.Code.Should().Be(ApiResponseCodes.Common.Conflict);
        root.IsActive.Should().BeFalse();
        m.Accounts.Verify(r => r.AddAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAccount_I4_permite_cuenta_con_toda_la_cadena_activa()
    {
        var root = NewAccount("1", "Activo", allowsPosting: false);
        var parent = NewAccount("1.1", "Activo Corriente", root.Id, allowsPosting: false);
        var m = new Mocks();
        m.Chart(root, parent);

        var result = await m.Create().Handle(CreateCommand("1.1.01", parent.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsActive.Should().BeTrue();
        result.Value.Level.Should().Be(2);
        m.Accounts.Verify(r => r.AddAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// El padre se resuelve solo dentro del Plan de Cuentas del tenant y la empresa del contexto: un
    /// padre inactivo de otra empresa no existe para esta operación (rechazo «padre inexistente»),
    /// sin evaluar asientos ni referencias fuera del ámbito.
    /// </summary>
    [Fact]
    public async Task CreateAccount_no_resuelve_padres_fuera_del_tenant_y_la_empresa()
    {
        var foreignParent = Account.Create(Guid.NewGuid(), Guid.NewGuid(), AccountCode.Create("1.1"), "Ajena",
            null, AccountType.Asset, AccountNature.Debit, false, CreatedBy);
        foreignParent.Disable(CreatedBy);
        var m = new Mocks();
        m.Chart();

        var result = await m.Create().Handle(CreateCommand("1.1.01", foreignParent.Id), CancellationToken.None);

        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        result.Error.Should().Be("La cuenta padre indicada no existe en esta empresa.");
        m.Accounts.Verify(r => r.GetByCompanyAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()), Times.Once);
        m.Usage.Verify(
            r => r.HasPostedJournalLinesAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task CreateAccount_raiz_no_consulta_asientos()
    {
        var m = new Mocks();
        m.Chart();

        var result = await m.Create().Handle(CreateCommand("9", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        m.Usage.Verify(
            r => r.HasPostedJournalLinesAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    // ── I1 / I2 al editar ───────────────────────────────────────────────

    [Fact]
    public async Task UpdateAccount_I1_rechaza_admitir_movimiento_con_subcuenta_inactiva()
    {
        var parent = NewAccount("1.1", "Activo Corriente", allowsPosting: false);
        var child = NewAccount("1.1.01", "Caja", parent.Id, active: false);
        var root = NewAccount("1", "Activo", allowsPosting: false);
        parent.UpdateParent(root.Id, CreatedBy);
        var m = new Mocks();
        m.Chart(root, parent, child);

        var result = await m.Update().Handle(
            new UpdateAccountCommand(parent.Id, "Activo Corriente", root.Id, true),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        parent.AllowsPosting.Should().BeFalse();
        m.Accounts.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAccount_I1_permite_renombrar_agrupadora_sin_movimiento()
    {
        var root = NewAccount("1", "Activo", allowsPosting: false);
        var child = NewAccount("1.1", "Activo Corriente", root.Id);
        var m = new Mocks();
        m.Chart(root, child);

        var result = await m.Update().Handle(
            new UpdateAccountCommand(root.Id, "Activos", null, false),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        root.Name.Should().Be("Activos");
        m.Usage.Verify(
            r => r.GetConfigurationReferencesAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Theory]
    [MemberData(nameof(ReferenceFamilies))]
    public async Task UpdateAccount_I2_rechaza_quitar_movimiento_si_una_familia_la_referencia(
        AccountReferenceFamily family,
        string expected
    )
    {
        var account = NewAccount("1", "Caja");
        var m = new Mocks();
        m.Chart(account);
        m.References(account.Id, new AccountConfigurationReference(family, "Sales/InvoiceIssued"));

        var result = await m.Update().Handle(
            new UpdateAccountCommand(account.Id, "Caja", null, false),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.Conflict);
        result.Error.Should().Be(
            $"No se puede quitar 'Permite movimiento': la cuenta está referenciada por {expected}."
        );
        account.AllowsPosting.Should().BeTrue();
        m.Accounts.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static (Mocks Mocks, Account Parent, Account Result) ResultAccountChart()
    {
        var equity = NewAccount("3", "Patrimonio", allowsPosting: false);
        var capital = NewAccount("3.1", "Capital", equity.Id, allowsPosting: false);
        var results = NewAccount("3.1.03", "Resultados", capital.Id, allowsPosting: false);
        var result = NewAccount("3.1.03.001", "Resultado del ejercicio", results.Id);
        var m = new Mocks();
        m.Chart(equity, capital, results, result);
        return (m, results, result);
    }

    [Fact]
    public async Task UpdateAccount_I2_protege_la_cuenta_de_resultado_del_ejercicio()
    {
        var (m, parent, account) = ResultAccountChart();

        var outcome = await m.Update().Handle(
            new UpdateAccountCommand(account.Id, "Resultado del ejercicio", parent.Id, false),
            CancellationToken.None
        );

        outcome.Code.Should().Be(ApiResponseCodes.Common.Conflict);
        outcome.Error.Should().Contain("el cierre anual (cuenta de Resultado del ejercicio)");
        account.AllowsPosting.Should().BeTrue();
    }

    [Fact]
    public async Task DisableAccount_I2_protege_la_cuenta_de_resultado_del_ejercicio()
    {
        var (m, _, account) = ResultAccountChart();

        var outcome = await m.Disable().Handle(new DisableAccountCommand(account.Id), CancellationToken.None);

        outcome.Code.Should().Be(ApiResponseCodes.Common.Conflict);
        outcome.Error.Should().Contain("el cierre anual (cuenta de Resultado del ejercicio)");
        account.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAccount_I2_lista_hasta_tres_referencias_y_resume_el_resto()
    {
        var account = NewAccount("1", "Caja");
        var m = new Mocks();
        m.Chart(account);
        m.References(
            account.Id,
            new(AccountReferenceFamily.CashRegister, "Caja 1"),
            new(AccountReferenceFamily.CashRegister, "Caja 2"),
            new(AccountReferenceFamily.CashRegister, "Caja 3"),
            new(AccountReferenceFamily.CashRegister, "Caja 4"),
            new(AccountReferenceFamily.CashRegister, "Caja 5")
        );

        var result = await m.Update().Handle(
            new UpdateAccountCommand(account.Id, "Caja", null, false),
            CancellationToken.None
        );

        result.Error.Should().EndWith("la caja 'Caja 1', la caja 'Caja 2', la caja 'Caja 3' y 2 más.");
    }

    // ── I3 / I2 al desactivar ───────────────────────────────────────────

    [Fact]
    public async Task DisableAccount_I3_rechaza_con_descendiente_activo_de_segundo_nivel()
    {
        var root = NewAccount("1", "Activo", allowsPosting: false);
        var child = NewAccount("1.1", "Activo Corriente", root.Id, allowsPosting: false, active: false);
        var grandchild = NewAccount("1.1.01", "Caja", child.Id);
        var m = new Mocks();
        m.Chart(root, child, grandchild);

        var result = await m.Disable().Handle(new DisableAccountCommand(root.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.Conflict);
        result.Error.Should().Contain("subcuentas activas");
        root.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task DisableAccount_I3_permite_con_descendientes_inactivos()
    {
        var root = NewAccount("1", "Activo", allowsPosting: false);
        var child = NewAccount("1.1", "Activo Corriente", root.Id, allowsPosting: false, active: false);
        var grandchild = NewAccount("1.1.01", "Caja", child.Id, active: false);
        var m = new Mocks();
        m.Chart(root, child, grandchild);

        var result = await m.Disable().Handle(new DisableAccountCommand(root.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        root.IsActive.Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(ReferenceFamilies))]
    public async Task DisableAccount_I2_rechaza_si_una_familia_la_referencia(
        AccountReferenceFamily family,
        string expected
    )
    {
        var account = NewAccount("1", "Caja");
        var m = new Mocks();
        m.Chart(account);
        m.References(account.Id, new AccountConfigurationReference(family, "Sales/InvoiceIssued"));

        var result = await m.Disable().Handle(new DisableAccountCommand(account.Id), CancellationToken.None);

        result.Code.Should().Be(ApiResponseCodes.Common.Conflict);
        result.Error.Should().Be($"No se puede desactivar: la cuenta está referenciada por {expected}.");
        account.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task DisableAccount_consulta_referencias_con_el_tenant_y_la_empresa_del_contexto_tras_el_lock()
    {
        var account = NewAccount("1", "Caja");
        var m = new Mocks();
        m.Chart(account);

        await m.Disable().Handle(new DisableAccountCommand(account.Id), CancellationToken.None);

        m.Calls.Take(2).Should().Equal("tx", "lock");
        m.Accounts.Verify(r => r.AcquireChartLockAsync(CompanyId, It.IsAny<CancellationToken>()), Times.Once);
        m.Usage.Verify(
            r => r.GetConfigurationReferencesAsync(TenantId, CompanyId, account.Id, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    // ── I4 al activar ───────────────────────────────────────────────────

    [Fact]
    public async Task EnableAccount_I4_rechaza_con_ancestro_inactivo_de_segundo_nivel()
    {
        var root = NewAccount("1", "Activo", allowsPosting: false, active: false);
        var child = NewAccount("1.1", "Activo Corriente", root.Id, allowsPosting: false);
        var grandchild = NewAccount("1.1.01", "Caja", child.Id, active: false);
        var m = new Mocks();
        m.Chart(root, child, grandchild);

        var result = await m.Enable().Handle(new EnableAccountCommand(grandchild.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.Conflict);
        grandchild.IsActive.Should().BeFalse();
        m.Accounts.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnableAccount_I4_permite_con_ancestros_activos_y_toma_el_lock_primero()
    {
        var root = NewAccount("1", "Activo", allowsPosting: false);
        var child = NewAccount("1.1", "Caja", root.Id, active: false);
        var m = new Mocks();
        m.Chart(root, child);

        var result = await m.Enable().Handle(new EnableAccountCommand(child.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        child.IsActive.Should().BeTrue();
        m.Calls.Take(2).Should().Equal("tx", "lock");
    }

    [Fact]
    public async Task Update_toma_el_lock_antes_de_leer()
    {
        var account = NewAccount("1", "Caja");
        var m = new Mocks();
        m.Chart(account);

        await m.Update().Handle(new UpdateAccountCommand(account.Id, "Caja", null, true), CancellationToken.None);

        m.Calls.Take(2).Should().Equal("tx", "lock");
        m.Calls.Skip(2).Should().NotBeEmpty().And.OnlyContain(c => c == "read");
    }
}
