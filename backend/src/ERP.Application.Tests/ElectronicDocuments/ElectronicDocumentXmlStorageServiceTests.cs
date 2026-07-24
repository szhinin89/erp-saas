using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace ERP.Application.Tests.ElectronicDocuments;

public sealed class ElectronicDocumentXmlStorageServiceTests
{
    private sealed class FakeFileStorage : IFileStorage
    {
        public Dictionary<string, byte[]> Saved { get; } = new();
        public List<string> Deleted { get; } = new();
        public Func<string, bool>? FailWhenPathContains { get; set; }

        public async Task<string> SaveAsync(string relativePath, Stream content, CancellationToken cancellationToken = default)
        {
            if (FailWhenPathContains?.Invoke(relativePath) == true)
                throw new IOException($"Fallo simulado al guardar '{relativePath}'.");

            using var ms = new MemoryStream();
            await content.CopyToAsync(ms, cancellationToken);
            Saved[relativePath] = ms.ToArray();
            return relativePath;
        }

        public Task<Stream?> GetAsync(string storedPath, CancellationToken cancellationToken = default)
            => Task.FromResult<Stream?>(Saved.TryGetValue(storedPath, out var bytes) ? new MemoryStream(bytes) : null);

        public Task DeleteAsync(string storedPath, CancellationToken cancellationToken = default)
        {
            Deleted.Add(storedPath);
            Saved.Remove(storedPath);
            return Task.CompletedTask;
        }
    }

    private static ElectronicDocumentXml DraftXml() => new(
        Xml: "<factura>draft</factura>",
        Encoding: "UTF-8",
        Version: "1.1.0",
        DocumentType: ElectronicDocumentType.Invoice,
        AccessKey: new string('1', 49),
        GeneratedAtUtc: DateTime.UtcNow);

    private static SignedElectronicDocumentXml SignedXml() => new(
        SignedXml: "<factura>signed</factura>",
        Encoding: "UTF-8",
        Version: "1.1.0",
        DocumentType: ElectronicDocumentType.Invoice,
        AccessKey: new string('1', 49),
        SignedAtUtc: DateTime.UtcNow);

    [Fact]
    public async Task StoreAsync_happy_path_saves_both_files_via_file_storage()
    {
        var fileStorage = new FakeFileStorage();
        var service = new ElectronicDocumentXmlStorageService(
            fileStorage, new ElectronicDocumentStorageNamingStrategy(), NullLogger<ElectronicDocumentXmlStorageService>.Instance);

        var result = await service.StoreAsync(
            Guid.NewGuid(), ElectronicDocumentType.Invoice, Guid.NewGuid(), DraftXml(), SignedXml());

        result.IsSuccess.Should().BeTrue(result.Error);
        fileStorage.Saved.Should().HaveCount(2);
        fileStorage.Saved[result.Value!.DraftXmlPath].Should().Equal(System.Text.Encoding.UTF8.GetBytes("<factura>draft</factura>"));
        fileStorage.Saved[result.Value.SignedXmlPath].Should().Equal(System.Text.Encoding.UTF8.GetBytes("<factura>signed</factura>"));
    }

    [Fact]
    public async Task StoreAsync_when_draft_save_fails_returns_failure_without_saving_signed()
    {
        var fileStorage = new FakeFileStorage { FailWhenPathContains = p => p.EndsWith("draft.xml") };
        var service = new ElectronicDocumentXmlStorageService(
            fileStorage, new ElectronicDocumentStorageNamingStrategy(), NullLogger<ElectronicDocumentXmlStorageService>.Instance);

        var result = await service.StoreAsync(
            Guid.NewGuid(), ElectronicDocumentType.Invoice, Guid.NewGuid(), DraftXml(), SignedXml());

        result.IsSuccess.Should().BeFalse();
        fileStorage.Saved.Should().BeEmpty();
    }

    [Fact]
    public async Task StoreAsync_when_signed_save_fails_deletes_already_saved_draft_and_returns_failure()
    {
        var fileStorage = new FakeFileStorage { FailWhenPathContains = p => p.EndsWith("signed.xml") };
        var service = new ElectronicDocumentXmlStorageService(
            fileStorage, new ElectronicDocumentStorageNamingStrategy(), NullLogger<ElectronicDocumentXmlStorageService>.Instance);

        var result = await service.StoreAsync(
            Guid.NewGuid(), ElectronicDocumentType.Invoice, Guid.NewGuid(), DraftXml(), SignedXml());

        result.IsSuccess.Should().BeFalse();
        fileStorage.Saved.Should().BeEmpty("el borrador huérfano debe eliminarse (compensación best-effort)");
        fileStorage.Deleted.Should().HaveCount(1);
        fileStorage.Deleted[0].Should().EndWith("draft.xml");
    }
}
