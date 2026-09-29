using System.Text;
using ERP.API.Controllers;
using ERP.API.Controllers.InitialLoad;
using ERP.API.Controllers.Purchases;
using ERP.API.Tests.Support;
using ERP.Application.Common;
using ERP.Application.Common.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.Uploads;

/// <summary>
/// ZH-API-THIN-MEDIA-UPLOAD-01 — contrato de los 5 endpoints multipart que convierten
/// <see cref="IFormFile"/> en <see cref="MediaUploadContent"/>, fijado antes de unificar la
/// conversión: lo que recibe Application (nombre, ContentType, tamaño, bytes, stream legible y en
/// posición 0 durante el Send), el stream liberado al terminar la acción, el 400 propio de cada
/// endpoint para archivo ausente/vacío (sin llamar al mediator) y el error del handler sin cambios.
/// </summary>
public sealed class MediaUploadEndpointsContractTests
{
    private sealed class StubWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "ERP.API.Tests";
        public string WebRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private static T WithContext<T>(T controller)
        where T : ControllerBase
    {
        var services = new ServiceCollection();
        services.AddSingleton<IWebHostEnvironment>(new StubWebHostEnvironment());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() },
        };
        return controller;
    }

    private static IFormFile File(byte[] bytes, string name, string contentType) =>
        new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", name)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType,
        };

    /// <summary>Lo que vio Application dentro del Send (el stream ya no es legible después).</summary>
    private sealed record Seen(string FileName, string ContentType, long SizeBytes, long Position, bool CanRead, byte[] Bytes, Stream Stream);

    private static MediaUploadContent UploadOf(object command) =>
        command.GetType().GetProperties().Select(p => p.GetValue(command)).OfType<MediaUploadContent>().Single();

    private static Seen Capture(object command)
    {
        var upload = UploadOf(command);
        var position = upload.Content.Position;
        var canRead = upload.Content.CanRead;
        using var copy = new MemoryStream();
        upload.Content.CopyTo(copy);
        return new Seen(upload.FileName, upload.ContentType, upload.SizeBytes, position, canRead, copy.ToArray(), upload.Content);
    }

    public static TheoryData<string> Endpoints =>
        new() { "companies/logo", "companies/logo-alt", "sri/certificate", "initial-load/upload", "purchase-reception/import" };

    private static Task<IActionResult> Invoke(string endpoint, IFormFile? file, Func<object, object> mediatorHandler)
    {
        var mediator = new StubMediator(mediatorHandler);
        return endpoint switch
        {
            "companies/logo" => WithContext(new CompanyProfileController(mediator)).UploadLogo(file),
            "companies/logo-alt" => WithContext(new CompanyProfileController(mediator)).UploadLogoAlt(file),
            "sri/certificate" => WithContext(new ElectronicInvoicingController(mediator)).UploadCertificate(file),
            "initial-load/upload" => WithContext(new InitialLoadController(mediator)).Upload(Guid.NewGuid(), file, default),
            "purchase-reception/import" => WithContext(new PurchaseReceptionController(mediator)).Import(file, default),
            _ => throw new ArgumentOutOfRangeException(nameof(endpoint)),
        };
    }

    /// <summary>Respuesta de éxito tipada según el command recibido (el handler real no importa aquí).</summary>
    private static object Ok(object command) => Result(command, success: true);

    private static object Result(object command, bool success)
    {
        var responseType = command.GetType().GetInterfaces()
            .Single(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(MediatR.IRequest<>))
            .GetGenericArguments()[0];
        var valueType = responseType.GetGenericArguments()[0];
        return success
            ? responseType.GetMethod("Success")!.Invoke(null, [null, null])!
            : responseType.GetMethod("ValidationFailure")!.Invoke(null, ["Formato de archivo no permitido.", ApiResponseCodes.Common.ValidationError])!;
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Archivo_valido_llega_a_Application_con_nombre_tipo_tamano_y_bytes_y_el_stream_se_libera_al_final(string endpoint)
    {
        var bytes = Encoding.UTF8.GetBytes($"contenido de prueba {endpoint} ✓");
        Seen? seen = null;

        var response = await Invoke(endpoint, File(bytes, "archivo prueba.bin", "application/x-test"), command =>
        {
            seen = Capture(command);
            return Ok(command);
        });

        response.Should().BeOfType<OkObjectResult>();
        seen.Should().NotBeNull();
        seen!.FileName.Should().Be("archivo prueba.bin");
        seen.ContentType.Should().Be("application/x-test");
        seen.SizeBytes.Should().Be(bytes.Length);
        seen.Position.Should().Be(0, "el stream llega rebobinado");
        seen.CanRead.Should().BeTrue("no se libera antes del Send");
        seen.Bytes.Should().Equal(bytes);
        seen.Stream.CanRead.Should().BeFalse("la acción libera el buffer al terminar");
    }

    [Theory]
    [InlineData("companies/logo", "Debe adjuntar un archivo de imagen.")]
    [InlineData("companies/logo-alt", "Debe adjuntar un archivo de imagen.")]
    [InlineData("sri/certificate", "Debe adjuntar el archivo del certificado.")]
    [InlineData("initial-load/upload", "Debe adjuntar un archivo.")]
    [InlineData("purchase-reception/import", "Debe adjuntar un archivo.")]
    public async Task Archivo_ausente_o_vacio_responde_400_con_el_mensaje_del_endpoint_sin_llamar_al_mediator(string endpoint, string message)
    {
        foreach (var file in new[] { null, File([], "vacio.bin", "application/x-test") })
        {
            var called = false;

            var response = await Invoke(endpoint, file, command =>
            {
                called = true;
                return Ok(command);
            });

            called.Should().BeFalse();
            var bad = response.Should().BeOfType<BadRequestObjectResult>().Subject;
            var body = System.Text.Json.JsonSerializer.SerializeToElement(bad.Value);
            body.GetProperty("Code").GetString().Should().Be(ApiResponseCodes.Common.BadRequest);
            body.GetProperty("Data").GetProperty("errors").EnumerateArray().Select(e => e.GetString()).Should().Equal(message);
        }
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Error_del_handler_se_responde_igual_y_el_stream_igual_se_libera(string endpoint)
    {
        Stream? stream = null;

        var response = await Invoke(endpoint, File([1, 2, 3], "x.bin", "application/x-test"), command =>
        {
            stream = UploadOf(command).Content;
            return Result(command, success: false);
        });

        var failure = response.Should().BeOfType<UnprocessableEntityObjectResult>().Subject;
        System.Text.Json.JsonSerializer.SerializeToElement(failure.Value)
            .GetProperty("Data").GetProperty("errors").EnumerateArray().Select(e => e.GetString())
            .Should().Equal("Formato de archivo no permitido.");
        stream!.CanRead.Should().BeFalse();
    }
}
