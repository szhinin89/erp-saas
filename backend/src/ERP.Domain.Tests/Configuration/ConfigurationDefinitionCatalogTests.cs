using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Definitions;
using ERP.Domain.Configuration.Enums;
using FluentAssertions;

namespace ERP.Domain.Tests.Configuration;

/// <summary>
/// CONFIG-FOUNDATION-P1-03 — ConfigurationDefinitionCatalog es la fuente de verdad que decide qué
/// key/scope/tipo/valor es aceptable en org_settings. Estos tests verifican el catálogo en sí
/// (sin tocar EF/DB) — la prueba de que el guardrail de escritura realmente lo aplica vive en
/// OrgSettingsRepositoryConfigurationGuardrailTests (ERP.Infrastructure.Tests, contra Postgres real).
/// </summary>
public sealed class ConfigurationDefinitionCatalogTests
{
    public static IEnumerable<object[]> AllActiveKeys()
    {
        yield return new object[] { OrgSettingKeys.Invoice.DefaultDocTypeCode };
        yield return new object[] { OrgSettingKeys.Invoice.DefaultPaymentMethodCode };
        yield return new object[] { OrgSettingKeys.Invoice.DefaultPaymentTermId };
        yield return new object[] { OrgSettingKeys.Invoice.DefaultWarehouseId };
        yield return new object[] { OrgSettingKeys.Sales.ConsumerFinalMaxAmount };
        yield return new object[] { OrgSettingKeys.Presentation.DecimalSalesUnitPrice };
        yield return new object[] { OrgSettingKeys.Presentation.DecimalPurchaseUnitPrice };
        yield return new object[] { OrgSettingKeys.Presentation.DecimalQuantity };
        yield return new object[] { OrgSettingKeys.Presentation.DecimalPercentage };
        yield return new object[] { OrgSettingKeys.Presentation.DecimalTotalAmount };
        yield return new object[] { OrgSettingKeys.CompanyBranding.PrimaryColor };
        yield return new object[] { OrgSettingKeys.CompanyBranding.SecondaryColor };
        yield return new object[] { OrgSettingKeys.CompanyBranding.Slogan };
        yield return new object[] { OrgSettingKeys.CompanyBranding.DocumentFooterText };
        yield return new object[] { OrgSettingKeys.Catalog.MaxCategoryDepth };
    }

    [Theory]
    [MemberData(nameof(AllActiveKeys))]
    public void Catalog_contiene_todas_las_keys_activas_actuales(string key)
    {
        ConfigurationDefinitionCatalog.TryGet(key, out var definition).Should().BeTrue();
        definition!.Key.Should().Be(key);
    }

    [Fact]
    public void Catalog_no_tiene_keys_duplicadas_ni_scopes_vacios()
    {
        // La construcción estática ya lanzaría en el static ctor si hubiera un problema — acceder
        // a .All fuerza la inicialización; si este test corre, ya no lanzó.
        ConfigurationDefinitionCatalog.All.Should().HaveCount(AllActiveKeys().Count());
    }

    [Theory]
    [InlineData("ride.branding.primary_color_hex")]
    [InlineData("ride.branding.secondary_color_hex")]
    [InlineData("ride.branding.logo_storage_path")]
    [InlineData("ride.branding.footer_text")]
    [InlineData("decimal.quantity")]
    [InlineData("decimal.sales.unitPrice")]
    [InlineData("decimal.purchases.unitPrice")]
    [InlineData("decimal.percentage")]
    [InlineData("decimal.totalAmount")]
    public void Keys_legacy_eliminadas_no_estan_registradas(string legacyKey)
    {
        ConfigurationDefinitionCatalog.TryGet(legacyKey, out _).Should().BeFalse();
    }

    [Fact]
    public void Key_no_registrada_no_se_encuentra()
    {
        ConfigurationDefinitionCatalog.TryGet("invented.key.no.exists", out _).Should().BeFalse();
    }

    [Fact]
    public void invoice_default_warehouse_id_solo_permite_scope_Branch()
    {
        ConfigurationDefinitionCatalog.TryGet(
            OrgSettingKeys.Invoice.DefaultWarehouseId,
            out var definition
        );

        definition!.AllowedScopes.Should().BeEquivalentTo([OrgScope.Branch]);
        definition.AllowedScopes.Should().NotContain(OrgScope.Company);
    }

    [Fact]
    public void sales_consumer_final_max_amount_solo_Company_y_valida_no_negativo()
    {
        ConfigurationDefinitionCatalog.TryGet(
            OrgSettingKeys.Sales.ConsumerFinalMaxAmount,
            out var definition
        );

        definition!.AllowedScopes.Should().BeEquivalentTo([OrgScope.Company]);
        definition.IsValidValue("0.00").Should().BeTrue();
        definition.IsValidValue("150.75").Should().BeTrue();
        definition.IsValidValue("-1").Should().BeFalse();
        definition.IsValidValue("not-a-number").Should().BeFalse();
    }

    [Theory]
    [InlineData(OrgSettingKeys.Presentation.DecimalSalesUnitPrice)]
    [InlineData(OrgSettingKeys.Presentation.DecimalPurchaseUnitPrice)]
    [InlineData(OrgSettingKeys.Presentation.DecimalQuantity)]
    [InlineData(OrgSettingKeys.Presentation.DecimalPercentage)]
    [InlineData(OrgSettingKeys.Presentation.DecimalTotalAmount)]
    public void presentation_decimal_valida_rango_0_a_6(string key)
    {
        ConfigurationDefinitionCatalog.TryGet(key, out var definition);

        definition!.IsValidValue("0").Should().BeTrue();
        definition.IsValidValue("6").Should().BeTrue();
        definition.IsValidValue("-1").Should().BeFalse();
        definition.IsValidValue("7").Should().BeFalse();
        definition.IsValidValue("99").Should().BeFalse();
    }

    [Theory]
    [InlineData(OrgSettingKeys.CompanyBranding.PrimaryColor)]
    [InlineData(OrgSettingKeys.CompanyBranding.SecondaryColor)]
    public void company_branding_color_valida_hex_pero_permite_null_o_vacio(string key)
    {
        ConfigurationDefinitionCatalog.TryGet(key, out var definition);

        definition!.IsValidValue("#1E88E5").Should().BeTrue();
        definition.IsValidValue("#FFF").Should().BeTrue();
        definition.IsValidValue(null).Should().BeTrue();
        definition.IsValidValue("").Should().BeTrue();
        definition.IsValidValue("   ").Should().BeTrue();
        definition.IsValidValue("not-a-color").Should().BeFalse();
        definition.IsValidValue("112233").Should().BeFalse();
    }

    [Fact]
    public void DataType_persistido_coincide_con_lo_declarado_en_la_definition()
    {
        ConfigurationDefinitionCatalog.TryGet(
            OrgSettingKeys.CompanyBranding.PrimaryColor,
            out var colorDef
        );
        colorDef!.DataType.Should().Be(ConfigurationDataType.ColorHex);
        colorDef.PersistedDataType.Should().Be(SettingDataType.String);

        ConfigurationDefinitionCatalog.TryGet(
            OrgSettingKeys.Sales.ConsumerFinalMaxAmount,
            out var decimalDef
        );
        decimalDef!.DataType.Should().Be(ConfigurationDataType.Decimal);
        decimalDef.PersistedDataType.Should().Be(SettingDataType.Decimal);

        ConfigurationDefinitionCatalog.TryGet(
            OrgSettingKeys.Invoice.DefaultWarehouseId,
            out var guidDef
        );
        guidDef!.DataType.Should().Be(ConfigurationDataType.Guid);
        guidDef.PersistedDataType.Should().Be(SettingDataType.Guid);
    }
}
