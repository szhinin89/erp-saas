using ERP.Domain.Modules.Ride.Enums;
using ERP.Infrastructure.Ride.Storage;
using ERP.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace ERP.Infrastructure.Tests.Ride;

/// <summary>
/// <see cref="RidePdfStorageService"/> contra <see cref="LocalFileStorage"/> real (sin mocks) —
/// mismo criterio que el resto de la suite de persistencia de Ride.
/// </summary>
public sealed class RidePdfStorageServiceTests : IDisposable
{
    private readonly string _basePath = Path.Combine(
        Path.GetTempPath(),
        "ride-storage-tests-" + Guid.NewGuid().ToString("N")
    );

    private RidePdfStorageService BuildService()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { ["FileStorage:BasePath"] = _basePath }
            )
            .Build();
        var fileStorage = new LocalFileStorage(configuration);
        return new RidePdfStorageService(fileStorage, new RidePdfStorageNamingStrategy());
    }

    public void Dispose()
    {
        if (Directory.Exists(_basePath))
            Directory.Delete(_basePath, recursive: true);
    }

    [Fact]
    public async Task StoreAsync_saves_the_pdf_and_returns_a_readable_path()
    {
        var service = BuildService();
        var tenantId = Guid.NewGuid();
        var electronicDocumentId = Guid.NewGuid();
        byte[] pdf = [1, 2, 3, 4];

        var result = await service.StoreAsync(
            tenantId,
            RideDocumentType.Invoice,
            electronicDocumentId,
            "1.0.0",
            pdf
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Stored_pdf_can_be_read_back_through_IFileStorage_with_identical_bytes()
    {
        var service = BuildService();
        var tenantId = Guid.NewGuid();
        var electronicDocumentId = Guid.NewGuid();
        byte[] pdf = [5, 6, 7, 8, 9];

        var storeResult = await service.StoreAsync(
            tenantId,
            RideDocumentType.Invoice,
            electronicDocumentId,
            "1.0.0",
            pdf
        );

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { ["FileStorage:BasePath"] = _basePath }
            )
            .Build();
        var fileStorage = new LocalFileStorage(configuration);
        await using var stream = await fileStorage.GetAsync(storeResult.Value!);
        stream.Should().NotBeNull();
        using var memory = new MemoryStream();
        await stream!.CopyToAsync(memory);

        memory.ToArray().Should().Equal(pdf);
    }

    [Fact]
    public async Task Regenerating_the_same_fingerprint_overwrites_the_existing_file()
    {
        var service = BuildService();
        var tenantId = Guid.NewGuid();
        var electronicDocumentId = Guid.NewGuid();

        var first = await service.StoreAsync(
            tenantId,
            RideDocumentType.Invoice,
            electronicDocumentId,
            "1.0.0",
            [1, 1, 1]
        );
        var second = await service.StoreAsync(
            tenantId,
            RideDocumentType.Invoice,
            electronicDocumentId,
            "1.0.0",
            [2, 2, 2, 2]
        );

        second.Value.Should().Be(first.Value);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { ["FileStorage:BasePath"] = _basePath }
            )
            .Build();
        var fileStorage = new LocalFileStorage(configuration);
        await using var stream = await fileStorage.GetAsync(second.Value!);
        using var memory = new MemoryStream();
        await stream!.CopyToAsync(memory);

        memory.ToArray().Should().Equal([2, 2, 2, 2]);
    }

    [Fact]
    public async Task Path_is_fully_deterministic_for_the_same_inputs()
    {
        var service = BuildService();
        var tenantId = Guid.NewGuid();
        var electronicDocumentId = Guid.NewGuid();

        var first = await service.StoreAsync(
            tenantId,
            RideDocumentType.Invoice,
            electronicDocumentId,
            "1.0.0",
            [1]
        );
        var second = await service.StoreAsync(
            tenantId,
            RideDocumentType.Invoice,
            electronicDocumentId,
            "1.0.0",
            [1]
        );

        second.Value.Should().Be(first.Value);
    }

    [Fact]
    public async Task Path_differs_when_the_template_version_differs()
    {
        var service = BuildService();
        var tenantId = Guid.NewGuid();
        var electronicDocumentId = Guid.NewGuid();

        var v1 = await service.StoreAsync(
            tenantId,
            RideDocumentType.Invoice,
            electronicDocumentId,
            "1.0.0",
            [1]
        );
        var v2 = await service.StoreAsync(
            tenantId,
            RideDocumentType.Invoice,
            electronicDocumentId,
            "2.0.0",
            [1]
        );

        v2.Value.Should().NotBe(v1.Value);
    }
}
