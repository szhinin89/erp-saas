using MediatR;

namespace ERP.Infrastructure.Persistence;

/// <summary>Publicador sin efecto para diseño en tiempo de migraciones o tests sin MediatR.</summary>
public sealed class NoOpPublisher : IPublisher
{
    public Task Publish(object notification, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task Publish<TNotification>(
        TNotification notification,
        CancellationToken cancellationToken = default
    )
        where TNotification : INotification => Task.CompletedTask;
}
