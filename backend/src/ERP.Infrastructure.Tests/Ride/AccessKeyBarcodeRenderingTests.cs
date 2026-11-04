using ERP.Application.Modules.Ride.Parsers;
using ERP.Application.Modules.Ride.Rendering;
using ERP.Application.Modules.Ride.Templates;
using ERP.Domain.Modules.Ride.ValueObjects;
using ERP.Infrastructure.Ride.Rendering;
using ERP.Infrastructure.Ride.Rendering.Sections;
using FluentAssertions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SkiaSharp;
using ZXing;
using ZXing.Common;

namespace ERP.Infrastructure.Tests.Ride;

public sealed class AccessKeyBarcodeRenderingTests
{
    [Theory]
    [InlineData("invoice")]
    [InlineData("credit-note")]
    [InlineData("retention")]
    public async Task Header_preserves_complete_key_and_scannable_code128(string kind)
    {
        IRideDocumentLayout layout = kind switch
        {
            "retention" => RetentionRideRenderingFixtures.Full(),
            "credit-note" => CreditNote(),
            _ => RideRenderingFixtures.Full(),
        };
        var key = layout is RetentionRideDocumentLayout r ? r.Header.AccessKey
            : ((InvoiceRideDocumentLayout)layout).Header.AccessKey;
        key.Value.Should().HaveLength(49);
        var barcode = RideBarcodeGeneratorTestFactory.Create().Generate(key);
        var header = Document.Create(d => d.Page(p =>
        {
            p.Size(PageSizes.A4);
            p.Margin(20);
            p.Content().Element(c =>
            {
                if (layout is RetentionRideDocumentLayout retention)
                    RetentionHeaderSection.Compose(c, retention, null, barcode);
                else
                    HeaderSection.Compose(c, (InvoiceRideDocumentLayout)layout, null, barcode);
            });
        }));
        var svg = System.Xml.Linq.XDocument.Parse(header.GenerateSvg().Single());
        svg.Descendants().Where(e => e.Name.LocalName == "text")
            .Should().Contain(e => e.Value.Trim() == key.Value, "los 49 dígitos deben permanecer juntos");

        // Decodifica el encabezado compuesto, no el PNG original del generador.
        var png = header.GenerateImages(new ImageGenerationSettings { RasterDpi = 300 }).Single();
        if (Environment.GetEnvironmentVariable("RIDE_BARCODE_SAMPLES") is { Length: > 0 } preview)
        {
            Directory.CreateDirectory(preview);
            await File.WriteAllBytesAsync(Path.Combine(preview, kind + "-header.png"), png);
        }
        using var decoded = SKBitmap.Decode(png);
        // Región del encabezado derecho: excluye bordes de caja y el resto de la página A4.
        // El escáner recibe las barras ya escaladas por QuestPDF, sin redimensionarlas.
        const float pixelsPerPoint = 300f / 72;
        var region = SKRectI.Round(new SKRect(
            (20 + (PageSizes.A4.Width - 40) * 6 / 11 + 8) * pixelsPerPoint,
            28 * pixelsPerPoint, (PageSizes.A4.Width - 28) * pixelsPerPoint, 260 * pixelsPerPoint));
        using var bitmap = new SKBitmap(region.Width, region.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        using (var canvas = new SKCanvas(bitmap))
            canvas.DrawBitmap(decoded, -region.Left, -region.Top);
        var reader = new BarcodeReaderGeneric
        {
            Options = new DecodingOptions { PossibleFormats = [BarcodeFormat.CODE_128], TryHarder = true },
        };
        var result = reader.Decode(new RGBLuminanceSource(bitmap.Bytes, bitmap.Width, bitmap.Height,
            RGBLuminanceSource.BitmapFormat.BGRA32));
        if (Environment.GetEnvironmentVariable("RIDE_BARCODE_SAMPLES") is { Length: > 0 } debug)
        {
            using var image = SKImage.FromBitmap(bitmap);
            using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
            await File.WriteAllBytesAsync(Path.Combine(debug, kind + "-crop.png"), encoded.ToArray());
            await File.WriteAllBytesAsync(Path.Combine(debug, kind + "-source.png"), barcode);
        }
        result.Should().NotBeNull();
        result!.Text.Should().Be(key.Value);

        var renderer = new QuestPdfRideRenderer(RideQrCodeGeneratorTestFactory.Create(),
            RideBarcodeGeneratorTestFactory.Create(), new NoOpFileStorage());
        var pdf = await renderer.RenderAsync(layout);
        pdf.Should().NotBeEmpty();
        // Exportación opt-in para revisión del propietario, sin modificar golden files.
        if (Environment.GetEnvironmentVariable("RIDE_BARCODE_SAMPLES") is { Length: > 0 } output)
        {
            Directory.CreateDirectory(output);
            await File.WriteAllBytesAsync(Path.Combine(output, kind + ".pdf"), pdf);
            await File.WriteAllBytesAsync(Path.Combine(output, kind + "-header.png"), png);
        }
    }

    private static InvoiceRideDocumentLayout CreditNote()
    {
        const string xml = """
            <notaCredito><infoTributaria><ambiente>1</ambiente><tipoEmision>1</tipoEmision>
            <razonSocial>EMISOR DE PRUEBA</razonSocial><ruc>1790012345001</ruc>
            <claveAcceso>0107202604179001234500110010010000000011234567811</claveAcceso>
            <codDoc>04</codDoc><estab>001</estab><ptoEmi>001</ptoEmi><secuencial>000000001</secuencial>
            </infoTributaria><infoNotaCredito><fechaEmision>01/07/2026</fechaEmision>
            <dirEstablecimiento>Dirección de prueba</dirEstablecimiento><moneda>USD</moneda>
            <obligadoContabilidad>SI</obligadoContabilidad><tipoIdentificacionComprador>05</tipoIdentificacionComprador>
            <identificacionComprador>1710034065</identificacionComprador><razonSocialComprador>CLIENTE DE PRUEBA</razonSocialComprador>
            <codDocModificado>01</codDocModificado><numDocModificado>001-001-000000001</numDocModificado>
            <fechaEmisionDocSustento>01/07/2026</fechaEmisionDocSustento><totalSinImpuestos>10</totalSinImpuestos>
            <valorModificacion>10</valorModificacion><motivo>Devolución de prueba</motivo></infoNotaCredito>
            <detalles><detalle><codigoInterno>TEST</codigoInterno><descripcion>Producto de prueba</descripcion>
            <cantidad>1</cantidad><precioUnitario>10</precioUnitario><descuento>0</descuento>
            <precioTotalSinImpuesto>10</precioTotalSinImpuesto><impuestos/></detalle></detalles></notaCredito>
            """;
        var model = new CreditNoteRideXmlParser().Parse(xml);
        model.IsSuccess.Should().BeTrue(model.Error);
        return (InvoiceRideDocumentLayout)new CreditNoteRideTemplate().Compose(model.Value!,
            RideBranding.Empty(), new RideLinePrecision(2, 2));
    }
}
