using ERP.Domain.Configuration.Entities;
using FluentAssertions;

namespace ERP.Domain.Tests.Configuration;

/// <summary>ERP-CORE-CLOSEOUT-09 — datos del proveedor del sistema de facturación electrónica.</summary>
public sealed class SystemProviderSettingsTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void CreateNew_empieza_deshabilitado_y_sin_datos()
    {
        var settings = SystemProviderSettings.CreateNew();

        settings.Enabled.Should().BeFalse();
        settings.IsFullyConfigured.Should().BeFalse();
        settings.Ruc.Should().BeNull();
    }

    [Fact]
    public void Configure_con_datos_completos_y_enabled_true_funciona()
    {
        var settings = SystemProviderSettings.CreateNew();

        settings.Configure(
            "1790012345001",
            "ZH Technologies S.A.",
            "J62021002",
            new DateOnly(2026, 8, 21),
            enabled: true,
            UserId
        );

        settings.Enabled.Should().BeTrue();
        settings.IsFullyConfigured.Should().BeTrue();
        settings.Ruc.Should().Be("1790012345001");
        settings.UpdatedBy.Should().Be(UserId);
    }

    [Fact]
    public void Configure_enabled_true_sin_RUC_lanza_excepcion()
    {
        var settings = SystemProviderSettings.CreateNew();

        var act = () =>
            settings.Configure(null, "ZH Technologies S.A.", "J62021002", null, enabled: true, UserId);

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*RUC, razón social y CIIU completos*");
    }

    [Theory]
    [InlineData("123")]
    [InlineData("179001234500A")]
    public void Configure_con_RUC_invalido_lanza_excepcion(string invalidRuc)
    {
        var settings = SystemProviderSettings.CreateNew();

        var act = () => settings.Configure(invalidRuc, null, null, null, enabled: false, UserId);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Configure_enabled_false_con_datos_parciales_no_lanza()
    {
        var settings = SystemProviderSettings.CreateNew();

        settings.Configure("1790012345001", null, null, null, enabled: false, UserId);

        settings.Enabled.Should().BeFalse();
        settings.IsFullyConfigured.Should().BeFalse();
    }
}
