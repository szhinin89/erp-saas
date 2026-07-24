using ERP.API.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace ERP.API.Tests;

/// <summary>
/// Fase 1 (gobernanza del contrato de respuesta): <see cref="RequestCorrelationMiddleware"/>
/// es la única fuente del <c>correlationId</c> por request.
/// </summary>
public sealed class RequestCorrelationMiddlewareTests
{
    [Fact]
    public async Task Generates_correlationId_from_TraceIdentifier_when_no_header_present()
    {
        RequestDelegate next = _ => Task.CompletedTask;
        var middleware = new RequestCorrelationMiddleware(next);
        var context = new DefaultHttpContext { TraceIdentifier = "trace-xyz" };

        await middleware.InvokeAsync(context);

        RequestCorrelationMiddleware.Resolve(context).Should().Be("trace-xyz");
        context.Response.Headers[RequestCorrelationMiddleware.HeaderName].ToString().Should().Be("trace-xyz");
    }

    [Fact]
    public async Task Reuses_incoming_X_Correlation_Id_header_when_present()
    {
        RequestDelegate next = _ => Task.CompletedTask;
        var middleware = new RequestCorrelationMiddleware(next);
        var context = new DefaultHttpContext { TraceIdentifier = "trace-internal" };
        context.Request.Headers[RequestCorrelationMiddleware.HeaderName] = "gateway-correlation-123";

        await middleware.InvokeAsync(context);

        RequestCorrelationMiddleware.Resolve(context).Should().Be("gateway-correlation-123");
        context.Response.Headers[RequestCorrelationMiddleware.HeaderName].ToString().Should().Be("gateway-correlation-123");
    }

    [Fact]
    public async Task Ignores_blank_incoming_header_and_falls_back_to_TraceIdentifier()
    {
        RequestDelegate next = _ => Task.CompletedTask;
        var middleware = new RequestCorrelationMiddleware(next);
        var context = new DefaultHttpContext { TraceIdentifier = "trace-fallback" };
        context.Request.Headers[RequestCorrelationMiddleware.HeaderName] = "   ";

        await middleware.InvokeAsync(context);

        RequestCorrelationMiddleware.Resolve(context).Should().Be("trace-fallback");
    }

    [Fact]
    public void Resolve_falls_back_to_TraceIdentifier_when_middleware_did_not_run()
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace-no-middleware" };

        RequestCorrelationMiddleware.Resolve(context).Should().Be("trace-no-middleware");
    }
}
