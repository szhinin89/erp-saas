using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Company.UseCases.PhysicalDocumentAuthorizations;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Company.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Companies;

/// <summary>PHYSICAL-FISCAL-F1 — registro y baja de autorizaciones de impresión del SRI.</summary>
public sealed class PhysicalDocumentAuthorizationUseCasesTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private sealed class Fixture
    {
        public Mock<IPhysicalDocumentAuthorizationRepository> Repo { get; } = new();
        public Mock<IEmissionPointRepository> Points { get; } = new();
        public Mock<ISriDocTypeCatalogResolver> DocTypes { get; } = new();
        public List<PhysicalDocumentAuthorization> Active { get; } = [];

        public Fixture()
        {
            DocTypes
                .Setup(d => d.IsActiveDocTypeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string code, CancellationToken _) => code == "01");
            Repo
                .Setup(r =>
                    r.GetActiveAsync(
                        TenantId,
                        CompanyId,
                        It.IsAny<Guid>(),
                        It.IsAny<string>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(() => Active);
        }

        public EmissionPoint AddPoint(EmissionType type)
        {
            var point = EmissionPoint.Create(TenantId, CompanyId, Guid.NewGuid(), "002", null, type, false, UserId);
            Points
                .Setup(r => r.GetByIdForCompanyAsync(TenantId, CompanyId, point.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(point);
            return point;
        }

        public Task<Result<PhysicalDocumentAuthorizationDto>> Create(
            Guid pointId,
            int first = 1,
            int last = 500,
            string docType = "01"
        ) =>
            new CreatePhysicalDocumentAuthorizationCommandHandler(
                Repo.Object,
                Points.Object,
                DocTypes.Object,
                Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
                Mock.Of<ICurrentCompany>(c => c.CompanyId == CompanyId),
                Mock.Of<ICurrentUser>(u => u.UserId == UserId)
            ).Handle(
                new CreatePhysicalDocumentAuthorizationCommand(
                    pointId,
                    docType,
                    "1122334455",
                    new DateOnly(2026, 10, 1),
                    new DateOnly(2027, 9, 30),
                    first,
                    last
                ),
                CancellationToken.None
            );

        public void ShouldNotPersist() =>
            Repo.Verify(
                r => r.AddAsync(It.IsAny<PhysicalDocumentAuthorization>(), It.IsAny<CancellationToken>()),
                Times.Never
            );
    }

    [Fact]
    public async Task Registra_la_autorizacion_de_un_punto_fisico_activo()
    {
        var f = new Fixture();
        var point = f.AddPoint(EmissionType.Physical);

        var result = await f.Create(point.Id);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Should().BeEquivalentTo(
            new
            {
                EmissionPointId = point.Id,
                DocTypeCode = "01",
                AuthorizationNumber = "1122334455",
                FirstSequential = 1,
                LastSequential = 500,
                IsActive = true,
            }
        );
        f.Repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Punto_electronico_inactivo_o_de_otra_empresa_se_rechaza()
    {
        var f = new Fixture();
        var electronic = f.AddPoint(EmissionType.Electronic);
        var inactive = f.AddPoint(EmissionType.Physical);
        inactive.Disable(UserId);

        (await f.Create(electronic.Id)).Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        (await f.Create(inactive.Id)).Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        (await f.Create(Guid.NewGuid())).Code.Should().Be(ApiResponseCodes.Common.NotFound);
        f.ShouldNotPersist();
    }

    [Fact]
    public async Task Tipo_no_habilitado_como_la_nota_de_venta_02_se_rechaza()
    {
        var f = new Fixture();
        var point = f.AddPoint(EmissionType.Physical);

        var result = await f.Create(point.Id, docType: "02");

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        f.ShouldNotPersist();
    }

    [Theory]
    [InlineData(400, 600)]
    [InlineData(1, 1)]
    [InlineData(500, 500)]
    public async Task Rango_superpuesto_con_otra_autorizacion_activa_se_rechaza(int first, int last)
    {
        var f = new Fixture();
        var point = f.AddPoint(EmissionType.Physical);
        f.Active.Add(
            PhysicalDocumentAuthorization.Create(
                TenantId,
                CompanyId,
                point.Id,
                "01",
                "999",
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31),
                1,
                500,
                UserId
            )
        );

        var result = await f.Create(point.Id, first, last);

        result.Code.Should().Be(ApiResponseCodes.Common.Conflict);
        f.ShouldNotPersist();
    }

    [Fact]
    public async Task Rango_contiguo_a_otra_autorizacion_se_acepta()
    {
        var f = new Fixture();
        var point = f.AddPoint(EmissionType.Physical);
        f.Active.Add(
            PhysicalDocumentAuthorization.Create(
                TenantId,
                CompanyId,
                point.Id,
                "01",
                "999",
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31),
                1,
                500,
                UserId
            )
        );

        (await f.Create(point.Id, 501, 1000)).IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData("2026-10-01", "2027-10-02", 1, 10, "un año")]
    [InlineData("2026-10-01", "2026-09-30", 1, 10, "un año")]
    [InlineData("2026-10-01", "2027-01-01", 0, 10, "rango")]
    [InlineData("2026-10-01", "2027-01-01", 20, 10, "rango")]
    [InlineData("2026-10-01", "2027-01-01", 1, 1_000_000_000, "rango")]
    public void La_entidad_rechaza_vigencia_mayor_a_un_anio_y_rangos_invalidos(
        string authorizedOn,
        string expiresOn,
        int first,
        int last,
        string because
    )
    {
        var act = () =>
            PhysicalDocumentAuthorization.Create(
                TenantId,
                CompanyId,
                Guid.NewGuid(),
                "01",
                "123",
                DateOnly.Parse(authorizedOn),
                DateOnly.Parse(expiresOn),
                first,
                last,
                UserId
            );

        act.Should().Throw<ArgumentException>(because);
    }

    [Fact]
    public void El_numero_de_autorizacion_solo_admite_digitos()
    {
        var act = () =>
            PhysicalDocumentAuthorization.Create(
                TenantId,
                CompanyId,
                Guid.NewGuid(),
                "01",
                "12-AB",
                new DateOnly(2026, 10, 1),
                new DateOnly(2027, 9, 30),
                1,
                10,
                UserId
            );

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task Deshabilitar_es_soft_delete_y_no_se_repite()
    {
        var authorization = PhysicalDocumentAuthorization.Create(
            TenantId,
            CompanyId,
            Guid.NewGuid(),
            "01",
            "123",
            new DateOnly(2026, 10, 1),
            new DateOnly(2027, 9, 30),
            1,
            10,
            UserId
        );
        var repo = new Mock<IPhysicalDocumentAuthorizationRepository>();
        repo.Setup(r =>
                r.GetByIdForCompanyAsync(TenantId, CompanyId, authorization.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(authorization);
        var handler = new DisablePhysicalDocumentAuthorizationCommandHandler(
            repo.Object,
            Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
            Mock.Of<ICurrentCompany>(c => c.CompanyId == CompanyId),
            Mock.Of<ICurrentUser>(u => u.UserId == UserId)
        );

        (await handler.Handle(new(authorization.Id), CancellationToken.None)).IsSuccess.Should().BeTrue();
        authorization.IsActive.Should().BeFalse();
        (await handler.Handle(new(authorization.Id), CancellationToken.None))
            .Code.Should().Be(ApiResponseCodes.Common.Conflict);
        (await handler.Handle(new(Guid.NewGuid()), CancellationToken.None))
            .Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }
}
