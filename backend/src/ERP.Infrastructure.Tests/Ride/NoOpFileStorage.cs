using ERP.Application.Common.Interfaces;

namespace ERP.Infrastructure.Tests.Ride;

/// <summary>Doble de test — nunca tiene un logo real que devolver, mismo patrón que ERP.API.Tests/Support/NoOpFileStorage.cs.</summary>
internal sealed class NoOpFileStorage : IFileStorage
{
    public Task<string> SaveAsync(
        string relativePath,
        Stream content,
        CancellationToken ct = default
    ) => Task.FromResult(relativePath);

    public Task<Stream?> GetAsync(string storedPath, CancellationToken ct = default) =>
        Task.FromResult<Stream?>(null);

    public Task DeleteAsync(string storedPath, CancellationToken ct = default) =>
        Task.CompletedTask;
}
