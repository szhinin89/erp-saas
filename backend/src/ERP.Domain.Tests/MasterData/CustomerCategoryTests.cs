using ERP.Domain.MasterData.Entities;
using FluentAssertions;

namespace ERP.Domain.Tests.MasterData;

/// <summary>
/// CLASS-BP-CATALOGS-01 — cubre <see cref="CustomerCategory"/> como representante de los 12
/// catálogos de clasificación de BusinessPartner (todos comparten la misma forma: Create,
/// CreateSystemSeeded, Update, Disable con guard de <c>IsSystemSeeded</c>).
/// </summary>
public sealed class CustomerCategoryTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();

    [Fact]
    public void Create_con_datos_validos_asigna_propiedades()
    {
        var entity = CustomerCategory.Create(
            TenantId,
            CompanyId,
            "Retail",
            "Minorista",
            1,
            ActorId
        );

        entity.TenantId.Should().Be(TenantId);
        entity.CompanyId.Should().Be(CompanyId);
        entity.Code.Should().Be("Retail");
        entity.Name.Should().Be("Minorista");
        entity.SortOrder.Should().Be(1);
        entity.IsActive.Should().BeTrue();
        entity.IsSystemSeeded.Should().BeFalse();
    }

    [Fact]
    public void Create_sin_codigo_lanza_ArgumentException()
    {
        var act = () => CustomerCategory.Create(TenantId, CompanyId, "  ", "Minorista", 1, ActorId);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_con_codigo_muy_largo_lanza_ArgumentException()
    {
        var act = () =>
            CustomerCategory.Create(
                TenantId,
                CompanyId,
                new string('X', CustomerCategory.CodeMaxLength + 1),
                "Minorista",
                1,
                ActorId
            );
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateSystemSeeded_marca_IsSystemSeeded()
    {
        var entity = CustomerCategory.CreateSystemSeeded(
            TenantId,
            CompanyId,
            "Retail",
            "Minorista",
            1,
            ActorId
        );

        entity.IsSystemSeeded.Should().BeTrue();
    }

    [Fact]
    public void Update_en_entidad_normal_actualiza_Name_y_SortOrder()
    {
        var entity = CustomerCategory.Create(
            TenantId,
            CompanyId,
            "Retail",
            "Minorista",
            1,
            ActorId
        );

        entity.Update("Minorista actualizado", 2, ActorId);

        entity.Name.Should().Be("Minorista actualizado");
        entity.SortOrder.Should().Be(2);
    }

    [Fact]
    public void Update_en_entidad_system_seeded_lanza_InvalidOperationException()
    {
        var entity = CustomerCategory.CreateSystemSeeded(
            TenantId,
            CompanyId,
            "Retail",
            "Minorista",
            1,
            ActorId
        );

        var act = () => entity.Update("Otro nombre", 2, ActorId);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Disable_en_entidad_normal_desactiva()
    {
        var entity = CustomerCategory.Create(
            TenantId,
            CompanyId,
            "Retail",
            "Minorista",
            1,
            ActorId
        );

        entity.Disable(ActorId);

        entity.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Disable_en_entidad_system_seeded_lanza_InvalidOperationException()
    {
        var entity = CustomerCategory.CreateSystemSeeded(
            TenantId,
            CompanyId,
            "Retail",
            "Minorista",
            1,
            ActorId
        );

        var act = () => entity.Disable(ActorId);

        act.Should().Throw<InvalidOperationException>();
    }
}
