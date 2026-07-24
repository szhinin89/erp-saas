using ERP.Domain.Access.Entities;
using FluentAssertions;

namespace ERP.Domain.Tests.Access;

public sealed class CompanyUserBranchTests
{
    private static CompanyUserBranch CreateEntity() =>
        CompanyUserBranch.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

    [Fact]
    public void Create_queda_activa_por_defecto()
    {
        var entity = CreateEntity();

        entity.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_asigna_CreatedBy_al_valor_recibido()
    {
        var createdBy = Guid.NewGuid();

        var entity = CompanyUserBranch.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), createdBy);

        entity.CreatedBy.Should().Be(createdBy);
    }

    [Fact]
    public void Create_con_CompanyId_vacio_lanza_ArgumentException()
    {
        var act = () => CompanyUserBranch.Create(Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_con_CompanyUserMembershipId_vacio_lanza_ArgumentException()
    {
        var act = () => CompanyUserBranch.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), Guid.NewGuid());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_con_BranchId_vacio_lanza_ArgumentException()
    {
        var act = () => CompanyUserBranch.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, Guid.NewGuid());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Deactivate_revoca_la_autorizacion()
    {
        var entity = CreateEntity();
        var updatedBy = Guid.NewGuid();

        entity.Deactivate(updatedBy);

        entity.IsActive.Should().BeFalse();
        entity.UpdatedBy.Should().Be(updatedBy);
    }

    [Fact]
    public void Deactivate_es_idempotente_no_reescribe_UpdatedBy_si_ya_estaba_inactiva()
    {
        var entity = CreateEntity();
        entity.Deactivate(Guid.NewGuid());
        var firstUpdatedBy = entity.UpdatedBy;
        var firstUpdatedAt = entity.UpdatedAt;

        entity.Deactivate(Guid.NewGuid());

        entity.IsActive.Should().BeFalse();
        entity.UpdatedBy.Should().Be(firstUpdatedBy);
        entity.UpdatedAt.Should().Be(firstUpdatedAt);
    }

    [Fact]
    public void Activate_restaura_una_autorizacion_previamente_revocada()
    {
        var entity = CreateEntity();
        entity.Deactivate(Guid.NewGuid());
        var updatedBy = Guid.NewGuid();

        entity.Activate(updatedBy);

        entity.IsActive.Should().BeTrue();
        entity.UpdatedBy.Should().Be(updatedBy);
    }

    [Fact]
    public void Activate_es_idempotente_no_reescribe_UpdatedBy_si_ya_estaba_activa()
    {
        var entity = CreateEntity();

        entity.Activate(Guid.NewGuid());

        entity.UpdatedBy.Should().BeNull();
        entity.UpdatedAt.Should().BeNull();
    }
}
