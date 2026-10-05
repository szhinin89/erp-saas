using ERP.Application.Common;
using ERP.Application.Modules.Communications.Templates;
using ERP.Domain.Modules.Communications.Enums;
using FluentAssertions;

namespace ERP.Application.Tests.Communications;

/// <summary>ZH-COMMUNICATIONS-TEMPLATES-01 — renderer único: fail-closed, escape HTML, determinístico.</summary>
public sealed class CommunicationTemplateRendererTests
{
    private const string Key = "TEST_TEMPLATE";

    private sealed record Model(
        IReadOnlyDictionary<string, string?> Values,
        string TemplateKey = Key
    ) : ICommunicationTemplateModel
    {
        public IReadOnlyDictionary<string, string?> ToVariables() => Values;
    }

    private static CommunicationTemplateDefinition Template(
        string subject = "Hola {{Name}}",
        string? html = "<p>Hola {{Name}}</p><p>{{Note}}</p>",
        string? text = "Hola {{Name}}\n{{Note}}",
        params CommunicationTemplateVariable[] variables
    ) =>
        new(
            Key,
            1,
            CommunicationTemplateSource.Default,
            subject,
            html,
            text,
            variables.Length > 0 ? variables : [new("Name"), new("Note", Required: false)]
        );

    private static Model Values(params (string Name, string? Value)[] values) =>
        new(values.ToDictionary(v => v.Name, v => v.Value));

    private static Result<RenderedCommunicationTemplate> Render(
        CommunicationTemplateDefinition template,
        ICommunicationTemplateModel model
    ) => CommunicationTemplateRenderer.Render(template, model);

    // 1-4. Template válido: asunto, HTML y texto.
    [Fact]
    public void Template_valido_renderiza_asunto_html_y_texto()
    {
        var result = Render(Template(), Values(("Name", "Ana"), ("Note", "Gracias")));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Subject.Should().Be("Hola Ana");
        result.Value.Html.Should().Be("<p>Hola Ana</p><p>Gracias</p>");
        result.Value.Text.Should().Be("Hola Ana\nGracias");
        result.Value.Usage.Key.Should().Be(Key);
        result.Value.Usage.Version.Should().Be(1);
        result.Value.Usage.Source.Should().Be(CommunicationTemplateSource.Default);
    }

    [Fact]
    public void Placeholders_admiten_espacios_internos()
    {
        Render(Template(subject: "Hola {{ Name }}"), Values(("Name", "Ana")))
            .Value!.Subject.Should()
            .Be("Hola Ana");
    }

    // 5. Variable obligatoria faltante.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Variable_obligatoria_ausente_falla_cerrado(string? name)
    {
        var result = Render(Template(), Values(("Name", name)));

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Communications.TemplateRenderFailed);
        result.Error.Should().Contain("Name");
    }

    [Fact]
    public void Variable_obligatoria_no_aportada_falla_cerrado()
    {
        Render(Template(), Values(("Note", "x")))
            .Code.Should()
            .Be(ApiResponseCodes.Communications.TemplateRenderFailed);
    }

    // 6. Placeholder desconocido en el template.
    [Fact]
    public void Placeholder_no_declarado_en_el_contrato_falla()
    {
        var result = Render(
            Template(html: "<p>{{Name}} {{Password}}</p>"),
            Values(("Name", "Ana"))
        );

        result.Code.Should().Be(ApiResponseCodes.Communications.TemplateInvalid);
        result.Error.Should().Contain("Password");
    }

    // 7. Placeholder sin resolver / mal formado.
    [Theory]
    [InlineData("Hola {{Name")]
    [InlineData("Hola Name}}")]
    [InlineData("Hola {{1Name}}")]
    [InlineData("Hola {{Name.Address}}")]
    public void Placeholder_mal_formado_o_sin_resolver_falla(string subject)
    {
        Render(Template(subject: subject), Values(("Name", "Ana")))
            .Code.Should()
            .Be(ApiResponseCodes.Communications.TemplateInvalid);
    }

    [Fact]
    public void Variable_no_declarada_en_el_modelo_falla()
    {
        var result = Render(Template(), Values(("Name", "Ana"), ("Token", "secreto-123")));

        result.Code.Should().Be(ApiResponseCodes.Communications.TemplateRenderFailed);
        result
            .Error.Should()
            .NotContain("secreto-123", "los errores nombran variables, nunca valores");
    }

    [Fact]
    public void Modelo_de_otro_template_falla()
    {
        var other = new Model(
            new Dictionary<string, string?> { ["Name"] = "Ana" },
            TemplateKey: "OTRO"
        );

        Render(Template(), other)
            .Code.Should()
            .Be(ApiResponseCodes.Communications.TemplateRenderFailed);
    }

    // 8 y 10. Escape HTML; no se ejecuta ni se inserta HTML crudo.
    [Fact]
    public void Html_escapa_toda_variable_y_nunca_inserta_script()
    {
        var result = Render(
            Template(),
            Values(("Name", "<script>alert(1)</script>"), ("Note", "<b>raw</b> & \"q\""))
        );

        result
            .Value!.Html.Should()
            .Be(
                "<p>Hola &lt;script&gt;alert(1)&lt;/script&gt;</p><p>&lt;b&gt;raw&lt;/b&gt; &amp; &quot;q&quot;</p>"
            );
        result.Value.Html.Should().NotContain("<script>").And.NotContain("<b>");
        result
            .Value.Text.Should()
            .Be(
                "Hola <script>alert(1)</script>\n<b>raw</b> & \"q\"",
                "el texto plano no lleva HTML ni escape"
            );
    }

    [Fact]
    public void Un_valor_con_sintaxis_de_placeholder_no_se_vuelve_a_interpretar()
    {
        var result = Render(Template(), Values(("Name", "{{Note}}"), ("Note", "x")));

        result.Value!.Subject.Should().Be("Hola {{Note}}");
    }

    // 9. Determinístico.
    [Fact]
    public void Mismo_input_produce_el_mismo_output()
    {
        var template = Template();
        var model = Values(("Name", "Ana"), ("Note", "n"));

        Render(template, model).Value.Should().Be(Render(template, model).Value);
    }

    // 11. Nulos según contrato.
    [Fact]
    public void Variable_opcional_nula_se_renderiza_vacia()
    {
        var result = Render(Template(), Values(("Name", "Ana"), ("Note", null)));

        result.Value!.Html.Should().Be("<p>Hola Ana</p><p></p>");
        result.Value.Text.Should().Be("Hola Ana\n");
    }

    // 12. Unicode.
    [Fact]
    public void Unicode_tildes_y_enie_se_conservan()
    {
        var result = Render(Template(), Values(("Name", "Peña Ñandú José"), ("Note", "€ ✓")));

        result.Value!.Subject.Should().Be("Hola Peña Ñandú José");
        result.Value.Text.Should().Be("Hola Peña Ñandú José\n€ ✓");
        System
            .Net.WebUtility.HtmlDecode(result.Value.Html!)
            .Should()
            .Be("<p>Hola Peña Ñandú José</p><p>€ ✓</p>");
    }

    [Fact]
    public void Asunto_es_texto_de_una_linea()
    {
        Render(Template(), Values(("Name", "Ana\r\nBcc: x@y.com")))
            .Value!.Subject.Should()
            .Be("Hola Ana  Bcc: x@y.com");
    }

    [Fact]
    public void Template_sin_asunto_o_sin_cuerpo_es_invalido()
    {
        Render(Template(subject: " "), Values(("Name", "Ana")))
            .Code.Should()
            .Be(ApiResponseCodes.Communications.TemplateInvalid);
        Render(Template(html: null, text: null), Values(("Name", "Ana")))
            .Code.Should()
            .Be(ApiResponseCodes.Communications.TemplateInvalid);
    }

    [Fact]
    public void Resultado_que_excede_los_limites_de_la_comunicacion_falla()
    {
        Render(Template(), Values(("Name", new string('x', 400))))
            .Code.Should()
            .Be(ApiResponseCodes.Communications.TemplateRenderFailed);
    }

    [Fact]
    public void Defaults_registrados_cumplen_su_propio_contrato()
    {
        CommunicationDefaultTemplates.All.Should().NotBeEmpty();
        CommunicationDefaultTemplates
            .All.Should()
            .OnlyContain(t => CommunicationTemplateRenderer.Validate(t).IsSuccess);
        CommunicationDefaultTemplates
            .All.Should()
            .OnlyContain(t => t.Source == CommunicationTemplateSource.Default && t.Version >= 1);
    }
}
