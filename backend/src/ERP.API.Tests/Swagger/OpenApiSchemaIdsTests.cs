using ERP.API.Contracts;
using ERP.API.Swagger;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Tests.Swagger;

/// <summary>API-DOC-V1 / Fase 2A — ids de esquema portables y operationId deterministas (sin host).</summary>
public sealed class OpenApiSchemaIdsTests
{
    private static OpenApiSchemaIds Ids() => new([typeof(ApiResponse<>).Assembly]);

    [Fact]
    public void Tipo_simple_usa_nombre_sin_namespace()
    {
        Ids().For(typeof(ApiResponseMeta)).Should().Be("ApiResponseMeta");
    }

    [Fact]
    public void Generico_se_compone_con_Of_y_sin_backtick_ni_assembly()
    {
        var ids = Ids();

        ids.For(typeof(ApiResponse<ApiResponseMeta>)).Should().Be("ApiResponseOfApiResponseMeta");
        ids.For(typeof(ApiResponse<object>)).Should().Be("ApiResponseOfObject");
        ids.For(typeof(ApiResponse<List<ApiResponseMeta>>))
            .Should()
            .Be("ApiResponseOfListOfApiResponseMeta");
        ids.For(typeof(ApiResponse<ApiResponseMeta[]>))
            .Should()
            .Be("ApiResponseOfApiResponseMetaArray");
    }

    [Fact]
    public void Id_es_estable_entre_instancias_y_llamadas()
    {
        var first = Ids();
        var second = Ids();

        first.For(typeof(ProblemDetails)).Should().Be(second.For(typeof(ProblemDetails)));
        first.For(typeof(ProblemDetails)).Should().Be("ProblemDetails");
    }

    [Fact]
    public void Tipos_con_el_mismo_nombre_en_ensamblados_ERP_se_califican_con_namespace()
    {
        // Simula dos ensamblados con un nombre repetido usando tipos propios de esta prueba.
        var ids = new OpenApiSchemaIds([typeof(NamespaceA.Dup).Assembly]);

        ids.AmbiguousNames.Should().Contain("Dup");
        var a = ids.For(typeof(NamespaceA.Dup));
        var b = ids.For(typeof(NamespaceB.Dup));

        a.Should().NotBe(b);
        a.Should().Contain("NamespaceA");
        b.Should().Contain("NamespaceB");
    }

    [Fact]
    public void Tipos_anidados_componen_Declarante_Anidado()
    {
        Ids().For(typeof(Outer.Inner)).Should().Be("OpenApiSchemaIdsTests.Outer.Inner");
    }

    [Theory]
    [InlineData("Foo`1[[Bar, Version=1.0.0.0]]", "Foo_1__Bar__Version_1.0.0.0__")]
    [InlineData("A.B-c_d", "A.B-c_d")]
    public void Sanitize_deja_solo_caracteres_validos_de_componente(string input, string expected)
    {
        OpenApiSchemaIds.Sanitize(input).Should().Be(expected);
    }

    [Fact]
    public void Todos_los_ids_generados_cumplen_el_patron_de_componentes_OpenAPI()
    {
        var ids = Ids();
        var types = new[]
        {
            typeof(ApiResponse<Dictionary<string, List<ApiResponseMeta>>>),
            typeof(ApiResponse<int?>),
            typeof(Outer.Inner),
        };

        foreach (var type in types)
            ids.For(type).Should().MatchRegex("^[A-Za-z0-9._-]+$");
    }

    public static class Outer
    {
        public sealed class Inner;
    }
}
