using MediatR;

namespace ERP.API.Tests.Support;

/// <summary>
/// Mediator stub para tests de contrato de controladores.
/// Permite configurar una respuesta fija o lanzar excepciones.
/// </summary>
internal sealed class StubMediator : IMediator
{
    private readonly Func<object, object> _handler;

    public StubMediator(Func<object, object> handler) => _handler = handler;

    public Task<TResponse> Send<TResponse>(
        IRequest<TResponse> request,
        CancellationToken ct = default
    ) => Task.FromResult((TResponse)_handler(request));

    public Task<object?> Send(object request, CancellationToken ct = default) =>
        Task.FromResult((object?)_handler(request));

    public Task Send<TRequest>(TRequest request, CancellationToken ct = default)
        where TRequest : IRequest
    {
        _ = _handler(request!);
        return Task.CompletedTask;
    }

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
        IStreamRequest<TResponse> request,
        CancellationToken ct = default
    ) => AsyncEnumerable.Empty<TResponse>();

    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default) =>
        AsyncEnumerable.Empty<object?>();

    public Task Publish(object notification, CancellationToken ct = default) => Task.CompletedTask;

    public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default)
        where TNotification : INotification => Task.CompletedTask;
}
