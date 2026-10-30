using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Interfaces.SRI;
using ERP.Application.Modules.ElectronicInvoicing.Services;
using ERP.Application.Modules.ElectronicInvoicing.UseCases.ValidateSriConfiguration;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using Moq;

namespace ERP.Application.Tests.ElectronicInvoicing;

public sealed class ValidateSriConfigurationQueryHandlerTests
{
    [Fact]
    public async Task Installed_certificate_without_password_returns_invalid_with_explicit_password_failure()
    {
        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var settings = SriSettings.Create(
            tenantId, companyId, 1, 1, "https://sri.example.invalid/RecepcionComprobantesOffline?wsdl", userId);
        settings.AttachCertificate("synthetic-certificate", "synthetic.p12", 0, DateTime.UtcNow, userId);
        Assert.True(string.IsNullOrWhiteSpace(settings.CertPassword));

        var repository = new Mock<ISriSettingsRepository>(MockBehavior.Strict);
        repository.Setup(r => r.GetByCompanyIdAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);
        var companyRepository = new Mock<ICompanyRepository>(MockBehavior.Strict);
        var tenant = new Mock<ICurrentTenant>(MockBehavior.Strict);
        tenant.SetupGet(t => t.TenantId).Returns(tenantId);
        var company = new Mock<ICurrentCompany>(MockBehavior.Strict);
        company.SetupGet(c => c.CompanyId).Returns(companyId);

        // Real resolver, synthetic storage only: no certificate parsing or secret access is allowed.
        using var syntheticFile = new MemoryStream();
        var storage = new Mock<IFileStorage>(MockBehavior.Strict);
        storage.Setup(s => s.GetAsync(settings.CertP12Path!, It.IsAny<CancellationToken>()))
            .ReturnsAsync(syntheticFile);
        var secrets = new Mock<ISecretProtector>(MockBehavior.Strict);
        var inspector = new Mock<ISriCertificateInspector>(MockBehavior.Strict);
        var resolver = new SriCertificateStatusResolver(storage.Object, secrets.Object, inspector.Object);
        var certificateStatus = await resolver.ResolveAsync(settings, CancellationToken.None);
        Assert.True(certificateStatus.Installed);
        Assert.False(certificateStatus.PasswordCorrect);
        Assert.False(certificateStatus.Valid);
        Assert.Equal("No hay contraseña configurada para el certificado.", certificateStatus.ErrorMessage);

        // The resolver disposes the stream; provide another synthetic stream for the handler invocation.
        using var handlerFile = new MemoryStream();
        storage.Setup(s => s.GetAsync(settings.CertP12Path!, It.IsAny<CancellationToken>()))
            .ReturnsAsync(handlerFile);
        var connectivity = new Mock<ISriConnectivityChecker>(MockBehavior.Strict);
        connectivity.Setup(c => c.PingAsync(settings.WsdlUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var handler = new ValidateSriConfigurationQueryHandler(
            repository.Object, companyRepository.Object, tenant.Object, company.Object,
            resolver, connectivity.Object);

        var result = await handler.Handle(new ValidateSriConfigurationQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var diagnostic = Assert.IsType<ERP.Application.Modules.ElectronicInvoicing.DTOs.SriConfigurationValidationDto>(result.Value);
        var passwordCheck = Assert.Single(diagnostic.Checks, c => c.Code == "certificatePassword");
        Assert.False(passwordCheck.Passed);
        Assert.Equal("No hay contraseña configurada para el certificado.", passwordCheck.Message);
        Assert.All(diagnostic.Checks.Where(c => c.Code != "certificatePassword"), c => Assert.True(c.Passed));
        Assert.DoesNotContain(diagnostic.Checks, c => c.Code is "certificateExpiry" or "certificateOwner");
        Assert.Null(diagnostic.Certificate);
        Assert.False(diagnostic.IsValid);

        repository.Verify(r => r.GetByCompanyIdAsync(companyId, It.IsAny<CancellationToken>()), Times.Once);
        storage.Verify(s => s.GetAsync(settings.CertP12Path!, It.IsAny<CancellationToken>()), Times.Exactly(2));
        connectivity.Verify(c => c.PingAsync(settings.WsdlUrl, It.IsAny<CancellationToken>()), Times.Once);
        companyRepository.VerifyNoOtherCalls();
        secrets.VerifyNoOtherCalls();
        inspector.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(true, true, false, true)]
    [InlineData(true, true, true, false)]
    [InlineData(true, false, false, false)]
    [InlineData(false, false, false, false)]
    public async Task Existing_certificate_diagnostics_are_preserved(
        bool installed, bool passwordCorrect, bool expired, bool expectedValid)
    {
        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var settings = SriSettings.Create(
            tenantId, companyId, 1, 1, "https://sri.example.invalid/RecepcionComprobantesOffline?wsdl", userId);
        settings.UpdateConfiguration(1, 1, settings.WsdlUrl, "synthetic-password", userId);
        settings.AttachCertificate("synthetic-certificate", "synthetic.p12", 0, DateTime.UtcNow, userId);
        var repository = new Mock<ISriSettingsRepository>(MockBehavior.Strict);
        repository.Setup(r => r.GetByCompanyIdAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);
        var tenant = new Mock<ICurrentTenant>(MockBehavior.Strict);
        tenant.SetupGet(t => t.TenantId).Returns(tenantId);
        var company = new Mock<ICurrentCompany>(MockBehavior.Strict);
        company.SetupGet(c => c.CompanyId).Returns(companyId);
        var companyRepository = new Mock<ICompanyRepository>(MockBehavior.Strict);
        var notAfter = passwordCorrect ? DateTime.UtcNow.AddDays(expired ? -10 : 10) : (DateTime?)null;
        const string subject = "CN=Synthetic 1234567890";
        if (installed && passwordCorrect)
        {
            companyRepository.Setup(r => r.GetByIdForTenantAsync(companyId, tenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ERP.Domain.Modules.Company.Entities.Company
                {
                    Id = companyId, TenantId = tenantId, TaxIdentificationNumber = "1234567890001"
                });
        }
        var status = new SriCertificateStatus(
            installed, passwordCorrect, expectedValid, notAfter,
            passwordCorrect ? subject : null, null, passwordCorrect ? null : "Synthetic certificate error");
        var resolver = new Mock<ISriCertificateStatusResolver>(MockBehavior.Strict);
        resolver.Setup(r => r.ResolveAsync(settings, It.IsAny<CancellationToken>())).ReturnsAsync(status);
        var connectivity = new Mock<ISriConnectivityChecker>(MockBehavior.Strict);
        connectivity.Setup(c => c.PingAsync(settings.WsdlUrl, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var handler = new ValidateSriConfigurationQueryHandler(
            repository.Object, companyRepository.Object, tenant.Object, company.Object,
            resolver.Object, connectivity.Object);

        var result = await handler.Handle(new ValidateSriConfigurationQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var diagnostic = Assert.IsType<ERP.Application.Modules.ElectronicInvoicing.DTOs.SriConfigurationValidationDto>(result.Value);
        Assert.Equal(expectedValid, diagnostic.IsValid);
        Assert.Equal(installed, Assert.Single(diagnostic.Checks, c => c.Code == "certificateFile").Passed);
        if (installed)
        {
            Assert.Equal(passwordCorrect, Assert.Single(diagnostic.Checks, c => c.Code == "certificatePassword").Passed);
            Assert.NotNull(diagnostic.Certificate);
            Assert.Equal(passwordCorrect, diagnostic.Certificate.PasswordCorrect);
            Assert.Equal(notAfter, diagnostic.Certificate.NotAfterUtc);
        }
        else
        {
            Assert.DoesNotContain(diagnostic.Checks, c => c.Code == "certificatePassword");
            Assert.Null(diagnostic.Certificate);
        }
        if (installed && passwordCorrect)
        {
            Assert.Equal(!expired, Assert.Single(diagnostic.Checks, c => c.Code == "certificateExpiry").Passed);
            Assert.True(Assert.Single(diagnostic.Checks, c => c.Code == "certificateOwner").Passed);
            companyRepository.Verify(r => r.GetByIdForTenantAsync(companyId, tenantId, It.IsAny<CancellationToken>()), Times.Once);
        }
        else
        {
            Assert.DoesNotContain(diagnostic.Checks, c => c.Code is "certificateExpiry" or "certificateOwner");
            companyRepository.VerifyNoOtherCalls();
        }
        resolver.Verify(r => r.ResolveAsync(settings, It.IsAny<CancellationToken>()), Times.Once);
        connectivity.Verify(c => c.PingAsync(settings.WsdlUrl, It.IsAny<CancellationToken>()), Times.Once);
    }
}
