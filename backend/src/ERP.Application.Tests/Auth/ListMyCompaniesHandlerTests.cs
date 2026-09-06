using ERP.Application.Auth.UseCases.ListMyCompanies;
using ERP.Application.Common;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Branches.Interfaces;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Auth;

/// <summary>
/// MEDIO cluster (auditoría multi-tenant) — ListMyCompaniesHandler resuelve las empresas
/// accesibles a partir de las membresías activas del usuario, pero la fuente real de esas
/// membresías (GetActiveCompanyUserMembershipsForUserSystemAsync) no filtra por tenant a nivel de
/// query — el handler filtra defensivamente `.Where(c => c.TenantId == tenantId)` en memoria
/// después de leer las compañías. Este test prueba que ese filtro funciona: si por un ID
/// adivinado/colisión el repo de compañías devolviera una empresa de OTRO tenant, nunca aparece
/// en el resultado.
/// </summary>
public sealed class ListMyCompaniesHandlerTests
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly Mock<IAccessRepository> _accessRepo = new();
    private readonly Mock<ICompanyRepository> _companyRepo = new();
    private readonly Mock<IBranchRepository> _branchRepo = new();
    private readonly Mock<ICompanyUserBranchRepository> _companyUserBranchRepo = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<ICurrentTenant> _currentTenant = new();

    public ListMyCompaniesHandlerTests()
    {
        _branchRepo
            .Setup(r =>
                r.CountActiveByCompanyIdsAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new Dictionary<Guid, int>());
        _companyUserBranchRepo
            .Setup(r =>
                r.CountActiveByMembershipIdsAsync(
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new Dictionary<Guid, int>());
    }

    private ListMyCompaniesHandler BuildHandler() =>
        new(
            _accessRepo.Object,
            _companyRepo.Object,
            _branchRepo.Object,
            _companyUserBranchRepo.Object,
            _currentUser.Object,
            _currentTenant.Object
        );

    [Fact]
    public async Task Compania_de_otro_tenant_devuelta_por_el_repo_nunca_aparece_en_el_resultado()
    {
        var companyOfTenantA = Company.CreateManaged(
            TenantA,
            "1790012345001",
            "Empresa A",
            createdBy: UserId
        );
        var companyOfTenantB = Company.CreateManaged(
            TenantB,
            "1790012345002",
            "Empresa B (otro tenant)",
            createdBy: UserId
        );

        _currentUser.Setup(u => u.IsAuthenticated).Returns(true);
        _currentUser.Setup(u => u.UserId).Returns(UserId);
        _currentTenant.Setup(t => t.TenantId).Returns(TenantA);

        _accessRepo
            .Setup(r =>
                r.GetActiveCompanyUserMembershipsForUserSystemAsync(
                    UserId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new[]
                {
                    CompanyUserMembership.Create(companyOfTenantA.Id, UserId, "Admin", null, UserId),
                    CompanyUserMembership.Create(companyOfTenantB.Id, UserId, "Admin", null, UserId),
                }
            );
        _companyRepo
            .Setup(r =>
                r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new[] { companyOfTenantA, companyOfTenantB });

        var handler = BuildHandler();
        var result = await handler.Handle(new ListMyCompaniesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().ContainSingle();
        result.Value![0].CompanyId.Should().Be(companyOfTenantA.Id);
        result.Value!.Should().NotContain(c => c.CompanyId == companyOfTenantB.Id);
    }

    [Fact]
    public async Task Sin_membresias_activas_devuelve_lista_vacia_sin_consultar_companias()
    {
        _currentUser.Setup(u => u.IsAuthenticated).Returns(true);
        _currentUser.Setup(u => u.UserId).Returns(UserId);
        _currentTenant.Setup(t => t.TenantId).Returns(TenantA);
        _accessRepo
            .Setup(r =>
                r.GetActiveCompanyUserMembershipsForUserSystemAsync(
                    UserId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Array.Empty<CompanyUserMembership>());

        var handler = BuildHandler();
        var result = await handler.Handle(new ListMyCompaniesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
        _companyRepo.Verify(
            r =>
                r.GetByIdsAsync(
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task Expone_estado_operativo_regimen_y_contabilidad_desde_la_entidad_company()
    {
        var company = Company.CreateManaged(TenantA, "1790012345001", "Empresa A", createdBy: UserId);
        var membership = CompanyUserMembership.Create(company.Id, UserId, "User", null, UserId);

        _currentUser.Setup(u => u.IsAuthenticated).Returns(true);
        _currentUser.Setup(u => u.UserId).Returns(UserId);
        _currentTenant.Setup(t => t.TenantId).Returns(TenantA);
        _accessRepo
            .Setup(r =>
                r.GetActiveCompanyUserMembershipsForUserSystemAsync(
                    UserId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new[] { membership });
        _companyRepo
            .Setup(r =>
                r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new[] { company });

        var handler = BuildHandler();
        var result = await handler.Handle(new ListMyCompaniesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var dto = result.Value!.Single();
        dto.IsActive.Should().Be(company.IsActive);
        dto.OperationalStatus.Should().Be(company.OperationalStatus.ToString());
        dto.IsAccountingRequired.Should().Be(company.IsAccountingReq);
        dto.TaxRegime.Should().BeNull(); // sin TaxRegimeCode asignado en este fixture
    }

    [Fact]
    public async Task Rol_admin_recibe_assignedBranchCount_igual_al_total_sin_depender_de_CompanyUserBranch()
    {
        var company = Company.CreateManaged(TenantA, "1790012345001", "Empresa A", createdBy: UserId);
        var adminMembership = CompanyUserMembership.Create(company.Id, UserId, "Admin", null, UserId);

        _currentUser.Setup(u => u.IsAuthenticated).Returns(true);
        _currentUser.Setup(u => u.UserId).Returns(UserId);
        _currentTenant.Setup(t => t.TenantId).Returns(TenantA);
        _accessRepo
            .Setup(r =>
                r.GetActiveCompanyUserMembershipsForUserSystemAsync(
                    UserId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new[] { adminMembership });
        _companyRepo
            .Setup(r =>
                r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new[] { company });
        _branchRepo
            .Setup(r =>
                r.CountActiveByCompanyIdsAsync(
                    TenantA,
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new Dictionary<Guid, int> { [company.Id] = 3 });
        // Ninguna fila explícita de CompanyUserBranch para este Admin — igual debe ver las 3.
        _companyUserBranchRepo
            .Setup(r =>
                r.CountActiveByMembershipIdsAsync(
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new Dictionary<Guid, int>());

        var handler = BuildHandler();
        var result = await handler.Handle(new ListMyCompaniesQuery(), CancellationToken.None);

        var dto = result.Value!.Single();
        dto.TotalBranchCount.Should().Be(3);
        dto.AssignedBranchCount.Should().Be(3);
    }

    [Fact]
    public async Task Rol_no_admin_recibe_assignedBranchCount_desde_CompanyUserBranch_no_desde_el_total()
    {
        var company = Company.CreateManaged(TenantA, "1790012345001", "Empresa A", createdBy: UserId);
        var membership = CompanyUserMembership.Create(company.Id, UserId, "User", null, UserId);

        _currentUser.Setup(u => u.IsAuthenticated).Returns(true);
        _currentUser.Setup(u => u.UserId).Returns(UserId);
        _currentTenant.Setup(t => t.TenantId).Returns(TenantA);
        _accessRepo
            .Setup(r =>
                r.GetActiveCompanyUserMembershipsForUserSystemAsync(
                    UserId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new[] { membership });
        _companyRepo
            .Setup(r =>
                r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new[] { company });
        _branchRepo
            .Setup(r =>
                r.CountActiveByCompanyIdsAsync(
                    TenantA,
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new Dictionary<Guid, int> { [company.Id] = 5 });
        _companyUserBranchRepo
            .Setup(r =>
                r.CountActiveByMembershipIdsAsync(
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new Dictionary<Guid, int> { [membership.Id] = 1 });

        var handler = BuildHandler();
        var result = await handler.Handle(new ListMyCompaniesQuery(), CancellationToken.None);

        var dto = result.Value!.Single();
        dto.TotalBranchCount.Should().Be(5);
        dto.AssignedBranchCount.Should().Be(1);
    }
}
