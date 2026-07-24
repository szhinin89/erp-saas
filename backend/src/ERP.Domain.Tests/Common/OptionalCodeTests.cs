using ERP.Domain.Common;
using FluentAssertions;

namespace ERP.Domain.Tests.Common;

public sealed class OptionalCodeTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_valores_sin_contenido_real_devuelve_null(string? blank)
    {
        OptionalCode.Normalize(blank).Should().BeNull();
    }

    [Fact]
    public void Normalize_recorta_espacios_de_un_codigo_con_contenido_real()
    {
        OptionalCode.Normalize(" ICE01 ").Should().Be("ICE01");
    }
}
