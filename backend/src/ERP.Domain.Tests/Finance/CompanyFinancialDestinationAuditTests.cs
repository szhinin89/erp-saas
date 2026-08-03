using ERP.Domain.Audit;
using ERP.Domain.Modules.Finance.Entities;
using FluentAssertions;

namespace ERP.Domain.Tests.Finance;

public sealed class CompanyFinancialDestinationAuditTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid FinancialDestinationId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static AuditActor Actor() =>
        new(TenantId, UserId, "tester", null, null, null, null, null, AuditSource.UserAction);

    [Fact]
    public void Create_para_creacion_pobla_los_campos_comunes_y_propios()
    {
        var audit = CompanyFinancialDestinationAudit.Create(
            Actor(),
            CompanyId,
            FinancialDestinationId,
            "Created",
            code: "BANCO-001"
        );

        audit.TenantId.Should().Be(TenantId);
        audit.EntityId.Should().Be(FinancialDestinationId);
        audit.UserId.Should().Be(UserId);
        audit.Action.Should().Be("Created");
        audit.CompanyId.Should().Be(CompanyId);
        audit.Code.Should().Be("BANCO-001");
    }

    [Fact]
    public void Create_para_edicion_conserva_la_accion_de_edicion_auditada()
    {
        var audit = CompanyFinancialDestinationAudit.Create(
            Actor(),
            CompanyId,
            FinancialDestinationId,
            "Updated",
            code: "BANCO-001",
            reason: "Cambio de cuenta contable"
        );

        audit.Action.Should().Be("Updated");
        audit.Reason.Should().Be("Cambio de cuenta contable");
        audit.Code.Should().Be("BANCO-001");
    }

    [Fact]
    public void Create_conserva_exactamente_el_Code_recibido_sin_sustituirlo()
    {
        var auditA = CompanyFinancialDestinationAudit.Create(
            Actor(),
            CompanyId,
            FinancialDestinationId,
            "Created",
            code: "BANCO-001"
        );
        var auditB = CompanyFinancialDestinationAudit.Create(
            Actor(),
            CompanyId,
            FinancialDestinationId,
            "Created",
            code: "CAJA-002"
        );

        auditA.Code.Should().Be("BANCO-001");
        auditB.Code.Should().Be("CAJA-002");
        auditA.Code.Should().NotBe(auditB.Code);
    }

    [Fact]
    public void Create_rechaza_companyId_vacio()
    {
        var act = () =>
            CompanyFinancialDestinationAudit.Create(
                Actor(),
                Guid.Empty,
                FinancialDestinationId,
                "Created"
            );

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_rechaza_entityId_vacio_via_SetCommon()
    {
        var act = () =>
            CompanyFinancialDestinationAudit.Create(Actor(), CompanyId, Guid.Empty, "Created");

        act.Should().Throw<ArgumentException>();
    }

    // ── Valores antes/después de los 3 campos editables (§20.1, Remediación técnica limitada 01) ──

    [Fact]
    public void Create_para_creacion_puebla_solo_los_valores_New_sin_ningun_Old()
    {
        var audit = CompanyFinancialDestinationAudit.Create(
            Actor(),
            CompanyId,
            FinancialDestinationId,
            "Created",
            code: "BANCO-001",
            newName: "Cuenta corriente Pichincha",
            newIsActive: true,
            newAccountingAccountId: Guid.NewGuid()
        );

        audit.OldName.Should().BeNull();
        audit.OldIsActive.Should().BeNull();
        audit.OldAccountingAccountId.Should().BeNull();
        audit.NewName.Should().Be("Cuenta corriente Pichincha");
        audit.NewIsActive.Should().BeTrue();
        audit.NewAccountingAccountId.Should().NotBeNull();
    }

    [Fact]
    public void Create_para_renombrado_puebla_OldName_y_NewName_sin_tocar_los_otros_campos()
    {
        var audit = CompanyFinancialDestinationAudit.Create(
            Actor(),
            CompanyId,
            FinancialDestinationId,
            "Renamed",
            code: "BANCO-001",
            oldName: "Cuenta corriente Pichincha",
            newName: "Nueva razón social visible"
        );

        audit.OldName.Should().Be("Cuenta corriente Pichincha");
        audit.NewName.Should().Be("Nueva razón social visible");
        audit.OldIsActive.Should().BeNull();
        audit.NewIsActive.Should().BeNull();
        audit.OldAccountingAccountId.Should().BeNull();
        audit.NewAccountingAccountId.Should().BeNull();
    }

    [Fact]
    public void Create_para_activacion_puebla_OldIsActive_y_NewIsActive_sin_tocar_los_otros_campos()
    {
        var audit = CompanyFinancialDestinationAudit.Create(
            Actor(),
            CompanyId,
            FinancialDestinationId,
            "Deactivated",
            code: "BANCO-001",
            oldIsActive: true,
            newIsActive: false
        );

        audit.OldIsActive.Should().BeTrue();
        audit.NewIsActive.Should().BeFalse();
        audit.OldName.Should().BeNull();
        audit.NewName.Should().BeNull();
        audit.OldAccountingAccountId.Should().BeNull();
        audit.NewAccountingAccountId.Should().BeNull();
    }

    [Fact]
    public void Create_para_cambio_de_cuenta_contable_puebla_OldAccountingAccountId_y_NewAccountingAccountId()
    {
        var oldAccountId = Guid.NewGuid();
        var newAccountId = Guid.NewGuid();

        var audit = CompanyFinancialDestinationAudit.Create(
            Actor(),
            CompanyId,
            FinancialDestinationId,
            "AccountChanged",
            code: "BANCO-001",
            oldAccountingAccountId: oldAccountId,
            newAccountingAccountId: newAccountId
        );

        audit.OldAccountingAccountId.Should().Be(oldAccountId);
        audit.NewAccountingAccountId.Should().Be(newAccountId);
        audit.OldName.Should().BeNull();
        audit.NewName.Should().BeNull();
        audit.OldIsActive.Should().BeNull();
        audit.NewIsActive.Should().BeNull();
    }
}
