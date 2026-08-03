using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Finance.Events;
using FluentAssertions;

namespace ERP.Domain.Tests.Finance;

public sealed class CompanyFinancialDestinationTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid AccountingAccountId = Guid.NewGuid();
    private static readonly Guid CashRegisterId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static CompanyFinancialDestination CreateBank() =>
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

    private static CompanyFinancialDestination CreateCash() =>
        CompanyFinancialDestination.Create(
            TenantId,
            CompanyId,
            "CAJA-001",
            "Caja principal",
            FinancialDestinationTypeCode.CashRegister,
            AccountingAccountId,
            "USD",
            UserId,
            cashRegisterId: CashRegisterId
        );

    // ── Create — CHECK combinado banco vs. caja (§6.4) ────────────────

    [Fact]
    public void Create_banco_con_datos_bancarios_completos_es_valido()
    {
        var destination = CreateBank();

        destination.DestinationTypeCode.Should().Be(FinancialDestinationTypeCode.BankAccount);
        destination.BankInstitutionCode.Should().Be("PICHINCHA");
        destination.BankAccountIdentifierNormalized.Should().Be("2200123456");
        destination.CashRegisterId.Should().BeNull();
        destination.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_caja_con_CashRegisterId_es_valido()
    {
        var destination = CreateCash();

        destination.DestinationTypeCode.Should().Be(FinancialDestinationTypeCode.CashRegister);
        destination.CashRegisterId.Should().Be(CashRegisterId);
        destination.BankInstitutionCode.Should().BeNull();
        destination.BankAccountIdentifierNormalized.Should().BeNull();
    }

    [Fact]
    public void Create_banco_sin_institucion_bancaria_lanza()
    {
        var act = () =>
            CompanyFinancialDestination.Create(
                TenantId,
                CompanyId,
                "BANCO-002",
                "Cuenta sin institución",
                FinancialDestinationTypeCode.BankAccount,
                AccountingAccountId,
                "USD",
                UserId,
                bankAccountIdentifierNormalized: "2200123456"
            );

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_banco_sin_identificador_de_cuenta_lanza()
    {
        var act = () =>
            CompanyFinancialDestination.Create(
                TenantId,
                CompanyId,
                "BANCO-003",
                "Cuenta sin identificador",
                FinancialDestinationTypeCode.BankAccount,
                AccountingAccountId,
                "USD",
                UserId,
                bankInstitutionCode: "PICHINCHA"
            );

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_caja_sin_CashRegisterId_lanza()
    {
        var act = () =>
            CompanyFinancialDestination.Create(
                TenantId,
                CompanyId,
                "CAJA-002",
                "Caja sin registro",
                FinancialDestinationTypeCode.CashRegister,
                AccountingAccountId,
                "USD",
                UserId
            );

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_banco_con_CashRegisterId_ademas_de_datos_bancarios_lanza()
    {
        var act = () =>
            CompanyFinancialDestination.Create(
                TenantId,
                CompanyId,
                "BANCO-004",
                "Cuenta mixta inválida",
                FinancialDestinationTypeCode.BankAccount,
                AccountingAccountId,
                "USD",
                UserId,
                cashRegisterId: CashRegisterId,
                bankInstitutionCode: "PICHINCHA",
                bankAccountIdentifierNormalized: "2200123456"
            );

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_caja_con_datos_bancarios_ademas_de_CashRegisterId_lanza()
    {
        var act = () =>
            CompanyFinancialDestination.Create(
                TenantId,
                CompanyId,
                "CAJA-003",
                "Caja mixta inválida",
                FinancialDestinationTypeCode.CashRegister,
                AccountingAccountId,
                "USD",
                UserId,
                cashRegisterId: CashRegisterId,
                bankInstitutionCode: "PICHINCHA"
            );

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_puede_persistir_IsActive_false_sin_violar_ningun_CHECK_estructural()
    {
        var destination = CreateBank();

        destination.SetActive(false, UserId);

        destination.IsActive.Should().BeFalse();
        destination.DestinationTypeCode.Should().Be(FinancialDestinationTypeCode.BankAccount);
    }

    // ── Inmutabilidad de los 8 campos estructurales (§6.4ter) ─────────

    [Fact]
    public void Los_8_campos_estructurales_no_tienen_setter_publico()
    {
        // Verificación estructural: la única forma de "cambiarlos" sería crear un nuevo destino —
        // documentado por la ausencia total de métodos que los muten en la API pública.
        var publicMethods = typeof(CompanyFinancialDestination)
            .GetMethods()
            .Select(m => m.Name)
            .ToList();

        publicMethods.Should().NotContain("SetCode");
        publicMethods.Should().NotContain("ChangeDestinationType");
        publicMethods.Should().NotContain("SetCurrencyCode");
        publicMethods.Should().NotContain("SetCashRegisterId");
        publicMethods.Should().NotContain("SetBankInstitutionCode");
        publicMethods.Should().NotContain("SetBankAccountIdentifierNormalized");
    }

    // ── 3 mutadores permitidos ─────────────────────────────────────────

    [Fact]
    public void UpdateName_cambia_solo_el_nombre()
    {
        var destination = CreateBank();

        destination.UpdateName("Nueva razón social visible", UserId);

        destination.Name.Should().Be("Nueva razón social visible");
        destination.Code.Should().Be("BANCO-001");
    }

    [Fact]
    public void UpdateName_rechaza_nombre_vacio()
    {
        var destination = CreateBank();

        var act = () => destination.UpdateName(" ", UserId);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ChangeAccountingAccount_actualiza_la_cuenta_para_operaciones_futuras()
    {
        var destination = CreateBank();
        var newAccountId = Guid.NewGuid();

        destination.ChangeAccountingAccount(newAccountId, UserId);

        destination.AccountingAccountId.Should().Be(newAccountId);
    }

    [Fact]
    public void ChangeAccountingAccount_rechaza_cuenta_vacia()
    {
        var destination = CreateBank();

        var act = () => destination.ChangeAccountingAccount(Guid.Empty, UserId);

        act.Should().Throw<ArgumentException>();
    }

    // ── Eventos de dominio — soporte de auditoría (Remediación técnica limitada 01) ──

    [Fact]
    public void Create_levanta_CompanyFinancialDestinationCreatedEvent_con_los_valores_iniciales()
    {
        var destination = CreateBank();

        var evt = destination.DomainEvents.Should().ContainSingle().Subject;
        var created = evt.Should().BeOfType<CompanyFinancialDestinationCreatedEvent>().Subject;

        created.TenantId.Should().Be(TenantId);
        created.DestinationId.Should().Be(destination.Id);
        created.Code.Should().Be("BANCO-001");
        created.Name.Should().Be("Cuenta corriente Pichincha");
        created.DestinationTypeCode.Should().Be(FinancialDestinationTypeCode.BankAccount);
        created.AccountingAccountId.Should().Be(AccountingAccountId);
        created.IsActive.Should().BeTrue();
        ((ERP.Domain.Audit.IAuditEvent)created).Action.Should().Be("Created");
    }

    [Fact]
    public void UpdateName_levanta_CompanyFinancialDestinationRenamedEvent_con_nombre_anterior_y_nuevo()
    {
        var destination = CreateBank();
        destination.ClearDomainEvents();

        destination.UpdateName("Nueva razón social visible", UserId);

        var evt = destination.DomainEvents.Should().ContainSingle().Subject;
        var renamed = evt.Should().BeOfType<CompanyFinancialDestinationRenamedEvent>().Subject;

        renamed.DestinationId.Should().Be(destination.Id);
        renamed.Code.Should().Be("BANCO-001");
        renamed.OldName.Should().Be("Cuenta corriente Pichincha");
        renamed.NewName.Should().Be("Nueva razón social visible");
        ((ERP.Domain.Audit.IAuditEvent)renamed).Action.Should().Be("Renamed");
    }

    [Fact]
    public void SetActive_false_levanta_CompanyFinancialDestinationActiveChangedEvent_con_accion_Deactivated()
    {
        var destination = CreateBank();
        destination.ClearDomainEvents();

        destination.SetActive(false, UserId);

        var evt = destination.DomainEvents.Should().ContainSingle().Subject;
        var changed = evt.Should()
            .BeOfType<CompanyFinancialDestinationActiveChangedEvent>()
            .Subject;

        changed.OldIsActive.Should().BeTrue();
        changed.NewIsActive.Should().BeFalse();
        ((ERP.Domain.Audit.IAuditEvent)changed).Action.Should().Be("Deactivated");
    }

    [Fact]
    public void SetActive_true_levanta_CompanyFinancialDestinationActiveChangedEvent_con_accion_Activated()
    {
        var destination = CreateBank();
        destination.SetActive(false, UserId);
        destination.ClearDomainEvents();

        destination.SetActive(true, UserId);

        var evt = destination.DomainEvents.Should().ContainSingle().Subject;
        var changed = evt.Should()
            .BeOfType<CompanyFinancialDestinationActiveChangedEvent>()
            .Subject;

        changed.OldIsActive.Should().BeFalse();
        changed.NewIsActive.Should().BeTrue();
        ((ERP.Domain.Audit.IAuditEvent)changed).Action.Should().Be("Activated");
    }

    [Fact]
    public void ChangeAccountingAccount_levanta_CompanyFinancialDestinationAccountChangedEvent_con_cuenta_anterior_y_nueva()
    {
        var destination = CreateBank();
        destination.ClearDomainEvents();
        var newAccountId = Guid.NewGuid();

        destination.ChangeAccountingAccount(newAccountId, UserId);

        var evt = destination.DomainEvents.Should().ContainSingle().Subject;
        var changed = evt.Should()
            .BeOfType<CompanyFinancialDestinationAccountChangedEvent>()
            .Subject;

        changed.OldAccountingAccountId.Should().Be(AccountingAccountId);
        changed.NewAccountingAccountId.Should().Be(newAccountId);
        ((ERP.Domain.Audit.IAuditEvent)changed).Action.Should().Be("AccountChanged");
    }
}
