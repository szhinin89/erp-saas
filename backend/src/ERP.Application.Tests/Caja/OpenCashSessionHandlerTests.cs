using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.Caja;
using ERP.Application.Modules.Caja.UseCases;
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
/// Fase 2 (ADR — Rediseño del módulo de Caja) — OpenCashSessionHandler resuelve CashRegister,
/// deriva BranchId/CompanyId/EmissionPointId exclusivamente server-side (nunca del cliente) y
/// aplica las 6 reglas de apertura: caja existe, activa, misma sucursal, con punto de emisión,
/// sin sesión abierta por caja, sin sesión abierta por usuario.
/// </summary>
public sealed class OpenCashSessionHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid EstablishmentId = Guid.NewGuid();

    private static EmissionPoint CreateEmissionPoint(Guid id) =>
        EmissionPoint.Create(
            TenantId,
            CompanyId,
            EstablishmentId,
            "001",
            null,
            EmissionType.Electronic,
            true,
            UserId
        );

    private static CashRegister CreateRegister(Guid branchId, Guid? emissionPointId) =>
        CashRegister.Create(
            TenantId,
            CompanyId,
            branchId,
            "CAJA-01",
            "Caja Principal",
            UserId,
            emissionPointId
        );

    private sealed class Fixture
    {
        public Mock<ICashSessionRepository> Repo { get; } = new();
        public Mock<ICashRegisterRepository> CashRegisterRepo { get; } = new();
        public Mock<IEmissionPointRepository> EmissionPointRepo { get; } = new();
        public Mock<ICurrentTenant> Tenant { get; } = new();
        public Mock<ICurrentBranch> Branch { get; } = new();
        public Mock<ICurrentUser> User { get; } = new();
        public Mock<IDatabaseExceptionTranslator> DbEx { get; } = new();
        public Mock<ISalesInvoiceEmissionStrategyResolver> EmissionStrategies { get; } = new();
        public Mock<ISalesInvoiceEmissionStrategy> EmissionStrategy { get; } = new();

        public Fixture()
        {
            // Por defecto el punto no exige requisitos (como la estrategia física).
            EmissionStrategies
                .Setup(r => r.Resolve(It.IsAny<EmissionType>()))
                .Returns(EmissionStrategy.Object);
            EmissionStrategy
                .Setup(s =>
                    s.CheckRequirementsAsync(
                        It.IsAny<SalesInvoiceEmissionPreflight>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(ElectronicEmissionRequirementsResult.NotApplicable);
            Tenant.Setup(t => t.TenantId).Returns(TenantId);
            User.Setup(u => u.UserId).Returns(UserId);
            Repo.Setup(r => r.GetOpenByUserAsync(TenantId, UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((CashSession?)null);
            Repo.Setup(r =>
                    r.GetOpenByCashRegisterAsync(
                        TenantId,
                        It.IsAny<Guid>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync((CashSession?)null);

            DatabaseUniqueViolationInfo? none = null;
            DbEx.Setup(d => d.TryGetUniqueViolation(It.IsAny<Exception>(), out none))
                .Returns(false);
        }

        public OpenCashSessionHandler BuildHandler(Guid branchId)
        {
            Branch.Setup(b => b.BranchId).Returns(branchId);
            return new OpenCashSessionHandler(
                Repo.Object,
                CashRegisterRepo.Object,
                EmissionPointRepo.Object,
                new CashRegisterOpeningReadinessChecker(
                    EmissionStrategies.Object,
                    NullLogger<CashRegisterOpeningReadinessChecker>.Instance
                ),
                Tenant.Object,
                Branch.Object,
                User.Object,
                DbEx.Object
            );
        }
    }

    [Fact]
    public async Task Abrir_caja_con_registro_valido_persiste_BranchId_y_EmissionPointId_desde_CashRegister()
    {
        var f = new Fixture();
        var branchId = Guid.NewGuid();
        var emissionPointId = Guid.NewGuid();
        var register = CreateRegister(branchId, emissionPointId);
        var emissionPoint = CreateEmissionPoint(emissionPointId);

        f.CashRegisterRepo.Setup(r =>
                r.GetByIdAsync(TenantId, register.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(register);
        f.EmissionPointRepo.Setup(r =>
                r.GetByIdAsync(emissionPointId, TenantId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(emissionPoint);

        CashSession? captured = null;
        f.Repo.Setup(r => r.AddAsync(It.IsAny<CashSession>(), It.IsAny<CancellationToken>()))
            .Callback<CashSession, CancellationToken>((s, _) => captured = s)
            .Returns(Task.CompletedTask);

        var handler = f.BuildHandler(branchId);
        var result = await handler.Handle(
            new OpenCashSessionCommand(register.Id, 50m),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        captured.Should().NotBeNull();
        captured!.BranchId.Should().Be(branchId);
        captured.CashRegisterId.Should().Be(register.Id);
        captured.EmissionPointId.Should().Be(emissionPointId);
        captured.CashRegisterCodeSnapshot.Should().Be(register.Code);
        captured.CashRegisterNameSnapshot.Should().Be(register.Name);
        captured.EmissionPointCodeSnapshot.Should().Be(emissionPoint.Code);
    }

    [Fact]
    public async Task Caja_inexistente_retorna_NotFound()
    {
        var f = new Fixture();
        var branchId = Guid.NewGuid();
        var missingId = Guid.NewGuid();
        f.CashRegisterRepo.Setup(r =>
                r.GetByIdAsync(TenantId, missingId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((CashRegister?)null);

        var handler = f.BuildHandler(branchId);
        var result = await handler.Handle(
            new OpenCashSessionCommand(missingId, 50m),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Caja_inactiva_retorna_error_de_validacion()
    {
        var f = new Fixture();
        var branchId = Guid.NewGuid();
        var register = CreateRegister(branchId, Guid.NewGuid());
        register.Disable(UserId);
        f.CashRegisterRepo.Setup(r =>
                r.GetByIdAsync(TenantId, register.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(register);

        var handler = f.BuildHandler(branchId);
        var result = await handler.Handle(
            new OpenCashSessionCommand(register.Id, 50m),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Caja_sin_punto_de_emision_retorna_error_de_validacion()
    {
        var f = new Fixture();
        var branchId = Guid.NewGuid();
        var register = CreateRegister(branchId, emissionPointId: null);
        f.CashRegisterRepo.Setup(r =>
                r.GetByIdAsync(TenantId, register.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(register);

        var handler = f.BuildHandler(branchId);
        var result = await handler.Handle(
            new OpenCashSessionCommand(register.Id, 50m),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Caja_de_otra_sucursal_retorna_error_de_validacion()
    {
        var f = new Fixture();
        var registerBranchId = Guid.NewGuid();
        var activeBranchId = Guid.NewGuid();
        var register = CreateRegister(registerBranchId, Guid.NewGuid());
        f.CashRegisterRepo.Setup(r =>
                r.GetByIdAsync(TenantId, register.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(register);

        var handler = f.BuildHandler(activeBranchId);
        var result = await handler.Handle(
            new OpenCashSessionCommand(register.Id, 50m),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Usuario_con_sesion_abierta_retorna_error_de_validacion()
    {
        var f = new Fixture();
        var branchId = Guid.NewGuid();
        var register = CreateRegister(branchId, Guid.NewGuid());
        var existing = CashSession.Open(
            TenantId,
            CompanyId,
            branchId,
            UserId,
            Guid.NewGuid(),
            "CAJA-00",
            "Otra Caja",
            Guid.NewGuid(),
            "001",
            10m,
            UserId
        );
        f.Repo.Setup(r => r.GetOpenByUserAsync(TenantId, UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var handler = f.BuildHandler(branchId);
        var result = await handler.Handle(
            new OpenCashSessionCommand(register.Id, 50m),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        f.CashRegisterRepo.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Caja_con_sesion_abierta_retorna_error_de_validacion()
    {
        var f = new Fixture();
        var branchId = Guid.NewGuid();
        var register = CreateRegister(branchId, Guid.NewGuid());
        var existing = CashSession.Open(
            TenantId,
            CompanyId,
            branchId,
            Guid.NewGuid(),
            register.Id,
            register.Code,
            register.Name,
            Guid.NewGuid(),
            "001",
            10m,
            UserId
        );

        f.CashRegisterRepo.Setup(r =>
                r.GetByIdAsync(TenantId, register.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(register);
        f.Repo.Setup(r =>
                r.GetOpenByCashRegisterAsync(TenantId, register.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(existing);

        var handler = f.BuildHandler(branchId);
        var result = await handler.Handle(
            new OpenCashSessionCommand(register.Id, 50m),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task EmissionPointId_proviene_del_CashRegister_y_no_del_cliente()
    {
        var f = new Fixture();
        var branchId = Guid.NewGuid();
        var registerEmissionPointId = Guid.NewGuid();
        var register = CreateRegister(branchId, registerEmissionPointId);
        var emissionPoint = CreateEmissionPoint(registerEmissionPointId);

        f.CashRegisterRepo.Setup(r =>
                r.GetByIdAsync(TenantId, register.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(register);
        f.EmissionPointRepo.Setup(r =>
                r.GetByIdAsync(registerEmissionPointId, TenantId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(emissionPoint);

        CashSession? captured = null;
        f.Repo.Setup(r => r.AddAsync(It.IsAny<CashSession>(), It.IsAny<CancellationToken>()))
            .Callback<CashSession, CancellationToken>((s, _) => captured = s)
            .Returns(Task.CompletedTask);

        // El command (OpenCashSessionCommand) no expone ninguna propiedad EmissionPointId —
        // el único dato de entrada del cliente es CashRegisterId + OpeningAmount + Notes.
        typeof(OpenCashSessionCommand)
            .GetProperty("EmissionPointId")
            .Should()
            .BeNull(
                "el cliente nunca debe poder enviar el punto de emisión — se resuelve desde CashRegister"
            );

        var handler = f.BuildHandler(branchId);
        await handler.Handle(new OpenCashSessionCommand(register.Id, 50m), CancellationToken.None);

        captured!.EmissionPointId.Should().Be(registerEmissionPointId);
    }

    /// <summary>
    /// P1-01 (ERP_CORE_SUMAK_READINESS_AUDIT.md): dos aperturas concurrentes para la misma caja o
    /// el mismo usuario pueden ambas pasar las verificaciones previas (GetOpenByUserAsync/
    /// GetOpenByCashRegisterAsync) bajo condición de carrera. El índice único parcial de BD
    /// (ux_cash_sessions_open_per_register / ux_cash_sessions_open_per_company_user,
    /// CashSessionConfiguration) es la defensa final: el "perdedor" no debe recibir un 500 crudo,
    /// sino un Result.Conflict — mismo patrón que CreateAuthenticatedSessionHandler.
    /// </summary>
    [Fact]
    public async Task Si_SaveChangesAsync_falla_por_violacion_de_unicidad_devuelve_Conflict()
    {
        var f = new Fixture();
        var branchId = Guid.NewGuid();
        var register = CreateRegister(branchId, Guid.NewGuid());
        var emissionPoint = CreateEmissionPoint(register.EmissionPointId!.Value);

        f.CashRegisterRepo.Setup(r =>
                r.GetByIdAsync(TenantId, register.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(register);
        f.EmissionPointRepo.Setup(r =>
                r.GetByIdAsync(
                    register.EmissionPointId!.Value,
                    TenantId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(emissionPoint);

        f.Repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Violación de unicidad simulada."));

        var info = new DatabaseUniqueViolationInfo(
            "23505",
            "ux_cash_sessions_open_per_register",
            "cash_sessions",
            null
        );
        f.DbEx.Setup(d => d.TryGetUniqueViolation(It.IsAny<Exception>(), out info)).Returns(true);

        var handler = f.BuildHandler(branchId);
        var result = await handler.Handle(
            new OpenCashSessionCommand(register.Id, 50m),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.Conflict);
    }

    [Fact]
    public async Task Si_SaveChangesAsync_falla_por_una_causa_no_relacionada_a_unicidad_la_excepcion_se_propaga()
    {
        var f = new Fixture();
        var branchId = Guid.NewGuid();
        var register = CreateRegister(branchId, Guid.NewGuid());
        var emissionPoint = CreateEmissionPoint(register.EmissionPointId!.Value);

        f.CashRegisterRepo.Setup(r =>
                r.GetByIdAsync(TenantId, register.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(register);
        f.EmissionPointRepo.Setup(r =>
                r.GetByIdAsync(
                    register.EmissionPointId!.Value,
                    TenantId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(emissionPoint);

        f.Repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Fallo no relacionado a unicidad."));

        var handler = f.BuildHandler(branchId);
        var act = () =>
            handler.Handle(new OpenCashSessionCommand(register.Id, 50m), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ── READINESS-2B-3 — habilitación preventiva de la caja ─────────────────

    private static EmissionPoint CreatePoint(EmissionType type) =>
        EmissionPoint.Create(TenantId, CompanyId, EstablishmentId, "002", null, type, false, UserId);

    private static (CashRegister Register, EmissionPoint Point) Arrange(
        Fixture f,
        Guid branchId,
        EmissionPoint point
    )
    {
        var register = CreateRegister(branchId, point.Id);
        f.CashRegisterRepo.Setup(r =>
                r.GetByIdAsync(TenantId, register.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(register);
        f.EmissionPointRepo.Setup(r =>
                r.GetByIdAsync(point.Id, TenantId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(point);
        return (register, point);
    }

    private static void RequirementsFor(Fixture f, params ElectronicEmissionRequirementIssue[] issues) =>
        f.EmissionStrategy.Setup(s =>
                s.CheckRequirementsAsync(
                    It.IsAny<SalesInvoiceEmissionPreflight>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ElectronicEmissionRequirementsResult(issues));

    private static void ShouldNotCreateSession(Fixture f)
    {
        f.Repo.Verify(
            r => r.AddAsync(It.IsAny<CashSession>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        f.Repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Caja_electronica_preparada_abre_y_valida_el_punto_propio_de_la_caja_como_factura()
    {
        var f = new Fixture();
        var branchId = Guid.NewGuid();
        var (register, point) = Arrange(f, branchId, CreatePoint(EmissionType.Electronic));

        var result = await f.BuildHandler(branchId)
            .Handle(new OpenCashSessionCommand(register.Id, 0m), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        f.EmissionStrategies.Verify(r => r.Resolve(EmissionType.Electronic), Times.Once);
        // El punto efectivo de la caja, nunca el punto por defecto de la empresa.
        f.EmissionStrategy.Verify(
            s =>
                s.CheckRequirementsAsync(
                    It.Is<SalesInvoiceEmissionPreflight>(p =>
                        p.EmissionPointId == point.Id
                        && p.CompanyId == register.CompanyId
                        && p.TenantId == TenantId
                        && p.DocTypeCode == "01"
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task Caja_electronica_sin_certificado_valido_se_rechaza_antes_de_crear_la_sesion()
    {
        var f = new Fixture();
        var branchId = Guid.NewGuid();
        var (register, _) = Arrange(f, branchId, CreatePoint(EmissionType.Electronic));
        RequirementsFor(
            f,
            new ElectronicEmissionRequirementIssue(
                ElectronicEmissionRequirementCodes.Certificate,
                ElectronicEmissionRequirementSeverity.Required,
                ReadinessActionTarget.ElectronicInvoicingSettings
            )
        );

        var result = await f.BuildHandler(branchId)
            .Handle(new OpenCashSessionCommand(register.Id, 0m), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.EmissionRequirementsMissing);
        result.Error.Should().Contain(ElectronicEmissionRequirementCodes.Certificate);
        ShouldNotCreateSession(f);
    }

    [Fact]
    public async Task Certificado_proximo_a_vencer_es_advertencia_y_no_impide_abrir()
    {
        var f = new Fixture();
        var branchId = Guid.NewGuid();
        var (register, _) = Arrange(f, branchId, CreatePoint(EmissionType.Electronic));
        RequirementsFor(
            f,
            new ElectronicEmissionRequirementIssue(
                ElectronicEmissionRequirementCodes.CertificateExpiring,
                ElectronicEmissionRequirementSeverity.Warning,
                ReadinessActionTarget.ElectronicInvoicingSettings
            )
        );

        var result = await f.BuildHandler(branchId)
            .Handle(new OpenCashSessionCommand(register.Id, 0m), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Punto_de_emision_desactivado_se_rechaza_sin_evaluar_requisitos_fiscales()
    {
        var f = new Fixture();
        var branchId = Guid.NewGuid();
        var point = CreatePoint(EmissionType.Electronic);
        point.Disable(UserId);
        var (register, _) = Arrange(f, branchId, point);

        var result = await f.BuildHandler(branchId)
            .Handle(new OpenCashSessionCommand(register.Id, 0m), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("desactivado");
        f.EmissionStrategies.Verify(r => r.Resolve(It.IsAny<EmissionType>()), Times.Never);
        ShouldNotCreateSession(f);
    }

    [Fact]
    public async Task Fallo_tecnico_del_validador_nunca_habilita_la_caja()
    {
        var f = new Fixture();
        var branchId = Guid.NewGuid();
        var (register, _) = Arrange(f, branchId, CreatePoint(EmissionType.Electronic));
        f.EmissionStrategy.Setup(s =>
                s.CheckRequirementsAsync(
                    It.IsAny<SalesInvoiceEmissionPreflight>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new InvalidOperationException("certificado ilegible"));

        var result = await f.BuildHandler(branchId)
            .Handle(new OpenCashSessionCommand(register.Id, 0m), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        ShouldNotCreateSession(f);
    }

    [Fact]
    public async Task Caja_fisica_que_cumple_la_politica_fisica_abre_con_la_estrategia_fisica()
    {
        var f = new Fixture();
        var branchId = Guid.NewGuid();
        var (register, _) = Arrange(f, branchId, CreatePoint(EmissionType.Physical));

        var result = await f.BuildHandler(branchId)
            .Handle(new OpenCashSessionCommand(register.Id, 0m), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        f.EmissionStrategies.Verify(r => r.Resolve(EmissionType.Physical), Times.Once);
    }

    [Theory]
    [InlineData(PhysicalEmissionRequirementCodes.Authorization)]
    [InlineData(PhysicalEmissionRequirementCodes.AuthorizationExpired)]
    [InlineData(PhysicalEmissionRequirementCodes.SequentialOutOfRange)]
    [InlineData(PhysicalEmissionRequirementCodes.DocTypeNotPermitted)]
    public async Task Caja_fisica_sin_requisitos_fiscales_fisicos_se_rechaza_antes_de_crear_la_sesion(
        string code
    )
    {
        var f = new Fixture();
        var branchId = Guid.NewGuid();
        var (register, _) = Arrange(f, branchId, CreatePoint(EmissionType.Physical));
        f.EmissionStrategy.Setup(s =>
                s.CheckRequirementsAsync(
                    It.IsAny<SalesInvoiceEmissionPreflight>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ElectronicEmissionRequirementsResult([
                    new(
                        code,
                        ElectronicEmissionRequirementSeverity.Required,
                        ReadinessActionTarget.PhysicalDocumentAuthorizations
                    ),
                ])
                {
                    Modality = EmissionType.Physical,
                }
            );

        var result = await f.BuildHandler(branchId)
            .Handle(new OpenCashSessionCommand(register.Id, 0m), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.PhysicalEmission.RequirementsMissing);
        result.Error.Should().Contain($"[{code}]").And.Contain("comprobante físico");
        ShouldNotCreateSession(f);
    }

    [Fact]
    public async Task Fallo_tecnico_de_la_politica_fisica_nunca_habilita_la_caja()
    {
        var f = new Fixture();
        var branchId = Guid.NewGuid();
        var (register, _) = Arrange(f, branchId, CreatePoint(EmissionType.Physical));
        f.EmissionStrategy.Setup(s =>
                s.CheckRequirementsAsync(
                    It.IsAny<SalesInvoiceEmissionPreflight>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new InvalidOperationException("db down"));

        var result = await f.BuildHandler(branchId)
            .Handle(new OpenCashSessionCommand(register.Id, 0m), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("emisión física");
        ShouldNotCreateSession(f);
    }
}
