using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Interfaces.SRI;
using ERP.Application.Common.Models;
using ERP.Application.Modules.ElectronicInvoicing.UseCases.UploadSriCertificate;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Enums;
using ERP.Domain.Configuration.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.ElectronicInvoicing;

/// <summary>
/// CONFIG-FOUNDATION-P2-01 — UploadSriCertificateCommandHandler audita el cambio de certificado
/// con un fingerprint SHA-256 del contenido, nunca el binario ni la contraseña.
/// </summary>
public sealed class UploadSriCertificateCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private sealed class Fixture
    {
        public Mock<ISriSettingsRepository> Repo { get; } = new();
        public Mock<IConfigurationChangeLogger> ChangeLogger { get; } = new();
        public Mock<ICurrentCompany> Company { get; } = new();
        public Mock<ICurrentUser> User { get; } = new();
        public Mock<ISecretProtector> SecretProtector { get; } = new();
        public Mock<ISriCertificateInspector> CertInspector { get; } = new();
        public Mock<IFileStorage> FileStorage { get; } = new();

        public Fixture()
        {
            Company.Setup(c => c.CompanyId).Returns(CompanyId);
            User.Setup(u => u.UserId).Returns(UserId);
            FileStorage
                .Setup(f =>
                    f.SaveAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>())
                )
                .ReturnsAsync("certificates/company/certificate.p12");
        }

        public UploadSriCertificateCommandHandler BuildHandler() =>
            new(
                Repo.Object,
                ChangeLogger.Object,
                Company.Object,
                User.Object,
                SecretProtector.Object,
                CertInspector.Object,
                FileStorage.Object
            );
    }

    private static MediaUploadContent MakeCertificateFile(string fileName = "certificado.p12")
    {
        // Estructura mínima ASN.1 DER SEQUENCE (0x30) que el handler acepta como PKCS#12 válido
        // a nivel estructural — no es un certificado real, no se descifra en este test.
        var bytes = new byte[] { 0x30, 0x82, 0x01, 0x00 }.Concat(new byte[100]).ToArray();
        return new MediaUploadContent(new MemoryStream(bytes), fileName, "application/x-pkcs12", bytes.Length);
    }

    [Fact]
    public async Task Subir_certificado_genera_log_con_fingerprint_sin_el_binario_ni_password()
    {
        var f = new Fixture();
        var settings = SriSettings.Create(TenantId, CompanyId, 1, 1, "https://wsdl.example/test", UserId);
        f.Repo
            .Setup(r => r.GetByCompanyIdAsync(CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        var result = await f.BuildHandler()
            .Handle(new UploadSriCertificateCommand(MakeCertificateFile()), default);

        result.IsSuccess.Should().BeTrue();
        f.ChangeLogger.Verify(
            l =>
                l.LogAsync(
                    It.Is<ConfigurationChangeLogEntry>(e =>
                        e.EntityType == "SriSettings"
                        && e.FieldName == "Certificate"
                        && e.ValueType == ConfigurationChangeValueType.Fingerprint
                        && e.IsSensitive
                        && e.NewValue != null
                        && e.NewValue.Contains("sha256:")
                        && e.NewValue.Contains("certificado.p12")
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task Sin_configuracion_previa_no_permite_subir_certificado_ni_genera_log()
    {
        var f = new Fixture();
        f.Repo
            .Setup(r => r.GetByCompanyIdAsync(CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SriSettings?)null);

        var result = await f.BuildHandler()
            .Handle(new UploadSriCertificateCommand(MakeCertificateFile()), default);

        result.IsSuccess.Should().BeFalse();
        f.ChangeLogger.Verify(
            l => l.LogAsync(It.IsAny<ConfigurationChangeLogEntry>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }
}
