using ERP.Domain.Configuration.Entities;
using ERP.Domain.Exceptions;
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
            .Throw<DomainRuleViolationException>()
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

    // ── ZH-SRI-ANEXO26-PROVIDER-RUC-01 (ADR-038 D7, regla 2) ───────────────

    [Fact]
    public void Configure_enabled_true_sin_EffectiveDate_lanza_excepcion()
    {
        var settings = SystemProviderSettings.CreateNew();

        var act = () =>
            settings.Configure("1790012345001", "ZH Technologies S.A.", "J62021002", null, enabled: true, UserId);

        act.Should()
            .Throw<DomainRuleViolationException>()
            .WithMessage(SystemProviderSettings.EnabledWithoutEffectiveDateMessage);
        settings.Enabled.Should().BeFalse("una configuración inválida nunca se aplica");
    }

    [Fact]
    public void Configure_enabled_true_con_RUC_invalido_lanza_excepcion()
    {
        var settings = SystemProviderSettings.CreateNew();

        var act = () =>
            settings.Configure("179001234500A", "ZH Technologies S.A.", "J62021002", new DateOnly(2026, 11, 3), enabled: true, UserId);

        act.Should().Throw<ArgumentException>();
        settings.Enabled.Should().BeFalse();
    }

    [Fact]
    public void Configure_enabled_false_puede_quedar_sin_EffectiveDate()
    {
        var settings = SystemProviderSettings.CreateNew();

        settings.Configure("1790012345001", "ZH Technologies S.A.", "J62021002", null, enabled: false, UserId);

        settings.Enabled.Should().BeFalse();
        settings.EffectiveDate.Should().BeNull();
    }

    [Theory]
    [InlineData("1790012345001", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("179001234500", false)]
    [InlineData("17900123450011", false)]
    [InlineData("179001234500A", false)]
    [InlineData("١٧٩٠٠١٢٣٤٥٠٠١", false)]
    public void IsValidRuc_exige_13_digitos_ASCII(string? ruc, bool expected)
    {
        SystemProviderSettings.IsValidRuc(ruc).Should().Be(expected);
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
