using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.AdditionalInfo;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Tests.TestSupport;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.SriCatalogs.Constants;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.ElectronicDocuments.AdditionalInfo;

/// <summary>
/// ZH-SRI-ANEXO26-PROVIDER-RUC-01 (ADR-038 D7) — una prueba por fila de la regla fiscal definitiva
/// del RUC Proveedor (Ficha 2.34 Anexo 26). <c>EffectiveDate</c> es la fecha de aplicabilidad y se
/// compara solo con la fecha de emisión de negocio del comprobante.
/// </summary>
public sealed class SystemProviderRucAdditionalInfoContributorTests
{
    private static readonly DateOnly EffectiveDate = new(2026, 11, 3);

    private static AdditionalInfoCompositionContext Context(
        DateOnly issueDate,
        ElectronicDocumentType type = ElectronicDocumentType.Invoice
    ) =>
        new(
            type,
            Guid.NewGuid(),
            Guid.NewGuid(),
            issueDate,
            new ElectronicDocumentIssuerData("1790012345001", "Emisor S.A.", null, "Matriz", null, true)
        );

    private static Task<Result<IReadOnlyList<ElectronicDocumentAdditionalField>>> Contribute(
        Mock<ISystemProviderSettingsRepository> settings,
        DateOnly issueDate,
        ElectronicDocumentType type = ElectronicDocumentType.Invoice
    ) => new SystemProviderRucAdditionalInfoContributor(settings.Object).ContributeAsync(Context(issueDate, type));

    private static void ShouldEmitRuc(Result<IReadOnlyList<ElectronicDocumentAdditionalField>> result)
    {
        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.Should().Equal(
            new ElectronicDocumentAdditionalField(
                SriAdditionalInfoFieldNames.SystemProviderRuc,
                AdditionalInfoTestDoubles.ProviderRuc
            )
        );
    }

    private static void ShouldEmitNothing(Result<IReadOnlyList<ElectronicDocumentAdditionalField>> result)
    {
        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.Should().BeEmpty();
    }

    private static void ShouldFailClosed(Result<IReadOnlyList<ElectronicDocumentAdditionalField>> result)
    {
        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.SystemProviderRucNotConfigured);
        result.Error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Fila_A_sin_fecha_y_deshabilitado_no_emite_campo()
    {
        ShouldEmitNothing(await Contribute(AdditionalInfoTestDoubles.ProviderSettings(false, null), EffectiveDate));
    }

    [Fact]
    public async Task Fila_A_sin_configuracion_no_emite_campo()
    {
        var repo = new Mock<ISystemProviderSettingsRepository>();
        repo.Setup(r => r.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync((SystemProviderSettings?)null);

        ShouldEmitNothing(await Contribute(repo, EffectiveDate));
    }

    [Fact]
    public async Task Fila_B_sin_fecha_y_habilitado_falla_cerrado()
    {
        ShouldFailClosed(await Contribute(AdditionalInfoTestDoubles.ProviderSettings(true, null), EffectiveDate));
    }

    [Fact]
    public async Task Fila_C_emision_anterior_no_emite_campo_aunque_este_habilitado()
    {
        var settings = AdditionalInfoTestDoubles.ProviderSettings(true, EffectiveDate);

        ShouldEmitNothing(await Contribute(settings, EffectiveDate.AddDays(-1)));
    }

    [Fact]
    public async Task Fila_C_emision_anterior_no_emite_campo_estando_deshabilitado()
    {
        var settings = AdditionalInfoTestDoubles.ProviderSettings(false, EffectiveDate);

        ShouldEmitNothing(await Contribute(settings, EffectiveDate.AddDays(-1)));
    }

    [Fact]
    public async Task Fila_C_emision_anterior_no_evalua_el_RUC()
    {
        var settings = AdditionalInfoTestDoubles.ProviderSettings(true, EffectiveDate);
        AdditionalInfoTestDoubles.ForceRuc((await settings.Object.GetAsync())!, "123");

        ShouldEmitNothing(await Contribute(settings, EffectiveDate.AddDays(-1)));
    }

    [Fact]
    public async Task Fila_D_emision_igual_a_la_fecha_emite_RUC_Proveedor()
    {
        ShouldEmitRuc(await Contribute(AdditionalInfoTestDoubles.ProviderSettings(true, EffectiveDate), EffectiveDate));
    }

    [Fact]
    public async Task Fila_D_emision_posterior_emite_RUC_Proveedor()
    {
        var settings = AdditionalInfoTestDoubles.ProviderSettings(true, EffectiveDate);

        ShouldEmitRuc(await Contribute(settings, EffectiveDate.AddDays(30)));
    }

    [Fact]
    public async Task Fila_E_exigible_y_deshabilitado_falla_cerrado()
    {
        var settings = AdditionalInfoTestDoubles.ProviderSettings(false, EffectiveDate);

        ShouldFailClosed(await Contribute(settings, EffectiveDate));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("179214673900A")]
    public async Task Fila_F_exigible_con_RUC_ausente_o_invalido_falla_cerrado(string? ruc)
    {
        var settings = AdditionalInfoTestDoubles.ProviderSettings(true, EffectiveDate);
        AdditionalInfoTestDoubles.ForceRuc((await settings.Object.GetAsync())!, ruc);

        ShouldFailClosed(await Contribute(settings, EffectiveDate));
    }

    [Fact]
    public async Task El_resultado_depende_solo_de_la_fecha_de_emision_no_del_reloj()
    {
        // El contributor no recibe ni lee reloj alguno: la decisión es función de (configuración, IssueDate).
        // Una fecha de emisión muy anterior a "hoy" sigue sin campo, y una muy posterior sigue con campo,
        // en cualquier momento en que se ejecute (vista previa, pipeline o regeneración).
        var settings = AdditionalInfoTestDoubles.ProviderSettings(true, EffectiveDate);

        var before1 = await Contribute(settings, new DateOnly(2020, 1, 1));
        var before2 = await Contribute(settings, new DateOnly(2020, 1, 1));
        var after1 = await Contribute(settings, new DateOnly(2099, 12, 31));
        var after2 = await Contribute(settings, new DateOnly(2099, 12, 31));

        before1.Value.Should().BeEmpty();
        before2.Value.Should().Equal(before1.Value);
        ShouldEmitRuc(after1);
        after2.Value.Should().Equal(after1.Value);
        typeof(SystemProviderRucAdditionalInfoContributor)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(p => p.ParameterType)
            .Should()
            .Equal([typeof(ISystemProviderSettingsRepository)], "no debe depender de un reloj ni de tenant/empresa");
    }

    [Theory]
    [InlineData(ElectronicDocumentType.Invoice, true)]
    [InlineData(ElectronicDocumentType.CreditNote, true)]
    [InlineData(ElectronicDocumentType.Retention, true)]
    [InlineData(ElectronicDocumentType.DebitNote, false)]
    [InlineData(ElectronicDocumentType.ShippingGuide, false)]
    [InlineData(ElectronicDocumentType.PurchaseSettlement, false)]
    public void Aplica_a_los_tipos_electronicos_implementados_01_04_07(ElectronicDocumentType type, bool applies)
    {
        var contributor = new SystemProviderRucAdditionalInfoContributor(
            AdditionalInfoTestDoubles.ProviderSettings(true, EffectiveDate).Object
        );

        contributor.AppliesTo(type).Should().Be(applies);
    }

    [Theory]
    [InlineData(ElectronicDocumentType.Invoice)]
    [InlineData(ElectronicDocumentType.CreditNote)]
    [InlineData(ElectronicDocumentType.Retention)]
    public async Task Emite_el_mismo_campo_para_01_04_07(ElectronicDocumentType type)
    {
        var settings = AdditionalInfoTestDoubles.ProviderSettings(true, EffectiveDate);

        ShouldEmitRuc(await Contribute(settings, EffectiveDate, type));
    }
}
