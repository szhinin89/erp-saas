using ERP.Application.Audit.UseCases.GetEntityActivity;
using ERP.Application.Common;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Branches.Interfaces;
using ERP.Domain.Modules.Inventory.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Audit;

/// <summary>
/// IAM-V1-F1 (gate final): la actividad por entidad se acota a la empresa activa verificando la
/// entidad con los repositorios de alcance de empresa; fuera de alcance → lista vacía sin leer
/// la auditoría.
/// </summary>
public sealed class GetEntityActivityHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private sealed class Fixture
    {
        public Mock<IUserActivityRepository> Activity { get; } = new();
        public Mock<IAccessRepository> Access { get; } = new();
        public Mock<IBranchRepository> Branches { get; } = new();
        public Mock<IWarehouseRepository> Warehouses { get; } = new();

        public GetEntityActivityHandler Build(bool hasCompany = true)
        {
            var tenant = new Mock<ICurrentTenant>();
            tenant.Setup(t => t.TenantId).Returns(TenantId);
            var user = new Mock<ICurrentUser>();
            user.Setup(u => u.IsAuthenticated).Returns(true);
            user.Setup(u => u.UserId).Returns(UserId);
            var company = new Mock<ICurrentCompany>();
            company.Setup(c => c.HasCompanyContext).Returns(hasCompany);
            company.Setup(c => c.CompanyId).Returns(hasCompany ? CompanyId : Guid.Empty);
            return new GetEntityActivityHandler(
                Activity.Object,
                tenant.Object,
                user.Object,
                company.Object,
                Access.Object,
                Branches.Object,
                Warehouses.Object
            );
        }

        public void VerifyActivityNeverRead() =>
            Activity.Verify(
                r =>
                    r.GetByEntityAsync(
                        It.IsAny<Guid>(),
                        It.IsAny<string>(),
                        It.IsAny<Guid>(),
                        It.IsAny<int>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never
            );
    }

    [Fact]
    public async Task Sin_empresa_activa_es_Forbidden()
    {
        var f = new Fixture();

        var result = await f.Build(hasCompany: false)
            .Handle(new GetEntityActivityQuery("Branch", Guid.NewGuid(), 10), CancellationToken.None);

        result.Code.Should().Be(ApiResponseCodes.Common.Forbidden);
        f.VerifyActivityNeverRead();
    }

    [Fact]
    public async Task Sucursal_de_otra_empresa_devuelve_lista_vacia_sin_leer_auditoria()
    {
        var f = new Fixture();
        var branchId = Guid.NewGuid();
        f.Branches.Setup(r =>
                r.GetByIdForCompanyAsync(TenantId, CompanyId, branchId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((Branch?)null);

        var result = await f.Build()
            .Handle(new GetEntityActivityQuery("Branch", branchId, 10), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
        f.VerifyActivityNeverRead();
    }

    [Theory]
    [InlineData("Item")]
    [InlineData("branch")]
    [InlineData("Desconocido")]
    public async Task Tipo_sin_verificacion_de_empresa_es_fail_closed(string entityType)
    {
        var f = new Fixture();

        var result = await f.Build()
            .Handle(new GetEntityActivityQuery(entityType, Guid.NewGuid(), 10), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
        f.VerifyActivityNeverRead();
    }

    [Fact]
    public async Task BusinessPartner_dato_tenant_lee_la_actividad_del_tenant()
    {
        var f = new Fixture();
        var partnerId = Guid.NewGuid();
        var row = UserActivity.Create(
            TenantId,
            UserId,
            "ana@test.com",
            "Ana Perez",
            "masterdata",
            "Create",
            "BusinessPartner",
            partnerId,
            "Alta"
        );
        f.Activity.Setup(r =>
                r.GetByEntityAsync(
                    TenantId,
                    "BusinessPartner",
                    partnerId,
                    10,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([row]);

        var result = await f.Build()
            .Handle(
                new GetEntityActivityQuery("BusinessPartner", partnerId, 10),
                CancellationToken.None
            );

        result.Value.Should().ContainSingle().Which.UserFullName.Should().Be("Ana Perez");
    }
}
