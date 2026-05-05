using System.Text.Json;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using ERP.API.Middleware;

namespace ERP.API.Tests;

public class ExceptionMiddlewareValidationTests
{
    [Fact]
    public async Task InvokeAsync_WhenValidationException_Returns422WithErrors()
    {
        // Arrange
        RequestDelegate next = _ =>
        {
            throw new ValidationException(new[]
            {
                new ValidationFailure("SaleCode", "El código de venta es obligatorio."),
                new ValidationFailure("ShortName", "El nombre corto es obligatorio.")
            });
        };

        var middleware = new ExceptionMiddleware(next, NullLogger<ExceptionMiddleware>.Instance);
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
        doc.RootElement.GetProperty("status").GetInt32().Should().Be(StatusCodes.Status422UnprocessableEntity);
        doc.RootElement.GetProperty("message").GetString().Should().Be("La solicitud contiene errores de validación.");

        var errors = doc.RootElement.GetProperty("errors");
        errors.GetArrayLength().Should().Be(2);
        errors[0].GetProperty("field").GetString().Should().Be("SaleCode");
        errors[0].GetProperty("message").GetString().Should().Be("El código de venta es obligatorio.");
        errors[1].GetProperty("field").GetString().Should().Be("ShortName");
        errors[1].GetProperty("message").GetString().Should().Be("El nombre corto es obligatorio.");
    }
}
