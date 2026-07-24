using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Enums;
using FluentAssertions;

namespace ERP.Domain.Tests.Access;

public sealed class CompanyUserPreferencesTests
{
    private static CompanyUserPreferences CreateEntity(
        CompanyUserLoginMode loginMode = CompanyUserLoginMode.AskBranch,
        Guid? defaultBranchId = null) =>
        CompanyUserPreferences.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), loginMode, defaultBranchId, Guid.NewGuid());

    [Fact]
    public void Create_con_AskBranch_y_sin_sucursal_por_defecto_es_valida()
    {
        var entity = CreateEntity(CompanyUserLoginMode.AskBranch, defaultBranchId: null);

        entity.LoginMode.Should().Be(CompanyUserLoginMode.AskBranch);
        entity.DefaultBranchId.Should().BeNull();
    }

    [Fact]
    public void Create_con_DirectToDefault_y_sucursal_asignada_es_valida()
    {
        var branchId = Guid.NewGuid();

        var entity = CreateEntity(CompanyUserLoginMode.DirectToDefault, branchId);

        entity.LoginMode.Should().Be(CompanyUserLoginMode.DirectToDefault);
        entity.DefaultBranchId.Should().Be(branchId);
    }

    [Fact]
    public void Create_asigna_CreatedBy_al_valor_recibido()
    {
        var createdBy = Guid.NewGuid();

        var entity = CompanyUserPreferences.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            CompanyUserLoginMode.AskBranch, null, createdBy);

        entity.CreatedBy.Should().Be(createdBy);
    }

    [Fact]
    public void Create_con_DirectToDefault_sin_sucursal_por_defecto_lanza_ArgumentException()
    {
        var act = () => CompanyUserPreferences.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            CompanyUserLoginMode.DirectToDefault, null, Guid.NewGuid());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_con_CompanyId_vacio_lanza_ArgumentException()
    {
        var act = () => CompanyUserPreferences.Create(
            Guid.NewGuid(), Guid.Empty, Guid.NewGuid(),
            CompanyUserLoginMode.AskBranch, null, Guid.NewGuid());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_con_CompanyUserMembershipId_vacio_lanza_ArgumentException()
    {
        var act = () => CompanyUserPreferences.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.Empty,
            CompanyUserLoginMode.AskBranch, null, Guid.NewGuid());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_con_DefaultBranchId_Guid_Empty_lanza_ArgumentException()
    {
        var act = () => CompanyUserPreferences.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            CompanyUserLoginMode.AskBranch, Guid.Empty, Guid.NewGuid());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ChangeDefaultBranch_actualiza_la_sucursal_por_defecto()
    {
        var entity = CreateEntity();
        var newBranchId = Guid.NewGuid();
        var updatedBy = Guid.NewGuid();

        entity.ChangeDefaultBranch(newBranchId, updatedBy);

        entity.DefaultBranchId.Should().Be(newBranchId);
        entity.UpdatedBy.Should().Be(updatedBy);
    }

    [Fact]
    public void ChangeDefaultBranch_es_idempotente_no_reescribe_UpdatedBy_si_es_la_misma_sucursal()
    {
        var branchId = Guid.NewGuid();
        var entity = CreateEntity(CompanyUserLoginMode.DirectToDefault, branchId);
        entity.ChangeDefaultBranch(Guid.NewGuid(), Guid.NewGuid());
        var firstUpdatedBy = entity.UpdatedBy;
        var firstUpdatedAt = entity.UpdatedAt;
        var currentBranchId = entity.DefaultBranchId;

        entity.ChangeDefaultBranch(currentBranchId, Guid.NewGuid());

        entity.UpdatedBy.Should().Be(firstUpdatedBy);
        entity.UpdatedAt.Should().Be(firstUpdatedAt);
    }

    [Fact]
    public void ChangeDefaultBranch_con_Guid_Empty_lanza_ArgumentException()
    {
        var entity = CreateEntity();

        var act = () => entity.ChangeDefaultBranch(Guid.Empty, Guid.NewGuid());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ChangeDefaultBranch_a_null_mientras_DirectToDefault_lanza_ArgumentException()
    {
        var entity = CreateEntity(CompanyUserLoginMode.DirectToDefault, Guid.NewGuid());

        var act = () => entity.ChangeDefaultBranch(null, Guid.NewGuid());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ChangeLoginMode_actualiza_el_modo()
    {
        var entity = CreateEntity(CompanyUserLoginMode.DirectToDefault, Guid.NewGuid());
        var updatedBy = Guid.NewGuid();

        entity.ChangeLoginMode(CompanyUserLoginMode.AskBranch, updatedBy);

        entity.LoginMode.Should().Be(CompanyUserLoginMode.AskBranch);
        entity.UpdatedBy.Should().Be(updatedBy);
    }

    [Fact]
    public void ChangeLoginMode_es_idempotente_no_reescribe_UpdatedBy_si_ya_era_el_mismo_modo()
    {
        var entity = CreateEntity(CompanyUserLoginMode.AskBranch);

        entity.ChangeLoginMode(CompanyUserLoginMode.AskBranch, Guid.NewGuid());

        entity.UpdatedBy.Should().BeNull();
        entity.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void ChangeLoginMode_a_DirectToDefault_sin_sucursal_por_defecto_lanza_ArgumentException()
    {
        var entity = CreateEntity(CompanyUserLoginMode.AskBranch, defaultBranchId: null);

        var act = () => entity.ChangeLoginMode(CompanyUserLoginMode.DirectToDefault, Guid.NewGuid());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ChangeLoginMode_a_DirectToDefault_con_sucursal_por_defecto_ya_asignada_es_valido()
    {
        var entity = CreateEntity(CompanyUserLoginMode.AskBranch, defaultBranchId: Guid.NewGuid());

        entity.ChangeLoginMode(CompanyUserLoginMode.DirectToDefault, Guid.NewGuid());

        entity.LoginMode.Should().Be(CompanyUserLoginMode.DirectToDefault);
    }
}
