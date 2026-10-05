using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.AdditionalInfo;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace ERP.Application.Tests.ElectronicDocuments.AdditionalInfo;

/// <summary>
/// ZH-SRI-ANEXO26-PROVIDER-RUC-01 (ADR-038 D6, diseño §I.3) — reglas únicas de composición de
/// <c>infoAdicional</c>.
/// </summary>
public sealed class ElectronicDocumentAdditionalInfoComposerTests
{
    private static readonly AdditionalInfoCompositionContext Context = new(
        ElectronicDocumentType.Invoice,
        Guid.NewGuid(),
        Guid.NewGuid(),
        new DateOnly(2026, 11, 3),
        new ElectronicDocumentIssuerData("1790012345001", "Emisor S.A.", null, "Matriz", null, true)
    );

    private sealed class StubContributor(
        string id,
        int order,
        Result<IReadOnlyList<ElectronicDocumentAdditionalField>> result,
        bool applies = true
    ) : IElectronicDocumentAdditionalInfoContributor
    {
        public int Calls { get; private set; }
        public string Id => id;
        public int Order => order;

        public bool AppliesTo(ElectronicDocumentType documentType) => applies;

        public Task<Result<IReadOnlyList<ElectronicDocumentAdditionalField>>> ContributeAsync(
            AdditionalInfoCompositionContext context,
            CancellationToken ct = default
        )
        {
            Calls++;
            return Task.FromResult(result);
        }
    }

    private static StubContributor Fields(
        string id,
        int order,
        params (string Name, string Value)[] fields
    ) =>
        new(
            id,
            order,
            Result<IReadOnlyList<ElectronicDocumentAdditionalField>>.Success(
                fields.Select(f => new ElectronicDocumentAdditionalField(f.Name, f.Value)).ToList()
            )
        );

    private static ElectronicDocumentAdditionalInfoComposer Composer(
        params IElectronicDocumentAdditionalInfoContributor[] contributors
    ) => new(contributors, NullLogger<ElectronicDocumentAdditionalInfoComposer>.Instance);

    private static IReadOnlyList<ElectronicDocumentAdditionalField> Source(
        params (string Name, string Value)[] fields
    ) => fields.Select(f => new ElectronicDocumentAdditionalField(f.Name, f.Value)).ToList();

    [Fact]
    public async Task Combina_el_campo_normativo_y_el_campo_del_documento_normativo_primero()
    {
        var composer = Composer(Fields("n1", 100, ("RUC Proveedor", "1792146739001")));

        var result = await composer.ComposeAsync(
            Context,
            Source(("Observación", "Entrega en bodega"))
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        result
            .Value!.Select(f => (f.Name, f.Value))
            .Should()
            .Equal(("RUC Proveedor", "1792146739001"), ("Observación", "Entrega en bodega"));
    }

    [Fact]
    public async Task Ordena_contributors_por_Order_y_luego_por_Id_independiente_del_registro()
    {
        var composer = Composer(
            Fields("z", 200, ("C", "3")),
            Fields("b", 100, ("B", "2")),
            Fields("a", 100, ("A", "1"))
        );

        var result = await composer.ComposeAsync(Context, Source(("D", "4")));

        result.Value!.Select(f => f.Name).Should().Equal("A", "B", "C", "D");
    }

    [Fact]
    public async Task Rechaza_campos_duplicados_entre_contributors()
    {
        var composer = Composer(
            Fields("a", 100, ("RUC Proveedor", "1792146739001")),
            Fields("b", 200, (" ruc proveedor ", "1792146739001"))
        );

        var result = await composer.ComposeAsync(Context, []);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.AdditionalInfoInvalid);
        result.Error.Should().Contain("duplicado");
    }

    [Fact]
    public async Task Un_campo_del_documento_no_puede_usar_un_nombre_normativo_reservado()
    {
        var composer = Composer(Fields("n1", 100, ("RUC Proveedor", "1792146739001")));

        var result = await composer.ComposeAsync(
            Context,
            Source(("  RUC PROVEEDOR ", "9999999999001"))
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.AdditionalInfoInvalid);
        result.Error.Should().Contain("reservado");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Rechaza_un_nombre_vacio(string name)
    {
        var result = await Composer().ComposeAsync(Context, Source((name, "valor")));

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.AdditionalInfoInvalid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Rechaza_un_valor_vacio(string value)
    {
        var result = await Composer().ComposeAsync(Context, Source(("Observación", value)));

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.AdditionalInfoInvalid);
    }

    [Fact]
    public async Task Acepta_nombre_y_valor_de_300_caracteres_sin_alterarlos()
    {
        var name = new string('N', 300);
        var value = new string('v', 300);

        var result = await Composer().ComposeAsync(Context, Source((name, value)));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Single().Should().Be(new ElectronicDocumentAdditionalField(name, value));
    }

    [Fact]
    public async Task Rechaza_nombre_o_valor_de_301_caracteres_sin_truncar()
    {
        var longName = await Composer().ComposeAsync(Context, Source((new string('N', 301), "v")));
        var longValue = await Composer()
            .ComposeAsync(Context, Source(("Observación", new string('v', 301))));

        longName.IsSuccess.Should().BeFalse();
        longName.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.AdditionalInfoInvalid);
        longValue.IsSuccess.Should().BeFalse();
        longValue.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.AdditionalInfoInvalid);
        longValue.Error.Should().Contain("300");
    }

    [Fact]
    public async Task Acepta_15_campos_contando_los_normativos()
    {
        var composer = Composer(Fields("n1", 100, ("RUC Proveedor", "1792146739001")));
        var source = Enumerable.Range(1, 14).Select(i => ($"Campo {i}", "x")).ToArray();

        var result = await composer.ComposeAsync(Context, Source(source));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.Should().HaveCount(15);
    }

    [Fact]
    public async Task Rechaza_16_campos_contando_los_normativos()
    {
        var composer = Composer(Fields("n1", 100, ("RUC Proveedor", "1792146739001")));
        var source = Enumerable.Range(1, 15).Select(i => ($"Campo {i}", "x")).ToArray();

        var result = await composer.ComposeAsync(Context, Source(source));

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.AdditionalInfoInvalid);
        result.Error.Should().Contain("15");
    }

    [Fact]
    public async Task Un_contributor_que_falla_corta_la_composicion_con_su_codigo()
    {
        var failing = new StubContributor(
            "falla",
            100,
            Result<IReadOnlyList<ElectronicDocumentAdditionalField>>.ValidationFailure(
                "Configuración incompleta.",
                ApiResponseCodes.ElectronicDocuments.SystemProviderRucNotConfigured
            )
        );
        var later = Fields("despues", 200, ("Otro", "x"));

        var result = await Composer(failing, later)
            .ComposeAsync(Context, Source(("Observación", "x")));

        result.IsSuccess.Should().BeFalse();
        result
            .Code.Should()
            .Be(ApiResponseCodes.ElectronicDocuments.SystemProviderRucNotConfigured);
        result.Error.Should().Be("Configuración incompleta.");
        later.Calls.Should().Be(0);
    }

    [Fact]
    public async Task El_mismo_contexto_produce_el_mismo_resultado()
    {
        var composer = Composer(
            Fields("b", 200, ("Gran Contribuyente", "Res. 1")),
            Fields("a", 100, ("RUC Proveedor", "1792146739001"))
        );
        var source = Source(("Observación", "x"));

        var first = await composer.ComposeAsync(Context, source);
        var second = await composer.ComposeAsync(Context, source);

        second.Value.Should().Equal(first.Value);
    }

    [Fact]
    public async Task Un_contributor_que_no_aplica_al_tipo_no_se_invoca_ni_aporta_campos()
    {
        var notApplicable = new StubContributor(
            "otro-tipo",
            100,
            Result<IReadOnlyList<ElectronicDocumentAdditionalField>>.Success([
                new ElectronicDocumentAdditionalField("Solo guía", "x"),
            ]),
            applies: false
        );
        var source = Source(("Observación", "x"));

        var result = await Composer(notApplicable).ComposeAsync(Context, source);

        result.IsSuccess.Should().BeTrue(result.Error);
        notApplicable.Calls.Should().Be(0);
        result
            .Value.Should()
            .BeSameAs(source, "sin campos normativos el modelo queda idéntico al del provider");
    }
}
