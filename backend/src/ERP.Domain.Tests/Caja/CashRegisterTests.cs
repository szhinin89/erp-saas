using ERP.Domain.Modules.Caja.Entities;
using FluentAssertions;

namespace ERP.Domain.Tests.Caja;

/// <summary>
/// Fase 1 (ADR — Rediseño del módulo de Caja) — CashRegister como Aggregate Root independiente.
/// BranchId/TenantId/CompanyId inmutables (Branch Ownership Rule); EmissionPointId solo se muta
/// mediante ChangeEmissionPoint; sin eliminación física (MasterEntity: Enable/Disable únicamente).
/// </summary>
public sealed class CashRegisterTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid CreatedBy = Guid.NewGuid();

    private static CashRegister CreateValid(
        Guid? branchId = null,
        Guid? emissionPointId = null,
        string code = "CAJA-01",
        string name = "Caja Principal"
    ) =>
        CashRegister.Create(
            TenantId,
            CompanyId,
            branchId ?? Guid.NewGuid(),
            code,
            name,
            CreatedBy,
            emissionPointId
        );

    [Fact]
    public void Create_con_datos_validos_persiste_los_campos()
    {
        var branchId = Guid.NewGuid();
        var emissionPointId = Guid.NewGuid();

        var register = CashRegister.Create(
            TenantId,
            CompanyId,
            branchId,
            "CAJA-01",
            "Caja Principal",
            CreatedBy,
            emissionPointId,
            "Turno mañana"
        );

        register.TenantId.Should().Be(TenantId);
        register.CompanyId.Should().Be(CompanyId);
        register.BranchId.Should().Be(branchId);
        register.Code.Should().Be("CAJA-01");
        register.Name.Should().Be("Caja Principal");
        register.EmissionPointId.Should().Be(emissionPointId);
        register.Notes.Should().Be("Turno mañana");
        register.IsActive.Should().BeTrue();
        register.CreatedBy.Should().Be(CreatedBy);
    }

    [Fact]
    public void Create_sin_punto_de_emision_lo_deja_nulo()
    {
        var register = CreateValid(emissionPointId: null);

        register.EmissionPointId.Should().BeNull();
    }

    [Fact]
    public void Create_con_BranchId_vacio_lanza_ArgumentException()
    {
        var act = () =>
            CashRegister.Create(
                TenantId,
                CompanyId,
                Guid.Empty,
                "CAJA-01",
                "Caja Principal",
                CreatedBy
            );

        act.Should().Throw<ArgumentException>().WithParameterName("branchId");
    }

    [Fact]
    public void Create_con_CompanyId_vacio_lanza_ArgumentException()
    {
        var act = () =>
            CashRegister.Create(
                TenantId,
                Guid.Empty,
                Guid.NewGuid(),
                "CAJA-01",
                "Caja Principal",
                CreatedBy
            );

        act.Should().Throw<ArgumentException>().WithParameterName("companyId");
    }

    [Fact]
    public void Create_con_codigo_vacio_lanza_ArgumentException()
    {
        var act = () =>
            CashRegister.Create(
                TenantId,
                CompanyId,
                Guid.NewGuid(),
                "  ",
                "Caja Principal",
                CreatedBy
            );

        act.Should().Throw<ArgumentException>().WithParameterName("code");
    }

    [Fact]
    public void Create_con_nombre_vacio_lanza_ArgumentException()
    {
        var act = () =>
            CashRegister.Create(TenantId, CompanyId, Guid.NewGuid(), "CAJA-01", "  ", CreatedBy);

        act.Should().Throw<ArgumentException>().WithParameterName("name");
    }

    [Fact]
    public void ChangeEmissionPoint_asigna_un_nuevo_punto_de_emision()
    {
        var register = CreateValid(emissionPointId: null);
        var newEmissionPointId = Guid.NewGuid();

        register.ChangeEmissionPoint(newEmissionPointId, CreatedBy);

        register.EmissionPointId.Should().Be(newEmissionPointId);
        register.UpdatedBy.Should().Be(CreatedBy);
        register.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void ChangeEmissionPoint_con_null_desasigna_el_punto_de_emision()
    {
        var register = CreateValid(emissionPointId: Guid.NewGuid());

        register.ChangeEmissionPoint(null, CreatedBy);

        register.EmissionPointId.Should().BeNull();
    }

    [Fact]
    public void ChangeEmissionPoint_con_Guid_vacio_lanza_ArgumentException()
    {
        var register = CreateValid();

        var act = () => register.ChangeEmissionPoint(Guid.Empty, CreatedBy);

        act.Should().Throw<ArgumentException>().WithParameterName("emissionPointId");
    }

    [Fact]
    public void Enable_y_Disable_alternan_IsActive()
    {
        var register = CreateValid();

        register.Disable(CreatedBy);
        register.IsActive.Should().BeFalse();

        register.Enable(CreatedBy);
        register.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Disable_dos_veces_seguidas_lanza_InvalidOperationException()
    {
        var register = CreateValid();
        register.Disable(CreatedBy);

        var act = () => register.Disable(CreatedBy);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void BranchId_no_expone_setter_publico_ni_metodo_ChangeBranch()
    {
        var property = typeof(CashRegister).GetProperty(nameof(CashRegister.BranchId))!;
        property.SetMethod.Should().NotBeNull();
        property.SetMethod!.IsPublic.Should().BeFalse("BranchId solo se asigna en Create");

        typeof(CashRegister)
            .GetMethods()
            .Any(m => m.Name is "ChangeBranch" or "SetBranch" or "UpdateBranch")
            .Should()
            .BeFalse("no debe existir ningún método para mutar la sucursal");
    }

    [Fact]
    public void CompanyId_no_expone_setter_publico_ni_metodo_de_cambio()
    {
        var property = typeof(CashRegister).GetProperty(nameof(CashRegister.CompanyId))!;
        property.SetMethod.Should().NotBeNull();
        property.SetMethod!.IsPublic.Should().BeFalse("CompanyId solo se asigna en Create");

        typeof(CashRegister)
            .GetMethods()
            .Any(m => m.Name is "ChangeCompany" or "SetCompany" or "UpdateCompany")
            .Should()
            .BeFalse("no debe existir ningún método para mutar la empresa");
    }

    [Fact]
    public void TenantId_no_expone_setter_publico_ni_metodo_de_cambio()
    {
        var property = typeof(CashRegister).GetProperty(nameof(CashRegister.TenantId))!;
        property.SetMethod.Should().NotBeNull();
        property
            .SetMethod!.IsPublic.Should()
            .BeFalse("TenantId es inmutable desde BaseEntity (setter protegido)");

        typeof(CashRegister)
            .GetMethods()
            .Any(m => m.Name is "ChangeTenant" or "SetTenant" or "UpdateTenant")
            .Should()
            .BeFalse("no debe existir ningún método para mutar el tenant");
    }

    [Fact]
    public void No_existe_ningun_metodo_publico_de_eliminacion_fisica()
    {
        typeof(CashRegister)
            .GetMethods()
            .Any(m => m.Name is "Delete" or "Remove" or "HardDelete")
            .Should()
            .BeFalse(
                "la única forma de dar de baja una caja es Disable() — soft delete vía IsActive"
            );
    }
}
