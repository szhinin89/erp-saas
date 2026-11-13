using ERP.Application.Common;
using ERP.Application.Modules.Integration;
using ERP.Application.Modules.Integration.UseCases;
using ERP.Domain.Modules.Company.Entities;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Integration;

/// <summary>
/// F-01 (API-DOC-V1): <c>brandingJson</c> se conserva en el contrato v1 pero no está soportado.
/// Un valor no vacío se rechaza (422 vía FluentValidation) y el handler ya no lo entrega al parámetro
/// <c>website</c> de <c>CreateManagedCompanyAsync</c>; no existe otro destino de persistencia.
/// </summary>
public sealed class IntegrationBrandingJsonTests
{
    private const string ValidRuc = "1710034065001";
    private const int WebsiteArgumentIndex = 12; // posición de `website` en CreateManagedCompanyAsync

    private static readonly CreateIntegrationCompanyCommandValidator Validator = new();

    private static CreateIntegrationCompanyCommand Command(string? brandingJson, string taxId = ValidRuc) =>
        new(new IntegrationCompanyCreateRequest(
            Guid.NewGuid(), taxId, "ACME", "Dir", Guid.NewGuid(), BrandingJson: brandingJson));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Acepta_BrandingJson_nulo_vacio_o_solo_espacios(string? branding) =>
        Validator.Validate(Command(branding)).IsValid.Should().BeTrue();

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"logo\":\"x.png\",\"primary\":\"#fff\"}")]
    [InlineData("texto cualquiera")]
    [InlineData(" {} ")]
    public void Rechaza_BrandingJson_no_vacio_corto(string branding)
    {
        var result = Validator.Validate(Command(branding));
        result.IsValid.Should().BeFalse();
        var error = result.Errors.Should().ContainSingle().Subject;
        error.PropertyName.Should().Be("Request.BrandingJson");
        error.ErrorMessage.Should().Be(CreateIntegrationCompanyCommandValidator.UnsupportedBrandingMessage);
    }

    [Fact]
    public void Rechaza_BrandingJson_largo_aunque_exceda_el_limite_de_website()
    {
        var long300 = "{\"k\":\"" + new string('a', 300) + "\"}";
        var result = Validator.Validate(Command(long300));
        result.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be(CreateIntegrationCompanyCommandValidator.UnsupportedBrandingMessage);
    }

    [Fact]
    public void RUC_invalido_y_branding_no_vacio_se_informan_por_separado()
    {
        var result = Validator.Validate(Command("{}", taxId: "123"));

        result.Errors.Select(e => (e.PropertyName, e.ErrorMessage)).Should().BeEquivalentTo(new[]
        {
            ("Request.TaxId", CreateIntegrationCompanyCommandValidator.InvalidTaxIdMessage),
            ("Request.BrandingJson", CreateIntegrationCompanyCommandValidator.UnsupportedBrandingMessage),
        });
    }

    [Fact]
    public void RUC_invalido_con_branding_vacio_solo_informa_el_RUC()
    {
        var result = Validator.Validate(Command("", taxId: "123"));
        result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be("Request.TaxId");
    }

    [Fact]
    public void Branding_no_vacio_con_RUC_valido_solo_informa_el_branding()
    {
        var result = Validator.Validate(Command("{}"));
        result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be("Request.BrandingJson");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{\"logo\":\"x.png\"}")]
    public async Task El_handler_nunca_entrega_branding_al_parametro_website(string? branding)
    {
        var provisioning = new Mock<ICompanyProvisioningService>();
        provisioning
            .Setup(p => p.CreateManagedCompanyAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>(), It.IsAny<InitialCompanyAdministrator?>()))
            .ReturnsAsync(Company.CreateManaged(Guid.NewGuid(), ValidRuc, "ACME", createdBy: Guid.NewGuid()));

        var result = await new CreateIntegrationCompanyHandler(provisioning.Object)
            .Handle(Command(branding), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var args = provisioning.Invocations.Single().Arguments;
        args[WebsiteArgumentIndex].Should().BeNull("el branding no se persiste en Company.Website");
        args.OfType<string>().Should().NotContain(a => a == branding && !string.IsNullOrEmpty(branding));
    }
}
