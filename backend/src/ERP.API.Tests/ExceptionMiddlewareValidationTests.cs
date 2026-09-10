using ERP.API.Middleware;
using ERP.Domain.Exceptions;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace ERP.API.Tests;

public class ExceptionMiddlewareValidationTests
{
    private sealed class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "ERP.API.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }

    [Fact]
    public async Task InvokeAsync_WhenValidationException_Returns422WithErrors()
    {
        // Arrange
        RequestDelegate next = _ =>
        {
            throw new ValidationException(
                new[]
                {
                    new ValidationFailure("SaleCode", "El código de venta es obligatorio."),
                    new ValidationFailure("ShortName", "El nombre corto es obligatorio."),
                }
            );
        };

        var environment = new FakeWebHostEnvironment();
        var middleware = new ExceptionMiddleware(
            next,
            NullLogger<ExceptionMiddleware>.Instance,
            environment
        );
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var responseJson = await reader.ReadToEndAsync();

        using var doc = JsonDocument.Parse(responseJson);
        doc.RootElement.GetProperty("code").GetString().Should().Be("VALIDATION_ERROR");
        doc.RootElement.GetProperty("severity").GetString().Should().Be("error");

        var message = doc.RootElement.GetProperty("message");
        message
            .GetProperty("user")
            .GetString()
            .Should()
            .Be("Datos inválidos. Revisa el formulario.");
        message.GetProperty("dev").ValueKind.Should().Be(JsonValueKind.Null);

        var errors = doc.RootElement.GetProperty("data").GetProperty("errors");
        errors
            .GetProperty("saleCode")[0]
            .GetString()
            .Should()
            .Be("El código de venta es obligatorio.");
        errors
            .GetProperty("shortName")[0]
            .GetString()
            .Should()
            .Be("El nombre corto es obligatorio.");
    }

    // ZH-DATETIME-UTC-GUARDRAILS-01: la violación de invariante detectada por
    // UtcDateTimeGuardInterceptor (DateTime sin normalizar a UTC) nunca debe salir del
    // middleware como DATABASE_UNAVAILABLE — no es una caída/timeout de PostgreSQL, es un bug
    // de normalización detectado ANTES de tocar la base de datos.
    [Fact]
    public async Task InvokeAsync_WhenUnspecifiedDateTimeKindException_Returns500WithDedicatedCode_NotDatabaseUnavailable()
    {
        RequestDelegate next = _ =>
            throw new UnspecifiedDateTimeKindException(
                "CashSession",
                "OpenedAt",
                DateTimeKind.Unspecified
            );

        var environment = new FakeWebHostEnvironment();
        var middleware = new ExceptionMiddleware(
            next,
            NullLogger<ExceptionMiddleware>.Instance,
            environment
        );
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var responseJson = await reader.ReadToEndAsync();

        using var doc = JsonDocument.Parse(responseJson);
        doc.RootElement.GetProperty("code").GetString().Should().Be("INVALID_DATETIME_KIND");
        doc.RootElement.GetProperty("code").GetString().Should().NotBe("DATABASE_UNAVAILABLE");
    }

    // Control: un DbUpdateException genuino (fallo real de escritura en PostgreSQL) sigue
    // clasificándose como DATABASE_UNAVAILABLE — el caso nuevo no debe desplazar este.
    [Fact]
    public async Task InvokeAsync_WhenGenuineDbUpdateException_StillReturnsDatabaseUnavailable()
    {
        RequestDelegate next = _ => throw new DbUpdateException("Simulated write failure");

        var environment = new FakeWebHostEnvironment();
        var middleware = new ExceptionMiddleware(
            next,
            NullLogger<ExceptionMiddleware>.Instance,
            environment
        );
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var responseJson = await reader.ReadToEndAsync();

        using var doc = JsonDocument.Parse(responseJson);
        doc.RootElement.GetProperty("code").GetString().Should().Be("DATABASE_UNAVAILABLE");
    }
}
