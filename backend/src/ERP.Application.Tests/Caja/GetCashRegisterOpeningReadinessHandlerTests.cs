using ERP.Application.Common;
using ERP.Application.Modules.Caja;
using ERP.Application.Modules.Caja.UseCases;
using ERP.Application.Modules.Companies;
using ERP.Application.Modules.Company.PhysicalEmission;
using ERP.Application.Modules.ElectronicDocuments.Readiness;
using ERP.Application.Modules.Sales.Services;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Company.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ERP.Application.Tests.Caja;

/// <summary>
/// READINESS-2B-3 — diagnóstico previo a la apertura: cada caja se evalúa con su propio punto y
/// la estrategia de emisión real de ese punto (la física, con la política fiscal física simulada
/// por punto; la electrónica, el validador propietario simulado por punto), en la sucursal activa.
/// </summary>
public sealed class GetCashRegisterOpeningReadinessHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private sealed class Fixture
    {
        private readonly Dictionary<Guid, ElectronicEmissionRequirementIssue[]> _issuesByPoint =
            [];
        private readonly Dictionary<Guid, ElectronicEmissionRequirementIssue[]> _physicalIssuesByPoint =
            [];

        public Mock<ICashRegisterRepository> Registers { get; } = new();
        public Mock<IEmissionPointRepository> Points { get; } = new();
        public Mock<ICurrentTenant> Tenant { get; } = new();
        public Mock<ICurrentBranch> Branch { get; } = new();
        public Mock<ISalesInvoiceEmissionStrategy> Electronic { get; } = new();
        public Mock<IPhysicalEmissionRequirementsValidator> Physical { get; } = new();

        public Fixture()
        {
            Tenant.Setup(t => t.TenantId).Returns(TenantId);
            Electronic
                .Setup(s =>
                    s.CheckRequirementsAsync(
                        It.IsAny<SalesInvoiceEmissionPreflight>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(
                    (SalesInvoiceEmissionPreflight p, CancellationToken _) =>
                        new ElectronicEmissionRequirementsResult(
                            _issuesByPoint.GetValueOrDefault(p.EmissionPointId) ?? []
                        )
                );
            Physical
                .Setup(v =>
                    v.ValidateAsync(
                        It.IsAny<PhysicalEmissionRequirementsRequest>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(
                    (PhysicalEmissionRequirementsRequest r, CancellationToken _) =>
                        new ElectronicEmissionRequirementsResult(
                            _physicalIssuesByPoint.GetValueOrDefault(r.EmissionPointId) ?? []
                        )
                        {
                            Modality = EmissionType.Physical,
                        }
                );
        }

        public void PhysicalRequirements(
            Guid pointId,
            params ElectronicEmissionRequirementIssue[] issues
        ) => _physicalIssuesByPoint[pointId] = issues;

        public void Requirements(Guid pointId, params ElectronicEmissionRequirementIssue[] issues) =>
            _issuesByPoint[pointId] = issues;

        public CashRegister AddRegister(Guid branchId, EmissionPoint? point, string code)
        {
            var register = CashRegister.Create(
                TenantId,
                CompanyId,
                branchId,
                code,
                code,
                UserId,
                point?.Id
            );
            Registers
                .Setup(r => r.GetByIdAsync(TenantId, register.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(register);
            if (point is not null)
                Points
                    .Setup(r =>
                        r.GetByIdForCompanyAsync(
                            TenantId,
                            CompanyId,
                            point.Id,
                            It.IsAny<CancellationToken>()
                        )
                    )
                    .ReturnsAsync(point);
            return register;
        }

        public Task<Result<ERP.Application.Modules.Caja.DTOs.CashRegisterOpeningReadinessDto>> Query(
            Guid activeBranchId,
            Guid registerId
        )
        {
            Branch.Setup(b => b.BranchId).Returns(activeBranchId);
            var strategies = new SalesInvoiceEmissionStrategyResolver([
                new PhysicalSalesInvoiceEmissionStrategy(
                    Physical.Object,
                    NullLogger<PhysicalSalesInvoiceEmissionStrategy>.Instance
                ),
                ElectronicStrategy(),
            ]);
            return new GetCashRegisterOpeningReadinessHandler(
                Registers.Object,
                Points.Object,
                new CashRegisterOpeningReadinessChecker(
                    strategies,
                    NullLogger<CashRegisterOpeningReadinessChecker>.Instance
                ),
                Tenant.Object,
                Branch.Object
            ).Handle(new GetCashRegisterOpeningReadinessQuery(registerId), CancellationToken.None);
        }

        private ISalesInvoiceEmissionStrategy ElectronicStrategy()
        {
            Electronic.Setup(s => s.SupportedType).Returns(EmissionType.Electronic);
            return Electronic.Object;
        }
    }

    private static EmissionPoint Point(EmissionType type, string code) =>
        EmissionPoint.Create(TenantId, CompanyId, Guid.NewGuid(), code, null, type, false, UserId);

    private static ElectronicEmissionRequirementIssue PhysicalRequired(string code) =>
        new(
            code,
            ElectronicEmissionRequirementSeverity.Required,
            ReadinessActionTarget.PhysicalDocumentAuthorizations
        );

    private static ElectronicEmissionRequirementIssue Required(string code) =>
        new(
            code,
            ElectronicEmissionRequirementSeverity.Required,
            ReadinessActionTarget.ElectronicInvoicingSettings
        );

    [Fact]
    public async Task Dos_cajas_de_la_misma_sucursal_se_evaluan_cada_una_con_su_punto()
    {
        var f = new Fixture();
        var branchId = Guid.NewGuid();
        var ready = Point(EmissionType.Electronic, "002");
        var withoutCertificate = Point(EmissionType.Electronic, "003");
        f.Requirements(withoutCertificate.Id, Required(ElectronicEmissionRequirementCodes.Certificate));
        var cajaLista = f.AddRegister(branchId, ready, "CAJA-01");
        var cajaPendiente = f.AddRegister(branchId, withoutCertificate, "CAJA-02");

        var lista = (await f.Query(branchId, cajaLista.Id)).Value!;
        var pendiente = (await f.Query(branchId, cajaPendiente.Id)).Value!;

        lista.CanOpen.Should().BeTrue();
        lista.FiscalStatus.Should().Be("Verified");
        lista.EmissionPointId.Should().Be(ready.Id);
        lista.Requirements.Should().BeEmpty();

        pendiente.CanOpen.Should().BeFalse();
        pendiente.FiscalStatus.Should().Be("Pending");
        pendiente.Requirements.Should()
            .ContainSingle()
            .Which.Should()
            .Be(
                new ERP.Application.Modules.Caja.DTOs.CashRegisterOpeningRequirementDto(
                    ElectronicEmissionRequirementCodes.Certificate,
                    "Required",
                    "ElectronicInvoicingSettings"
                )
            );
    }

    [Fact]
    public async Task Caja_fisica_que_cumple_la_politica_fisica_abre_y_su_estado_fiscal_es_NotVerified()
    {
        var f = new Fixture();
        var branchId = Guid.NewGuid();
        var point = Point(EmissionType.Physical, "002");
        var register = f.AddRegister(branchId, point, "CAJA-01");

        var dto = (await f.Query(branchId, register.Id)).Value!;

        dto.CanOpen.Should().BeTrue();
        dto.EmissionType.Should().Be("Physical");
        dto.FiscalStatus.Should()
            .Be("NotVerified", "la autorización ante el SRI no se verifica localmente");
        f.Physical.Verify(
            v =>
                v.ValidateAsync(
                    It.Is<PhysicalEmissionRequirementsRequest>(r =>
                        r.EmissionPointId == point.Id
                        && r.CompanyId == CompanyId
                        && r.TenantId == TenantId
                        && r.DocTypeCode == "01"
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        f.Electronic.Verify(
            s =>
                s.CheckRequirementsAsync(
                    It.IsAny<SalesInvoiceEmissionPreflight>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task Caja_sin_punto_o_con_punto_desactivado_no_puede_abrir()
    {
        var f = new Fixture();
        var branchId = Guid.NewGuid();
        var inactive = Point(EmissionType.Electronic, "002");
        inactive.Disable(UserId);
        var sinPunto = f.AddRegister(branchId, null, "CAJA-01");
        var conPuntoInactivo = f.AddRegister(branchId, inactive, "CAJA-02");

        foreach (var register in new[] { sinPunto, conPuntoInactivo })
        {
            var dto = (await f.Query(branchId, register.Id)).Value!;
            dto.CanOpen.Should().BeFalse();
            dto.EmissionPointId.Should().BeNull();
            dto.FiscalStatus.Should().Be("NotApplicable");
            dto.Requirements.Should()
                .ContainSingle()
                .Which.Should()
                .Be(
                    new ERP.Application.Modules.Caja.DTOs.CashRegisterOpeningRequirementDto(
                        OperationalReadinessCodes.CashRegisterWithEmissionPoint,
                        "Required",
                        "CashRegisters"
                    )
                );
        }
    }

    [Fact]
    public async Task Requisitos_con_item_base_se_publican_con_el_codigo_de_Readiness()
    {
        var f = new Fixture();
        var branchId = Guid.NewGuid();
        var point = Point(EmissionType.Electronic, "002");
        f.Requirements(
            point.Id,
            Required(ElectronicEmissionRequirementCodes.Company),
            Required(ElectronicEmissionRequirementCodes.IssuerTaxId),
            new(
                ElectronicEmissionRequirementCodes.CertificateExpiring,
                ElectronicEmissionRequirementSeverity.Warning,
                ReadinessActionTarget.ElectronicInvoicingSettings
            )
        );
        var register = f.AddRegister(branchId, point, "CAJA-01");

        var dto = (await f.Query(branchId, register.Id)).Value!;

        dto.CanOpen.Should().BeFalse();
        dto.Requirements.Select(r => (r.Code, r.Severity))
            .Should()
            .BeEquivalentTo([
                (OperationalReadinessCodes.IdentityTaxId, "Required"),
                (ElectronicEmissionRequirementCodes.CertificateExpiring, "Warning"),
            ]);
    }

    [Fact]
    public async Task Caja_fisica_sin_autorizacion_vigente_no_puede_abrir_y_la_otra_caja_si()
    {
        var f = new Fixture();
        var branchId = Guid.NewGuid();
        var authorized = Point(EmissionType.Physical, "002");
        var expired = Point(EmissionType.Physical, "003");
        f.PhysicalRequirements(
            expired.Id,
            PhysicalRequired(PhysicalEmissionRequirementCodes.AuthorizationExpired)
        );
        var cajaLista = f.AddRegister(branchId, authorized, "CAJA-01");
        var cajaVencida = f.AddRegister(branchId, expired, "CAJA-02");

        var lista = (await f.Query(branchId, cajaLista.Id)).Value!;
        var vencida = (await f.Query(branchId, cajaVencida.Id)).Value!;

        lista.CanOpen.Should().BeTrue();
        vencida.CanOpen.Should().BeFalse();
        vencida.FiscalStatus.Should().Be("Pending");
        vencida.Requirements.Should()
            .ContainSingle()
            .Which.Should()
            .BeEquivalentTo(
                new
                {
                    Code = PhysicalEmissionRequirementCodes.AuthorizationExpired,
                    Severity = "Required",
                    ActionTarget = "PhysicalDocumentAuthorizations",
                }
            );
    }

    [Fact]
    public async Task Ruc_provisional_en_caja_fisica_se_publica_con_el_codigo_base_de_Readiness()
    {
        var f = new Fixture();
        var branchId = Guid.NewGuid();
        var point = Point(EmissionType.Physical, "002");
        f.PhysicalRequirements(
            point.Id,
            new ElectronicEmissionRequirementIssue(
                PhysicalEmissionRequirementCodes.IssuerTaxId,
                ElectronicEmissionRequirementSeverity.Required,
                ReadinessActionTarget.CompanyProfile
            )
        );
        var register = f.AddRegister(branchId, point, "CAJA-01");

        var dto = (await f.Query(branchId, register.Id)).Value!;

        dto.CanOpen.Should().BeFalse();
        dto.Requirements.Select(r => r.Code)
            .Should()
            .Equal(OperationalReadinessCodes.IdentityTaxId);
    }

    [Fact]
    public async Task Fallo_tecnico_de_la_politica_fisica_nunca_habilita_la_caja()
    {
        var f = new Fixture();
        var branchId = Guid.NewGuid();
        var register = f.AddRegister(branchId, Point(EmissionType.Physical, "002"), "CAJA-01");
        f.Physical.Setup(v =>
                v.ValidateAsync(
                    It.IsAny<PhysicalEmissionRequirementsRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new InvalidOperationException("db down"));

        var dto = (await f.Query(branchId, register.Id)).Value!;

        dto.CanOpen.Should().BeFalse();
        dto.FiscalStatus.Should().Be("Pending");
        dto.Requirements.Select(r => r.Code)
            .Should()
            .Equal(OperationalReadinessCodes.PhysicalVerificationFailed);
    }

    [Fact]
    public async Task Caja_de_otra_sucursal_o_tras_cambiar_de_sucursal_no_se_expone()
    {
        var f = new Fixture();
        var matriz = Guid.NewGuid();
        var norte = Guid.NewGuid();
        var cajaMatriz = f.AddRegister(matriz, Point(EmissionType.Physical, "002"), "CAJA-01");
        var cajaNorte = f.AddRegister(norte, Point(EmissionType.Physical, "001"), "CAJA-N1");

        (await f.Query(matriz, cajaMatriz.Id)).IsSuccess.Should().BeTrue();
        (await f.Query(norte, cajaNorte.Id)).IsSuccess.Should().BeTrue();

        var otraSucursal = await f.Query(norte, cajaMatriz.Id);
        otraSucursal.IsSuccess.Should().BeFalse();
        otraSucursal.Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }

    [Fact]
    public async Task Configuracion_que_cambia_se_refleja_en_la_siguiente_evaluacion()
    {
        var f = new Fixture();
        var branchId = Guid.NewGuid();
        var point = Point(EmissionType.Electronic, "002");
        var register = f.AddRegister(branchId, point, "CAJA-01");

        (await f.Query(branchId, register.Id)).Value!.CanOpen.Should().BeTrue();

        // El certificado vence después de la primera consulta: nada se cachea.
        f.Requirements(point.Id, Required(ElectronicEmissionRequirementCodes.CertificateExpired));
        var after = (await f.Query(branchId, register.Id)).Value!;

        after.CanOpen.Should().BeFalse();
        after.Requirements.Select(r => r.Code)
            .Should()
            .Equal(ElectronicEmissionRequirementCodes.CertificateExpired);
    }
}
