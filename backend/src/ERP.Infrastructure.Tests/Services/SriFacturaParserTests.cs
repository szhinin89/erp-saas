using FluentAssertions;
using ERP.Application.Common.Exceptions;
using ERP.Domain.Common.Validators;
using ERP.Infrastructure.Services;

namespace ERP.Infrastructure.Tests.Services;

public sealed class SriFacturaParserTests
{
    private static string ValidRucSociedad()
    {
        for (var d = 0; d <= 9; d++)
        {
            var r = "179001691" + d + "001";
            if (RucValidator.EsRucValido(r))
                return r;
        }

        throw new InvalidOperationException("RUC de prueba no encontrado.");
    }

    private static string LoadEjemploXml(string clave49, string ruc)
    {
        var dir  = AppContext.BaseDirectory;
        var path = Path.Combine(dir, "TestData", "FacturaEjemplo.xml");
        File.Exists(path).Should().BeTrue($"Falta {path} (CopyToOutputDirectory).");
        return File.ReadAllText(path)
            .Replace("__CLAVE__", clave49, StringComparison.Ordinal)
            .Replace("__RUC__", ruc, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ParseAsync_archivo_ejemplo_extrae_cabecera_y_detalle()
    {
        var prefix48 = new string('5', 48);
        var clave    = ClaveAcceso49TestFactory.FromPrefix48(prefix48);
        var ruc      = ValidRucSociedad();
        var xml      = LoadEjemploXml(clave, ruc);

        var parser = new SriFacturaParser();
        await using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml));

        var r = await parser.ParseAsync(ms, CancellationToken.None);

        r.ClaveAcceso.Should().Be(clave);
        r.RucProveedor.Should().Be(ruc);
        r.NumeroFactura.Should().Be("001-001-000000001");
        r.FechaEmision.Should().Be(new DateTime(2026, 5, 9));
        r.Subtotal.Should().Be(100m);
        r.Impuesto.Should().Be(12m);
        r.Total.Should().Be(112m);
        r.Items.Should().HaveCount(1);
        r.Items[0].Descripcion.ToLowerInvariant().Should().Contain("prueba");
    }

    [Fact]
    public async Task ParseAsync_xml_invalido_lanza_XmlParseException()
    {
        var parser = new SriFacturaParser();
        await using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("<noEsFactura/>"));

        var act = async () => await parser.ParseAsync(ms, CancellationToken.None);
        await act.Should().ThrowAsync<XmlParseException>();
    }
}
