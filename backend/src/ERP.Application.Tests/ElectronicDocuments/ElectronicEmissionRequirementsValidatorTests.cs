using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.ElectronicDocuments.Readiness;
using ERP.Application.Modules.ElectronicInvoicing.Services;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Company.Interfaces;
using FluentAssertions;
using Moq;
using RequirementCodes = ERP.Application.Modules.ElectronicDocuments.Readiness.ElectronicEmissionRequirementCodes;

namespace ERP.Application.Tests.ElectronicDocuments;

/// <summary>
/// READINESS-PREVENTIVE-1B — reglas del gate fiscal preventivo (los efectos sobre secuenciales,
/// Kardex, Caja y CxC se prueban con PostgreSQL real en Infrastructure.Tests / API.Tests).
/// </summary>
public sealed class ElectronicEmissionRequirementsValidatorTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task Configuracion_completa_permite_emitir_sin_incumplimientos()
    {
        var f = new Fixture();

        var result = await f.ValidateAsync();

        result.CanIssue.Should().BeTrue();
        result.Issues.Should().BeEmpty();
        result.ToFailure<object>().Should().BeNull();
    }

    [Fact]
    public async Task Direccion_vacia_del_establecimiento_efectivo_no_matriz_se_reporta_aparte()
    {
        var f = new Fixture();
        f.UseBranchEstablishment(address: "");

        var result = await f.ValidateAsync();

        result.CanIssue.Should().BeFalse();
        result.Issues.Should().ContainSingle().Which.Should().Be(
            new ElectronicEmissionRequirementIssue(RequirementCodes.EstablishmentAddress,
                ElectronicEmissionRequirementSeverity.Required, ReadinessActionTarget.Establishments));
    }

    [Fact]
    public async Task Sin_SriSettings_bloquea_y_dirige_a_la_configuracion_de_facturacion_electronica()
    {
        var f = new Fixture();
        f.Settings = null;

        var result = await f.ValidateAsync();

        result.Issues.Should().ContainSingle().Which.Should().Be(
            new ElectronicEmissionRequirementIssue(RequirementCodes.SriSettings,
                ElectronicEmissionRequirementSeverity.Required,
                ReadinessActionTarget.ElectronicInvoicingSettings));
        f.Certificates.Verify(c => c.ResolveAsync(It.IsAny<SriSettings>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Certificado_vencido_bloquea_la_emision()
    {
        var f = new Fixture();
        f.Certificate = f.Certificate with { Valid = false, NotAfterUtc = DateTime.UtcNow.AddDays(-1) };

        var result = await f.ValidateAsync();

        result.CanIssue.Should().BeFalse();
        result.Issues.Should().ContainSingle().Which.Code.Should().Be(RequirementCodes.CertificateExpired);
    }

    [Fact]
    public async Task Certificado_con_contrasena_incorrecta_bloquea_como_inutilizable()
    {
        var f = new Fixture();
        f.Certificate = f.Certificate with { PasswordCorrect = false, Valid = false, NotAfterUtc = null };

        var result = await f.ValidateAsync();

        result.Issues.Should().ContainSingle().Which.Code.Should().Be(RequirementCodes.CertificateUnusable);
    }

    [Fact]
    public async Task Certificado_proximo_a_vencer_solo_advierte()
    {
        var f = new Fixture();
        f.Certificate = f.Certificate with { NotAfterUtc = DateTime.UtcNow.AddDays(10) };

        var result = await f.ValidateAsync();

        result.CanIssue.Should().BeTrue();
        result.Issues.Should().ContainSingle().Which.Should().Be(
            new ElectronicEmissionRequirementIssue(RequirementCodes.CertificateExpiring,
                ElectronicEmissionRequirementSeverity.Warning,
                ReadinessActionTarget.ElectronicInvoicingSettings));
        result.ToFailure<object>().Should().BeNull();
    }

    [Fact]
    public async Task Ambiente_y_WSDL_invalidos_y_RUC_incompleto_se_reportan_con_codigos_estables()
    {
        var f = new Fixture();
        f.Settings = SriSettings.Create(f.TenantId, f.CompanyId, environment: 9, emissionType: 1,
            wsdlUrl: "no-es-una-url", UserId);
        f.Settings.AttachCertificate("certs/x.p12", "x.p12", 10, DateTime.UtcNow, UserId);
        f.Company.TaxIdentificationNumber = "17900123";

        var result = await f.ValidateAsync();

        result.Issues.Select(i => i.Code).Should().BeEquivalentTo(
            [RequirementCodes.IssuerTaxId, RequirementCodes.SriEnvironment, RequirementCodes.SriWsdlUrl]);
        var failure = result.ToFailure<object>()!;
        failure.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.EmissionRequirementsMissing);
        failure.Error.Should().Contain($"[{RequirementCodes.SriWsdlUrl}] La URL del webservice SRI no es válida.");
    }

    [Fact]
    public async Task Punto_de_emision_fuera_de_la_empresa_bloquea_sin_leer_su_establecimiento()
    {
        var f = new Fixture();

        var result = await f.Build().ValidateAsync(new(f.TenantId, f.CompanyId, Guid.NewGuid(), "01"));

        result.Issues.Should().ContainSingle().Which.Code.Should().Be(RequirementCodes.EmissionPoint);
    }

    [Fact]
    public void El_gate_no_depende_de_ningun_servicio_de_red_SRI()
    {
        var dependencies = typeof(ElectronicEmissionRequirementsValidator).GetConstructors()
            .Single().GetParameters().Select(p => p.ParameterType).ToList();

        dependencies.Should().NotContain(t =>
            t.Namespace!.Contains("SRI", StringComparison.Ordinal)
            || t.Name.Contains("Connectivity", StringComparison.Ordinal)
            || t.Name.Contains("Reception", StringComparison.Ordinal)
            || t.Name.Contains("Authorization", StringComparison.Ordinal));
    }

    private sealed class Fixture
    {
        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid CompanyId { get; }
        public Company Company { get; }
        public Establishment Main { get; }
        public EmissionPoint Point { get; private set; }
        public Establishment PointEstablishment { get; private set; }
        public SriSettings? Settings { get; set; }
        public SriCertificateStatus Certificate { get; set; } =
            new(true, true, true, DateTime.UtcNow.AddYears(1), "CN=TEST", "CN=CA", null);
        public Mock<ISriCertificateStatusResolver> Certificates { get; } = new();

        public Fixture()
        {
            Company = Company.CreateManaged(TenantId, "1790012345001", "Empresa S.A.", createdBy: UserId);
            CompanyId = Company.Id;
            Main = Establishment.Create(TenantId, null, CompanyId, "001", "Matriz", "Av. Matriz 1", null,
                true, UserId);
            PointEstablishment = Main;
            Point = EmissionPoint.Create(TenantId, CompanyId, Main.Id, "001", "PE", EmissionType.Electronic,
                true, UserId);
            Settings = SriSettings.Create(TenantId, CompanyId, environment: 1, emissionType: 1,
                wsdlUrl: "https://celcer.sri.gob.ec/ws", UserId);
            Settings.AttachCertificate("certs/x.p12", "x.p12", 10, DateTime.UtcNow, UserId);
        }

        public void UseBranchEstablishment(string address)
        {
            PointEstablishment = Establishment.Create(TenantId, null, CompanyId, "002", "Sucursal", address,
                null, false, UserId);
            Point = EmissionPoint.Create(TenantId, CompanyId, PointEstablishment.Id, "001", "PE-2",
                EmissionType.Electronic, false, UserId);
        }

        public Task<ElectronicEmissionRequirementsResult> ValidateAsync() =>
            Build().ValidateAsync(new(TenantId, CompanyId, Point.Id, "01"));

        public ElectronicEmissionRequirementsValidator Build()
        {
            var companies = new Mock<ICompanyRepository>();
            companies.Setup(r => r.GetByIdForTenantAsync(CompanyId, TenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Company);
            var establishments = new Mock<IEstablishmentRepository>();
            establishments.Setup(r => r.GetMainByCompanyAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Main);
            establishments.Setup(r => r.GetByIdForCompanyAsync(TenantId, CompanyId, PointEstablishment.Id,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(PointEstablishment);
            var points = new Mock<IEmissionPointRepository>();
            points.Setup(r => r.GetByIdForCompanyAsync(TenantId, CompanyId, Point.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Point);
            var settings = new Mock<ISriSettingsRepository>();
            settings.Setup(r => r.GetByCompanyIdAsync(CompanyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Settings);
            Certificates.Setup(c => c.ResolveAsync(It.IsAny<SriSettings>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Certificate);
            var docTypes = new Mock<ISriDocTypeCatalogResolver>();
            docTypes.Setup(d => d.IsActiveElectronicDocTypeAsync("01", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            return new ElectronicEmissionRequirementsValidator(companies.Object, establishments.Object,
                points.Object, settings.Object, Certificates.Object, docTypes.Object);
        }
    }
}
