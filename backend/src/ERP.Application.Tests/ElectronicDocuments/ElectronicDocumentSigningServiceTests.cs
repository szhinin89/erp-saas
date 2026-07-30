using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Interfaces.SRI;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Cryptography;

namespace ERP.Application.Tests.ElectronicDocuments;

public sealed class ElectronicDocumentSigningServiceTests
{
    private sealed class FakeSriSettingsRepository : ISriSettingsRepository
    {
        private readonly SriSettings? _settings;

        public FakeSriSettingsRepository(SriSettings? settings) => _settings = settings;

        public Task<SriSettings?> GetByCompanyIdAsync(
            Guid companyId,
            CancellationToken ct = default
        ) => Task.FromResult(_settings);

        public Task<SriSettings?> GetByCompanyIdForUpdateAsync(
            Guid companyId,
            CancellationToken ct = default
        ) => Task.FromResult(_settings);

        public Task AddAsync(SriSettings config, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task UpdateAsync(SriSettings config, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeFileStorage : IFileStorage
    {
        private readonly byte[]? _content;

        public FakeFileStorage(byte[]? content) => _content = content;

        public Task<string> SaveAsync(
            string relativePath,
            Stream content,
            CancellationToken ct = default
        ) => Task.FromResult(relativePath);

        public Task<Stream?> GetAsync(string storedPath, CancellationToken ct = default) =>
            Task.FromResult<Stream?>(_content is null ? null : new MemoryStream(_content));

        public Task DeleteAsync(string storedPath, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    private sealed class PassthroughSecretProtector : ISecretProtector
    {
        public string Protect(string plaintext) => plaintext;

        public string UnprotectOrPlaintext(string storedValue) => storedValue;

        public bool IsProtected(string storedValue) => false;
    }

    private sealed class ThrowingSecretProtector : ISecretProtector
    {
        public string Protect(string plaintext) => plaintext;

        public string UnprotectOrPlaintext(string storedValue) =>
            throw new CryptographicException("clave de protección inválida");

        public bool IsProtected(string storedValue) => true;
    }

    private sealed class FakeSigner : IElectronicDocumentSigner
    {
        private readonly Func<string, byte[], string, byte[]> _behavior;

        public FakeSigner(Func<string, byte[], string, byte[]> behavior) => _behavior = behavior;

        public byte[] Sign(string xmlUtf8, string p12FilePath, string p12Password) =>
            _behavior(xmlUtf8, [], p12Password);

        public byte[] Sign(string xmlUtf8, byte[] p12Bytes, string p12Password) =>
            _behavior(xmlUtf8, p12Bytes, p12Password);
    }

    private static ElectronicDocumentXml SampleXml() =>
        new(
            Xml: "<factura/>",
            Encoding: "UTF-8",
            Version: "1.1.0",
            DocumentType: ElectronicDocumentType.Invoice,
            AccessKey: new string('1', 49),
            GeneratedAtUtc: DateTime.UtcNow
        );

    private static SriSettings ValidSriSettings()
    {
        var s = SriSettings.Create(
            tenantId: Guid.NewGuid(),
            companyId: Guid.NewGuid(),
            environment: 1,
            emissionType: 1,
            wsdlUrl: "https://celcer.sri.gob.ec/wsdl",
            createdBy: Guid.NewGuid()
        );
        s.AttachCertificate(
            "certificates/x/certificate.p12",
            "certificate.p12",
            128,
            DateTime.UtcNow,
            Guid.NewGuid()
        );
        s.UpdateConfiguration(
            1,
            1,
            "https://celcer.sri.gob.ec/wsdl",
            "plaintext-password",
            Guid.NewGuid()
        );
        return s;
    }

    [Fact]
    public async Task SignAsync_without_sri_settings_fails_with_clear_message_not_exception()
    {
        var service = new ElectronicDocumentSigningService(
            new FakeSriSettingsRepository(null),
            new PassthroughSecretProtector(),
            new FakeSigner(
                (_, _, _) => throw new InvalidOperationException("no debería invocarse")
            ),
            new FakeFileStorage(null),
            NullLogger<ElectronicDocumentSigningService>.Instance
        );

        var result = await service.SignAsync(Guid.NewGuid(), Guid.NewGuid(), SampleXml());

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("configuración SRI");
    }

    [Fact]
    public async Task SignAsync_with_missing_cert_file_fails_with_not_found()
    {
        var settings = ValidSriSettings();

        var service = new ElectronicDocumentSigningService(
            new FakeSriSettingsRepository(settings),
            new PassthroughSecretProtector(),
            new FakeSigner(
                (_, _, _) => throw new InvalidOperationException("no debería invocarse")
            ),
            new FakeFileStorage(null),
            NullLogger<ElectronicDocumentSigningService>.Instance
        );

        var result = await service.SignAsync(Guid.NewGuid(), Guid.NewGuid(), SampleXml());

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("certificado");
    }

    [Fact]
    public async Task SignAsync_when_password_cannot_be_decrypted_fails_with_clear_message()
    {
        var settings = ValidSriSettings();
        var service = new ElectronicDocumentSigningService(
            new FakeSriSettingsRepository(settings),
            new ThrowingSecretProtector(),
            new FakeSigner(
                (_, _, _) => throw new InvalidOperationException("no debería invocarse")
            ),
            new FakeFileStorage([1, 2, 3]),
            NullLogger<ElectronicDocumentSigningService>.Instance
        );

        var result = await service.SignAsync(Guid.NewGuid(), Guid.NewGuid(), SampleXml());

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("descifrar");
    }

    [Fact]
    public async Task SignAsync_when_signer_throws_cryptographic_exception_fails_with_clear_message_not_exception()
    {
        var settings = ValidSriSettings();
        var service = new ElectronicDocumentSigningService(
            new FakeSriSettingsRepository(settings),
            new PassthroughSecretProtector(),
            new FakeSigner((_, _, _) => throw new CryptographicException("contraseña incorrecta")),
            new FakeFileStorage([1, 2, 3]),
            NullLogger<ElectronicDocumentSigningService>.Instance
        );

        var result = await service.SignAsync(Guid.NewGuid(), Guid.NewGuid(), SampleXml());

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("criptográfico");
    }

    [Fact]
    public async Task SignAsync_when_certificate_has_no_private_key_fails_with_certificate_error()
    {
        var settings = ValidSriSettings();
        var service = new ElectronicDocumentSigningService(
            new FakeSriSettingsRepository(settings),
            new PassthroughSecretProtector(),
            new FakeSigner(
                (_, _, _) =>
                    throw new InvalidOperationException(
                        "El certificado no contiene clave privada RSA."
                    )
            ),
            new FakeFileStorage([1, 2, 3]),
            NullLogger<ElectronicDocumentSigningService>.Instance
        );

        var result = await service.SignAsync(Guid.NewGuid(), Guid.NewGuid(), SampleXml());

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("certificado");
    }

    [Fact]
    public async Task SignAsync_happy_path_delegates_to_signer_and_returns_signed_xml()
    {
        var settings = ValidSriSettings();
        var certBytes = new byte[] { 9, 9, 9 };
        string? capturedXml = null,
            capturedPassword = null;
        byte[]? capturedBytes = null;

        var service = new ElectronicDocumentSigningService(
            new FakeSriSettingsRepository(settings),
            new PassthroughSecretProtector(),
            new FakeSigner(
                (xmlUtf8, p12Bytes, p12Password) =>
                {
                    capturedXml = xmlUtf8;
                    capturedBytes = p12Bytes;
                    capturedPassword = p12Password;
                    return System.Text.Encoding.UTF8.GetBytes("<factura><ds:Signature/></factura>");
                }
            ),
            new FakeFileStorage(certBytes),
            NullLogger<ElectronicDocumentSigningService>.Instance
        );

        var xml = SampleXml();
        var result = await service.SignAsync(Guid.NewGuid(), Guid.NewGuid(), xml);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.SignedXml.Should().Contain("ds:Signature");
        result.Value.AccessKey.Should().Be(xml.AccessKey);
        result.Value.DocumentType.Should().Be(xml.DocumentType);

        capturedXml.Should().Be(xml.Xml);
        capturedBytes.Should().BeEquivalentTo(certBytes);
        capturedPassword.Should().Be("plaintext-password");
    }
}
