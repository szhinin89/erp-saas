using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Company.PhysicalEmission;
using ERP.Application.Modules.ElectronicDocuments.Readiness;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Company.Interfaces;
using FluentAssertions;
using Moq;
using CompanyEntity = ERP.Domain.Modules.Company.Entities.Company;
using PhysicalCodes = ERP.Application.Modules.Company.PhysicalEmission.PhysicalEmissionRequirementCodes;

namespace ERP.Application.Tests.Companies;

/// <summary>
/// PHYSICAL-FISCAL-F1..F5 — política fiscal propietaria de la emisión física. Para aislar las reglas
/// de autorización, vigencia y rango, el catálogo régimen × tipo físico se simula por defecto con un
/// par HIPOTÉTICO permitido (01→01); el catálogo sembrado real (solo 03→02 activo, General→01
/// inactivo) se simula en <see cref="Fixture.UseSeededCatalog"/> y se prueba sobre PostgreSQL en
/// <c>SalesInvoiceAuthorizedPostingIntegrationTests.ReadinessGap</c>.
/// </summary>
public sealed class PhysicalEmissionRequirementsValidatorTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 10, 30);

    private sealed class Fixture
    {
        public CompanyEntity Company { get; } =
            CompanyEntity.CreateManaged(TenantId, "1790012345001", "Supermercado Test S.A.");

        public EmissionPoint Point { get; }
        public Mock<ICompanyRepository> Companies { get; } = new();
        public Mock<IEmissionPointRepository> Points { get; } = new();
        public Mock<IPhysicalDocumentAuthorizationRepository> Authorizations { get; } = new();
        public Mock<IDocumentSequenceRepository> Sequences { get; } = new();
        public Mock<ICompanyClock> Clock { get; } = new();
        public List<PhysicalDocumentAuthorization> Active { get; } = [];
        private readonly HashSet<string> _activeDocTypes = ["01", "04", "07"];
        private HashSet<(string Regime, string DocType)> _permitted = [("01", "01"), ("03", "02")];

        /// <summary>Filas activas sembradas por la migración: solo Negocio Popular → 02.</summary>
        public void UseSeededCatalog() => _permitted = [("03", "02")];

        public Fixture()
        {
            Company.TaxRegimeCode = "01";
            Point = NewPoint(EmissionType.Physical, "002");
            Companies
                .Setup(r => r.GetByIdForTenantAsync(Company.Id, TenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Company);
            Points
                .Setup(r =>
                    r.GetByIdForCompanyAsync(TenantId, Company.Id, Point.Id, It.IsAny<CancellationToken>())
                )
                .ReturnsAsync(Point);
            Authorizations
                .Setup(r =>
                    r.GetActiveAsync(
                        TenantId,
                        Company.Id,
                        It.IsAny<Guid>(),
                        It.IsAny<string>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(
                    (Guid _, Guid _, Guid pointId, string docType, CancellationToken _) =>
                        Active.Where(a => a.EmissionPointId == pointId && a.DocTypeCode == docType)
                            .ToList()
                );
            Clock
                .Setup(c => c.TodayAsync(Company.Id, TenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Today);
        }

        public EmissionPoint NewPoint(EmissionType type, string code) =>
            EmissionPoint.Create(TenantId, Company.Id, Guid.NewGuid(), code, null, type, false, UserId);

        public PhysicalDocumentAuthorization Authorize(
            DateOnly authorizedOn,
            DateOnly expiresOn,
            int first = 1,
            int last = 500,
            Guid? pointId = null,
            string docType = "01"
        )
        {
            var authorization = PhysicalDocumentAuthorization.Create(
                TenantId,
                Company.Id,
                pointId ?? Point.Id,
                docType,
                "1122334455",
                authorizedOn,
                expiresOn,
                first,
                last,
                UserId
            );
            Active.Add(authorization);
            return authorization;
        }

        public void NextSequential(int next)
        {
            var sequence = DocumentSequence.Create(TenantId, Company.Id, Point.Id, "01");
            sequence.ConfigureNextNumber(next);
            Sequences
                .Setup(r => r.GetByEmissionPointAndDocTypeAsync(Point.Id, "01", It.IsAny<CancellationToken>()))
                .ReturnsAsync(sequence);
        }

        public void DocTypeInactive(string code) => _activeDocTypes.Remove(code);

        public Task<ElectronicEmissionRequirementsResult> Validate(
            string docType = "01",
            Guid? pointId = null
        ) =>
            Build()
                .ValidateAsync(
                    new PhysicalEmissionRequirementsRequest(TenantId, Company.Id, pointId ?? Point.Id, docType)
                );

        public PhysicalEmissionRequirementsValidator Build()
        {
            var docTypes = new Mock<ISriDocTypeCatalogResolver>();
            docTypes
                .Setup(d => d.IsActiveDocTypeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string code, CancellationToken _) => _activeDocTypes.Contains(code));
            // Filas sembradas: Régimen General → factura; RIMPE Negocio Popular → nota de venta.
            docTypes
                .Setup(d =>
                    d.IsPhysicalDocTypePermittedAsync(
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(
                    (string regime, string code, CancellationToken _) =>
                        _permitted.Contains((regime, code))
                );
            return new PhysicalEmissionRequirementsValidator(
                Companies.Object,
                Points.Object,
                docTypes.Object,
                Authorizations.Object,
                Sequences.Object,
                Clock.Object
            );
        }
    }

    private static IEnumerable<string> IssueCodes(ElectronicEmissionRequirementsResult result) =>
        result.Issues.Select(i => i.Code);

    [Fact]
    public async Task Con_un_regimen_que_lo_permitiera_autorizacion_vigente_y_secuencial_en_rango_no_hay_requisitos()
    {
        var f = new Fixture();
        f.Authorize(Today.AddMonths(-2), Today.AddMonths(10));
        f.NextSequential(120);

        var result = await f.Validate();

        result.Issues.Should().BeEmpty();
        result.CanIssue.Should().BeTrue();
        result.Modality.Should().Be(EmissionType.Physical);
        result.ToFailure<bool>().Should().BeNull();
    }

    [Fact]
    public async Task Catalogo_sembrado_no_habilita_la_factura_fisica_ordinaria_del_regimen_general_aunque_tenga_autorizacion()
    {
        var f = new Fixture();
        f.UseSeededCatalog();
        f.Authorize(Today.AddMonths(-2), Today.AddMonths(10));
        f.NextSequential(120);

        var result = await f.Validate();

        IssueCodes(result).Should().Equal(PhysicalCodes.DocTypeNotPermitted);
        result.CanIssue.Should().BeFalse();
    }

    [Fact]
    public async Task Rangos_para_la_captura_son_solo_los_de_autorizaciones_en_vigor_hoy()
    {
        var f = new Fixture();
        f.Authorize(Today.AddYears(-1), Today.AddDays(-1), first: 1, last: 500);
        f.Authorize(Today.AddDays(-10), Today.AddMonths(11), first: 501, last: 1000);
        f.Authorize(Today.AddDays(1), Today.AddMonths(12), first: 1001, last: 1500);

        var ranges = await f.Build()
            .GetAuthorizedSequentialRangesAsync(new(TenantId, f.Company.Id, f.Point.Id, "01"));

        ranges.Should().Equal(new AuthorizedSequentialRange(501, 1000));
    }

    [Theory]
    [InlineData("02", "RIMPE Emprendedor: emisión electrónica obligatoria (caso pendiente, fail-closed)")]
    [InlineData("03", "RIMPE Negocio Popular: notas de venta o facturas electrónicas, nunca factura física")]
    public async Task Regimen_que_no_permite_factura_fisica_la_rechaza(string regime, string because)
    {
        var f = new Fixture();
        f.Company.TaxRegimeCode = regime;
        f.Authorize(Today.AddMonths(-2), Today.AddMonths(10));

        var result = await f.Validate();

        IssueCodes(result).Should().Equal([PhysicalCodes.DocTypeNotPermitted], because);
        result.CanIssue.Should().BeFalse();
    }

    [Fact]
    public async Task Nota_de_venta_02_no_se_habilita_aunque_el_regimen_la_permita_porque_no_esta_implementada()
    {
        var f = new Fixture();
        f.Company.TaxRegimeCode = "03";
        f.DocTypeInactive("02");
        f.Authorize(Today.AddMonths(-2), Today.AddMonths(10), docType: "02");

        var result = await f.Validate(docType: "02");

        IssueCodes(result).Should().Equal(PhysicalCodes.DocType);
    }

    [Fact]
    public async Task Empresa_sin_regimen_tributario_no_puede_emitir_en_fisico()
    {
        var f = new Fixture();
        f.Company.TaxRegimeCode = null;
        f.Authorize(Today.AddMonths(-2), Today.AddMonths(10));

        var result = await f.Validate();

        IssueCodes(result).Should().Equal(PhysicalCodes.TaxRegime);
        result.Issues.Single().ActionTarget.Should().Be(ReadinessActionTarget.CompanyFiscalSettings);
    }

    [Fact]
    public async Task Sin_autorizacion_de_impresion_no_puede_emitir()
    {
        var f = new Fixture();

        var result = await f.Validate();

        IssueCodes(result).Should().Equal(PhysicalCodes.Authorization);
        result.Issues.Single().ActionTarget
            .Should().Be(ReadinessActionTarget.PhysicalDocumentAuthorizations);
    }

    [Fact]
    public async Task Autorizacion_vencida_no_permite_emitir()
    {
        var f = new Fixture();
        f.Authorize(Today.AddYears(-1), Today.AddDays(-1));

        var result = await f.Validate();

        IssueCodes(result).Should().Equal(PhysicalCodes.AuthorizationExpired);
    }

    [Fact]
    public async Task Autorizacion_es_valida_hasta_el_ultimo_dia_de_su_caducidad_en_el_dia_de_la_empresa()
    {
        var f = new Fixture();
        f.Authorize(Today.AddMonths(-11), Today);

        (await f.Validate()).Issues.Should().BeEmpty();
    }

    [Fact]
    public async Task Autorizacion_aun_no_vigente_no_permite_emitir()
    {
        var f = new Fixture();
        f.Authorize(Today.AddDays(1), Today.AddMonths(6));

        IssueCodes(await f.Validate()).Should().Equal(PhysicalCodes.Authorization);
    }

    [Fact]
    public async Task Siguiente_secuencial_fuera_del_rango_autorizado_no_permite_emitir()
    {
        var f = new Fixture();
        f.Authorize(Today.AddMonths(-2), Today.AddMonths(10), first: 1, last: 500);
        f.NextSequential(501);

        IssueCodes(await f.Validate()).Should().Equal(PhysicalCodes.SequentialOutOfRange);
    }

    [Fact]
    public async Task Sin_secuencia_previa_el_primer_secuencial_1_debe_estar_autorizado()
    {
        var f = new Fixture();
        f.Authorize(Today.AddMonths(-2), Today.AddMonths(10), first: 851, last: 1350);

        IssueCodes(await f.Validate()).Should().Equal(PhysicalCodes.SequentialOutOfRange);
    }

    [Fact]
    public async Task El_rango_que_cubre_el_siguiente_secuencial_decide_la_vigencia()
    {
        var f = new Fixture();
        // Primer bloque agotado y caducado; el bloque vigente cubre el siguiente número.
        f.Authorize(Today.AddYears(-1), Today.AddDays(-30), first: 1, last: 500);
        f.Authorize(Today.AddDays(-29), Today.AddMonths(11), first: 501, last: 1000);
        f.NextSequential(501);

        (await f.Validate()).Issues.Should().BeEmpty();
    }

    [Fact]
    public async Task Autorizacion_de_otro_punto_no_habilita_este_punto()
    {
        var f = new Fixture();
        var other = f.NewPoint(EmissionType.Physical, "003");
        f.Authorize(Today.AddMonths(-2), Today.AddMonths(10), pointId: other.Id);

        IssueCodes(await f.Validate()).Should().Equal(PhysicalCodes.Authorization);
    }

    [Fact]
    public async Task Punto_inactivo_o_ajeno_a_la_empresa_no_puede_emitir()
    {
        var f = new Fixture();
        f.Authorize(Today.AddMonths(-2), Today.AddMonths(10));
        f.Point.Disable(UserId);

        IssueCodes(await f.Validate()).Should().Equal(PhysicalCodes.EmissionPoint);
        // Un punto de otra empresa no se resuelve con el scope de esta empresa.
        IssueCodes(await f.Validate(pointId: Guid.NewGuid())).Should().Equal(PhysicalCodes.EmissionPoint);
    }

    [Fact]
    public async Task Ruc_provisional_del_bootstrap_no_permite_emitir_en_fisico()
    {
        var f = new Fixture();
        f.Company.IsTemporaryTaxIdentification = true;
        f.Authorize(Today.AddMonths(-2), Today.AddMonths(10));

        var result = await f.Validate();

        IssueCodes(result).Should().Equal(PhysicalCodes.IssuerTaxId);
        result.Issues.Single().ActionTarget.Should().Be(ReadinessActionTarget.CompanyProfile);
    }

    [Fact]
    public async Task Empresa_fuera_del_scope_falla_cerrado_sin_mas_lecturas()
    {
        var f = new Fixture();

        var result = await new PhysicalEmissionRequirementsValidator(
            f.Companies.Object,
            f.Points.Object,
            Mock.Of<ISriDocTypeCatalogResolver>(),
            f.Authorizations.Object,
            f.Sequences.Object,
            f.Clock.Object
        ).ValidateAsync(new PhysicalEmissionRequirementsRequest(TenantId, Guid.NewGuid(), f.Point.Id, "01"));

        IssueCodes(result).Should().Equal(PhysicalCodes.IssuerTaxId);
        f.Points.VerifyNoOtherCalls();
        f.Authorizations.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task El_rechazo_usa_el_codigo_y_los_mensajes_de_la_emision_fisica()
    {
        var f = new Fixture();
        f.Authorize(Today.AddYears(-1), Today.AddDays(-1));

        var failure = (await f.Validate()).ToFailure<bool>();

        failure.Should().NotBeNull();
        failure!.Code.Should().Be(ApiResponseCodes.PhysicalEmission.RequirementsMissing);
        failure.Error.Should()
            .Contain("comprobante físico")
            .And.Contain($"[{PhysicalCodes.AuthorizationExpired}]")
            .And.Contain("caducada")
            .And.NotContain("electrónico");
    }
}
