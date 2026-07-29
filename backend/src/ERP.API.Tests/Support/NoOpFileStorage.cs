using ERP.Application.Common.Interfaces;

namespace ERP.API.Tests.Support;

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
