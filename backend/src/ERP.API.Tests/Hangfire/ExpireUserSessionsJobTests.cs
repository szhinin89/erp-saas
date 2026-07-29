using ERP.API.Hangfire;
using ERP.API.Tests.Support;
using ERP.Application.Access.UseCases.ExpireUserSessions;
using ERP.Application.Common;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace ERP.API.Tests.Hangfire;

/// <summary>
/// Fase 9: ExpireUserSessionsJob con StubMediator — prueba únicamente que el job invoca
/// ExpireUserSessionsCommand vía MediatR (sin DbContext directo) y maneja fallos/excepciones
/// sin propagarlas (mismo patrón que ElectronicDocumentRetryJob: un job de limpieza nunca debe
/// tumbar el proceso de Hangfire por una corrida fallida).
/// </summary>
public sealed class ExpireUserSessionsJobTests
{
    private static ExpireUserSessionsJob BuildJob(Func<object, object> handler)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMediator>(new StubMediator(handler));
        var provider = services.BuildServiceProvider();

        return new ExpireUserSessionsJob(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ExpireUserSessionsJob>.Instance
        );
    }

    [Fact]
    public async Task ExecuteAsync_envia_ExpireUserSessionsCommand_al_mediator()
    {
        object? sentRequest = null;
        var job = BuildJob(req =>
        {
            sentRequest = req;
            return Result<int>.Success(3);
        });

        await job.ExecuteAsync(CancellationToken.None);

        sentRequest.Should().BeOfType<ExpireUserSessionsCommand>();
    }

    [Fact]
    public async Task ExecuteAsync_si_el_command_falla_no_propaga_excepcion()
    {
        var job = BuildJob(_ => Result<int>.Failure("Fallo simulado."));

        var act = () => job.ExecuteAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ExecuteAsync_si_el_mediator_lanza_excepcion_no_la_propaga()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMediator>(new ThrowingMediator());
        var provider = services.BuildServiceProvider();
        var job = new ExpireUserSessionsJob(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ExpireUserSessionsJob>.Instance
        );

        var act = () => job.ExecuteAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    private sealed class ThrowingMediator : IMediator
    {
        public Task<TResponse> Send<TResponse>(
            IRequest<TResponse> request,
            CancellationToken ct = default
        ) => throw new InvalidOperationException("Fallo simulado de infraestructura.");

        public Task<object?> Send(object request, CancellationToken ct = default) =>
            throw new InvalidOperationException("Fallo simulado de infraestructura.");

        public Task Send<TRequest>(TRequest request, CancellationToken ct = default)
            where TRequest : IRequest =>
            throw new InvalidOperationException("Fallo simulado de infraestructura.");

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken ct = default
        ) => AsyncEnumerable.Empty<TResponse>();

        public IAsyncEnumerable<object?> CreateStream(
            object request,
            CancellationToken ct = default
        ) => AsyncEnumerable.Empty<object?>();

        public Task Publish(object notification, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken ct = default
        )
            where TNotification : INotification => Task.CompletedTask;
    }
}
