using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Interfaces.SRI;
using ERP.Application.Modules.ElectronicInvoicing.UseCases.GetSriConfiguration;
using ERP.Application.Modules.ElectronicInvoicing.UseCases.InspectSriCertificate;
using ERP.Application.Modules.ElectronicInvoicing.UseCases.UpsertSriConfiguration;
using ERP.Application.Modules.ElectronicInvoicing.UseCases.ValidateSriConfiguration;
using ERP.Application.Modules.ElectronicInvoicing.Services;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.ElectronicInvoicing;

/// <summary>
/// MEDIO cluster (auditoría multi-tenant) — GetSriConfigurationQueryHandler,
/// UpsertSriConfigurationCommandHandler, InspectSriCertificateQueryHandler y
/// ValidateSriConfigurationQueryHandler no reciben ningún CompanyId en el Command/Query: todos
/// resuelven exclusivamente vía ICurrentCompany.CompanyId (contexto autenticado). Estos tests
/// prueban que, con dos empresas seedeadas en el repo (la activa y "otra"), el handler nunca
/// lee/expone la configuración SRI de la otra empresa — solo la de la empresa activa,
/// exactamente el criterio "fail-closed" de docs/architecture/security.md.
/// </summary>
public sealed class ElectronicInvoicingCompanyScopeTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid ActiveCompanyId = Guid.NewGuid();
    private static readonly Guid OtherCompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private sealed class FixedCurrentCompany : ICurrentCompany
    {
        public FixedCurrentCompany(Guid companyId) => CompanyId = companyId;

        public Guid CompanyId { get; }
        public bool IsAuthenticated => true;
        public bool HasCompanyContext => true;
    }

    private static SriSettings NewSettings(Guid companyId) =>
        SriSettings.Create(
            TenantId,
            companyId,
            environment: 1,
            emissionType: 1,
            wsdlUrl: "https://celcer.sri.gob.ec/wsdl",
            createdBy: UserId
        );

    [Fact]
    public async Task GetSriConfiguration_solo_lee_la_configuracion_de_la_empresa_activa_nunca_la_de_otra()
    {
        var repo = new Mock<ISriSettingsRepository>();
        repo.Setup(r => r.GetByCompanyIdAsync(ActiveCompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SriSettings?)null);
        repo.Setup(r => r.GetByCompanyIdAsync(OtherCompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewSettings(OtherCompanyId));

        var handler = new GetSriConfigurationQueryHandler(
            repo.Object,
            new FixedCurrentCompany(ActiveCompanyId)
        );

        var result = await handler.Handle(new GetSriConfigurationQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull(); // Nunca cae en el registro de OtherCompanyId.
        repo.Verify(
            r => r.GetByCompanyIdAsync(OtherCompanyId, It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task UpsertSriConfiguration_crea_la_configuracion_con_el_CompanyId_activo_no_uno_arbitrario()
    {
        var repo = new Mock<ISriSettingsRepository>();
        var changeLogger = new Mock<IConfigurationChangeLogger>();
        var tenant = new Mock<ICurrentTenant>();
        var user = new Mock<ICurrentUser>();
        var secretProtector = new Mock<ISecretProtector>();

        tenant.Setup(t => t.TenantId).Returns(TenantId);
        user.Setup(u => u.UserId).Returns(UserId);
        repo.Setup(r => r.GetByCompanyIdAsync(ActiveCompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SriSettings?)null);

        SriSettings? added = null;
        repo.Setup(r => r.AddAsync(It.IsAny<SriSettings>(), It.IsAny<CancellationToken>()))
            .Callback<SriSettings, CancellationToken>((s, _) => added = s)
            .Returns(Task.CompletedTask);

        var handler = new UpsertSriConfigurationCommandHandler(
            repo.Object,
            changeLogger.Object,
            tenant.Object,
            new FixedCurrentCompany(ActiveCompanyId),
            user.Object,
            secretProtector.Object
        );

        var result = await handler.Handle(
            new UpsertSriConfigurationCommand(null, 1, 1, "https://celcer.sri.gob.ec/wsdl"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        added.Should().NotBeNull();
        added!.CompanyId.Should().Be(ActiveCompanyId);
        added.CompanyId.Should().NotBe(OtherCompanyId);
    }

    [Fact]
    public async Task InspectSriCertificate_sin_certificado_para_la_empresa_activa_falla_aunque_otra_empresa_tenga_uno()
    {
        var repo = new Mock<ISriSettingsRepository>();
        var certInspector = new Mock<ISriCertificateInspector>();
        var fileStorage = new Mock<IFileStorage>();

        repo.Setup(r => r.GetByCompanyIdAsync(ActiveCompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SriSettings?)null);
        var otherSettings = NewSettings(OtherCompanyId);
        // Simula: la empresa activa nunca subió certificado, pero otra empresa sí. El repo real
        // solo se consulta con ActiveCompanyId — nunca se filtra el certificado de otra empresa.
        repo.Setup(r => r.GetByCompanyIdAsync(OtherCompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(otherSettings);

        var handler = new InspectSriCertificateQueryHandler(
            repo.Object,
            new FixedCurrentCompany(ActiveCompanyId),
            certInspector.Object,
            fileStorage.Object
        );

        var result = await handler.Handle(
            new InspectSriCertificateQuery("password"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        fileStorage.Verify(
            f => f.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task ValidateSriConfiguration_sin_configuracion_de_la_empresa_activa_no_valida_la_de_otra_empresa()
    {
        var sriRepo = new Mock<ISriSettingsRepository>();
        var companyRepo = new Mock<ICompanyRepository>();
        var tenant = new Mock<ICurrentTenant>();
        var certStatusResolver = new Mock<ISriCertificateStatusResolver>();
        var connectivityChecker = new Mock<ISriConnectivityChecker>();

        tenant.Setup(t => t.TenantId).Returns(TenantId);
        sriRepo
            .Setup(r => r.GetByCompanyIdAsync(ActiveCompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SriSettings?)null);
        sriRepo
            .Setup(r => r.GetByCompanyIdAsync(OtherCompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewSettings(OtherCompanyId));

        var handler = new ValidateSriConfigurationQueryHandler(
            sriRepo.Object,
            companyRepo.Object,
            tenant.Object,
            new FixedCurrentCompany(ActiveCompanyId),
            certStatusResolver.Object,
            connectivityChecker.Object
        );

        var result = await handler.Handle(
            new ValidateSriConfigurationQuery(),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsValid.Should().BeFalse();
        result.Value.Checks.Should().ContainSingle(c => c.Code == "configuration" && !c.Passed);
        certStatusResolver.Verify(
            r => r.ResolveAsync(It.IsAny<SriSettings>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }
}
