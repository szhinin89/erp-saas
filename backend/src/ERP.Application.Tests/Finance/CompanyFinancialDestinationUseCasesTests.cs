using ERP.Application.Common;
using ERP.Application.Modules.Finance.UseCases;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Finance.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Finance;

/// <summary>
/// P0-02 Fase 4 (reejecución) — pruebas de los 4 casos de uso limitados de
/// <see cref="CompanyFinancialDestination"/>. No vuelve a probar internamente
/// <c>CompanyFinancialDestinationAuditHandler</c> (ya cubierto por la Remediación Técnica
/// Limitada 01) — aquí solo se verifica que cada handler invoca el método de dominio correcto,
/// que es quien levanta el evento consumido por ese soporte ya aceptado.
/// </summary>
public sealed class CompanyFinancialDestinationUseCasesTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid AccountingAccountId = Guid.NewGuid();
    private static readonly Guid CashRegisterId = Guid.NewGuid();

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

    private static CashRegister ActiveCashRegister(Guid companyId) =>
        CashRegister.Create(
            TenantId,
            companyId,
            Guid.NewGuid(),
            "CAJA-001",
            "Caja principal",
            UserId
        );

    private static CompanyFinancialDestination BankDestination() =>
        CompanyFinancialDestination.Create(
            TenantId,
            CompanyId,
            "BANCO-001",
            "Cuenta corriente Pichincha",
            FinancialDestinationTypeCode.BankAccount,
            AccountingAccountId,
            "USD",
            UserId,
            bankInstitutionCode: "PICHINCHA",
            bankAccountIdentifierNormalized: "2200123456"
        );

    // ── Create ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_banco_valido_persiste_una_sola_vez_y_retorna_el_dto()
    {
        var repo = new Mock<ICompanyFinancialDestinationRepository>();
        var accounts = new Mock<IAccountRepository>();
        var cashRegisters = new Mock<ICashRegisterRepository>();
        accounts
            .Setup(a => a.GetByIdAsync(TenantId, CompanyId, AccountingAccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveAccount());
        var handler = new CreateCompanyFinancialDestinationHandler(
            repo.Object,
            accounts.Object,
            cashRegisters.Object,
            Tenant().Object,
            Company().Object,
            User().Object
        );
        var cmd = new CreateCompanyFinancialDestinationCommand(
            "BANCO-001",
            "Cuenta corriente Pichincha",
            FinancialDestinationTypeCode.BankAccount,
            AccountingAccountId,
            "USD",
            null,
            "PICHINCHA",
            "2200123456"
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Code.Should().Be("BANCO-001");
        result.Value.DestinationTypeCode.Should().Be(nameof(FinancialDestinationTypeCode.BankAccount));
        repo.Verify(
            r => r.AddAsync(It.IsAny<CompanyFinancialDestination>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_caja_valida_con_caja_de_la_misma_compania_persiste_una_sola_vez()
    {
        var repo = new Mock<ICompanyFinancialDestinationRepository>();
        var accounts = new Mock<IAccountRepository>();
        var cashRegisters = new Mock<ICashRegisterRepository>();
        accounts
            .Setup(a => a.GetByIdAsync(TenantId, CompanyId, AccountingAccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveAccount());
        cashRegisters
            .Setup(c => c.GetByIdAsync(TenantId, CashRegisterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveCashRegister(CompanyId));
        var handler = new CreateCompanyFinancialDestinationHandler(
            repo.Object,
            accounts.Object,
            cashRegisters.Object,
            Tenant().Object,
            Company().Object,
            User().Object
        );
        var cmd = new CreateCompanyFinancialDestinationCommand(
            "CAJA-001",
            "Caja principal",
            FinancialDestinationTypeCode.CashRegister,
            AccountingAccountId,
            "USD",
            CashRegisterId,
            null,
            null
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.CashRegisterId.Should().Be(CashRegisterId);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_banco_sin_institucion_bancaria_rechaza_por_regla_de_dominio_SC_022_y_no_persiste()
    {
        var repo = new Mock<ICompanyFinancialDestinationRepository>();
        var accounts = new Mock<IAccountRepository>();
        accounts
            .Setup(a => a.GetByIdAsync(TenantId, CompanyId, AccountingAccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveAccount());
        var handler = new CreateCompanyFinancialDestinationHandler(
            repo.Object,
            accounts.Object,
            new Mock<ICashRegisterRepository>().Object,
            Tenant().Object,
            Company().Object,
            User().Object
        );
        var cmd = new CreateCompanyFinancialDestinationCommand(
            "BANCO-002",
            "Cuenta sin institución",
            FinancialDestinationTypeCode.BankAccount,
            AccountingAccountId,
            "USD",
            null,
            null,
            "2200123456"
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        repo.Verify(
            r => r.AddAsync(It.IsAny<CompanyFinancialDestination>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_caja_sin_CashRegisterId_rechaza_SC_022_y_no_persiste()
    {
        var repo = new Mock<ICompanyFinancialDestinationRepository>();
        var accounts = new Mock<IAccountRepository>();
        accounts
            .Setup(a => a.GetByIdAsync(TenantId, CompanyId, AccountingAccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveAccount());
        var handler = new CreateCompanyFinancialDestinationHandler(
            repo.Object,
            accounts.Object,
            new Mock<ICashRegisterRepository>().Object,
            Tenant().Object,
            Company().Object,
            User().Object
        );
        var cmd = new CreateCompanyFinancialDestinationCommand(
            "CAJA-002",
            "Caja sin registro",
            FinancialDestinationTypeCode.CashRegister,
            AccountingAccountId,
            "USD",
            null,
            null,
            null
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_tenant_y_company_del_contexto_se_usan_para_validar_la_cuenta_SC_023()
    {
        var repo = new Mock<ICompanyFinancialDestinationRepository>();
        var accounts = new Mock<IAccountRepository>();
        accounts
            .Setup(a => a.GetByIdAsync(TenantId, CompanyId, AccountingAccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveAccount());
        var handler = new CreateCompanyFinancialDestinationHandler(
            repo.Object,
            accounts.Object,
            new Mock<ICashRegisterRepository>().Object,
            Tenant().Object,
            Company().Object,
            User().Object
        );
        var cmd = new CreateCompanyFinancialDestinationCommand(
            "BANCO-001",
            "Cuenta corriente Pichincha",
            FinancialDestinationTypeCode.BankAccount,
            AccountingAccountId,
            "USD",
            null,
            "PICHINCHA",
            "2200123456"
        );

        await handler.Handle(cmd, CancellationToken.None);

        accounts.Verify(
            a => a.GetByIdAsync(TenantId, CompanyId, AccountingAccountId, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Create_cuenta_inexistente_o_de_otra_compania_retorna_NotFound_SC_023_y_no_persiste()
    {
        var repo = new Mock<ICompanyFinancialDestinationRepository>();
        var accounts = new Mock<IAccountRepository>();
        accounts
            .Setup(a => a.GetByIdAsync(TenantId, CompanyId, AccountingAccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Account?)null);
        var handler = new CreateCompanyFinancialDestinationHandler(
            repo.Object,
            accounts.Object,
            new Mock<ICashRegisterRepository>().Object,
            Tenant().Object,
            Company().Object,
            User().Object
        );
        var cmd = new CreateCompanyFinancialDestinationCommand(
            "BANCO-001",
            "Cuenta corriente Pichincha",
            FinancialDestinationTypeCode.BankAccount,
            AccountingAccountId,
            "USD",
            null,
            "PICHINCHA",
            "2200123456"
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_cuenta_inactiva_o_no_postable_retorna_ValidationFailure_SC_024_y_no_persiste()
    {
        var repo = new Mock<ICompanyFinancialDestinationRepository>();
        var accounts = new Mock<IAccountRepository>();
        accounts
            .Setup(a => a.GetByIdAsync(TenantId, CompanyId, AccountingAccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveAccount(allowsPosting: false));
        var handler = new CreateCompanyFinancialDestinationHandler(
            repo.Object,
            accounts.Object,
            new Mock<ICashRegisterRepository>().Object,
            Tenant().Object,
            Company().Object,
            User().Object
        );
        var cmd = new CreateCompanyFinancialDestinationCommand(
            "BANCO-001",
            "Cuenta corriente Pichincha",
            FinancialDestinationTypeCode.BankAccount,
            AccountingAccountId,
            "USD",
            null,
            "PICHINCHA",
            "2200123456"
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_caja_inexistente_o_de_otra_compania_retorna_NotFound_SC_026_y_no_persiste()
    {
        var repo = new Mock<ICompanyFinancialDestinationRepository>();
        var accounts = new Mock<IAccountRepository>();
        accounts
            .Setup(a => a.GetByIdAsync(TenantId, CompanyId, AccountingAccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveAccount());
        var cashRegisters = new Mock<ICashRegisterRepository>();
        cashRegisters
            .Setup(c => c.GetByIdAsync(TenantId, CashRegisterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveCashRegister(Guid.NewGuid())); // otra compañía
        var handler = new CreateCompanyFinancialDestinationHandler(
            repo.Object,
            accounts.Object,
            cashRegisters.Object,
            Tenant().Object,
            Company().Object,
            User().Object
        );
        var cmd = new CreateCompanyFinancialDestinationCommand(
            "CAJA-001",
            "Caja principal",
            FinancialDestinationTypeCode.CashRegister,
            AccountingAccountId,
            "USD",
            CashRegisterId,
            null,
            null
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Rename ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Rename_cambia_solo_el_nombre_y_guarda_una_sola_vez()
    {
        var destination = BankDestination();
        var repo = new Mock<ICompanyFinancialDestinationRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, destination.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(destination);
        var handler = new UpdateCompanyFinancialDestinationNameHandler(
            repo.Object,
            Tenant().Object,
            User().Object
        );

        var result = await handler.Handle(
            new UpdateCompanyFinancialDestinationNameCommand(
                destination.Id,
                "Nueva razón social visible"
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        destination.Name.Should().Be("Nueva razón social visible");
        destination.Code.Should().Be("BANCO-001");
        destination.AccountingAccountId.Should().Be(AccountingAccountId);
        destination.IsActive.Should().BeTrue();
        destination.DestinationTypeCode.Should().Be(FinancialDestinationTypeCode.BankAccount);
        destination.CurrencyCode.Should().Be("USD");
        destination.BankInstitutionCode.Should().Be("PICHINCHA");
        destination.BankAccountIdentifierNormalized.Should().Be("2200123456");
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Rename_destino_de_otro_tenant_retorna_NotFound_fail_closed_y_no_guarda()
    {
        var repo = new Mock<ICompanyFinancialDestinationRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompanyFinancialDestination?)null);
        var handler = new UpdateCompanyFinancialDestinationNameHandler(
            repo.Object,
            Tenant().Object,
            User().Object
        );

        var result = await handler.Handle(
            new UpdateCompanyFinancialDestinationNameCommand(Guid.NewGuid(), "Nuevo nombre"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Rename_nombre_vacio_rechaza_y_no_guarda()
    {
        var destination = BankDestination();
        var repo = new Mock<ICompanyFinancialDestinationRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, destination.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(destination);
        var handler = new UpdateCompanyFinancialDestinationNameHandler(
            repo.Object,
            Tenant().Object,
            User().Object
        );

        var result = await handler.Handle(
            new UpdateCompanyFinancialDestinationNameCommand(destination.Id, " "),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Change accounting account ─────────────────────────────────────

    [Fact]
    public async Task ChangeAccountingAccount_modifica_solo_la_cuenta_y_guarda_una_sola_vez()
    {
        var destination = BankDestination();
        var newAccountId = Guid.NewGuid();
        var repo = new Mock<ICompanyFinancialDestinationRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, destination.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(destination);
        var accounts = new Mock<IAccountRepository>();
        accounts
            .Setup(a => a.GetByIdAsync(TenantId, CompanyId, newAccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveAccount());
        var handler = new ChangeCompanyFinancialDestinationAccountingAccountHandler(
            repo.Object,
            accounts.Object,
            Tenant().Object,
            Company().Object,
            User().Object
        );

        var result = await handler.Handle(
            new ChangeCompanyFinancialDestinationAccountingAccountCommand(
                destination.Id,
                newAccountId
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        destination.AccountingAccountId.Should().Be(newAccountId);
        destination.Name.Should().Be("Cuenta corriente Pichincha");
        destination.Code.Should().Be("BANCO-001");
        destination.IsActive.Should().BeTrue();
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ChangeAccountingAccount_valida_existencia_compania_y_condiciones_de_uso()
    {
        var destination = BankDestination();
        var newAccountId = Guid.NewGuid();
        var repo = new Mock<ICompanyFinancialDestinationRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, destination.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(destination);
        var accounts = new Mock<IAccountRepository>();
        accounts
            .Setup(a => a.GetByIdAsync(TenantId, CompanyId, newAccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveAccount(isActive: false));
        var handler = new ChangeCompanyFinancialDestinationAccountingAccountHandler(
            repo.Object,
            accounts.Object,
            Tenant().Object,
            Company().Object,
            User().Object
        );

        var result = await handler.Handle(
            new ChangeCompanyFinancialDestinationAccountingAccountCommand(
                destination.Id,
                newAccountId
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        destination.AccountingAccountId.Should().Be(AccountingAccountId);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ChangeAccountingAccount_destino_inexistente_retorna_NotFound_fail_closed()
    {
        var repo = new Mock<ICompanyFinancialDestinationRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompanyFinancialDestination?)null);
        var handler = new ChangeCompanyFinancialDestinationAccountingAccountHandler(
            repo.Object,
            new Mock<IAccountRepository>().Object,
            Tenant().Object,
            Company().Object,
            User().Object
        );

        var result = await handler.Handle(
            new ChangeCompanyFinancialDestinationAccountingAccountCommand(
                Guid.NewGuid(),
                Guid.NewGuid()
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }

    // ── Set active ─────────────────────────────────────────────────────

    [Fact]
    public async Task SetActive_false_desactiva_conservando_los_demas_campos_y_guarda_una_sola_vez()
    {
        var destination = BankDestination();
        var repo = new Mock<ICompanyFinancialDestinationRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, destination.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(destination);
        var handler = new SetCompanyFinancialDestinationActiveHandler(
            repo.Object,
            Tenant().Object,
            User().Object
        );

        var result = await handler.Handle(
            new SetCompanyFinancialDestinationActiveCommand(destination.Id, false),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        destination.IsActive.Should().BeFalse();
        destination.Name.Should().Be("Cuenta corriente Pichincha");
        destination.AccountingAccountId.Should().Be(AccountingAccountId);
        destination.DestinationTypeCode.Should().Be(FinancialDestinationTypeCode.BankAccount);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetActive_true_reactiva_correctamente()
    {
        var destination = BankDestination();
        destination.SetActive(false, UserId);
        var repo = new Mock<ICompanyFinancialDestinationRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, destination.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(destination);
        var handler = new SetCompanyFinancialDestinationActiveHandler(
            repo.Object,
            Tenant().Object,
            User().Object
        );

        var result = await handler.Handle(
            new SetCompanyFinancialDestinationActiveCommand(destination.Id, true),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        destination.IsActive.Should().BeTrue();
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetActive_destino_inexistente_retorna_NotFound_fail_closed_y_no_guarda()
    {
        var repo = new Mock<ICompanyFinancialDestinationRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompanyFinancialDestination?)null);
        var handler = new SetCompanyFinancialDestinationActiveHandler(
            repo.Object,
            Tenant().Object,
            User().Object
        );

        var result = await handler.Handle(
            new SetCompanyFinancialDestinationActiveCommand(Guid.NewGuid(), false),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── GetList (Fase 13 Remediación 01) ─────────────────────────────────

    [Fact]
    public async Task GetList_retorna_los_destinos_del_tenant_actual()
    {
        var destination = BankDestination();
        var repo = new Mock<ICompanyFinancialDestinationRepository>();
        repo.Setup(r => r.GetListAsync(TenantId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CompanyFinancialDestination> { destination });
        var handler = new GetCompanyFinancialDestinationListHandler(repo.Object, Tenant().Object);

        var result = await handler.Handle(
            new GetCompanyFinancialDestinationListQuery(null),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(d => d.Id == destination.Id);
    }

    [Fact]
    public async Task GetList_con_isActive_true_delega_el_filtro_al_repositorio()
    {
        var repo = new Mock<ICompanyFinancialDestinationRepository>();
        repo.Setup(r => r.GetListAsync(TenantId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CompanyFinancialDestination>());
        var handler = new GetCompanyFinancialDestinationListHandler(repo.Object, Tenant().Object);

        var result = await handler.Handle(
            new GetCompanyFinancialDestinationListQuery(true),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        repo.Verify(
            r => r.GetListAsync(TenantId, true, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }
}
