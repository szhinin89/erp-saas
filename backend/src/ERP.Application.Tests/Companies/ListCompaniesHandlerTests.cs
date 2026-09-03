using ERP.Application.Common;
using ERP.Application.Modules.Companies;
using ERP.Application.Modules.Companies.UseCases.ListCompanies;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Companies;

/// <summary>
/// MEDIO cluster (auditoría multi-tenant) — ListCompaniesHandler solo puede listar empresas que
/// (1) el usuario tiene como membresía activa y (2) el repositorio confirma que pertenecen al
/// TenantId activo (GetByIdsForManagementAsync recibe el TenantId del guard, nunca uno arbitrario
/// ni "todas las empresas del sistema").
/// </summary>
public sealed class ListCompaniesHandlerTests
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly Mock<ICompanyAccessGuard> _accessGuard = new();
    private readonly Mock<IAccessRepository> _access = new();
    private readonly Mock<ICompanyRepository> _companies = new();
    private readonly Mock<ICurrentUser> _currentUser = new();

    private ListCompaniesHandler BuildHandler() =>
        new(_accessGuard.Object, _access.Object, _companies.Object, _currentUser.Object);

    [Fact]
    public async Task Sin_tenant_activo_el_guard_rechaza_y_nunca_se_consultan_membresias()
    {
        _accessGuard
            .Setup(g => g.RequireActiveTenantAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Guid>.Failure("Sin tenant activo."));

        var handler = BuildHandler();
        var result = await handler.Handle(new ListCompaniesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        _access.Verify(
            a =>
                a.GetActiveCompanyUserMembershipsForUserSystemAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task Solo_las_empresas_con_membresia_activa_se_piden_al_repo_con_el_TenantId_del_guard()
    {
        var companyId = Guid.NewGuid();
        _currentUser.Setup(u => u.UserId).Returns(UserId);
        _accessGuard
            .Setup(g => g.RequireActiveTenantAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Guid>.Success(TenantA));
        _access
            .Setup(a =>
                a.GetActiveCompanyUserMembershipsForUserSystemAsync(
                    UserId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new[] { CompanyUserMembership.Create(companyId, UserId, "Admin", null, UserId) }
            );

        IReadOnlyCollection<Guid>? requestedIds = null;
        Guid? requestedTenant = null;
        _companies
            .Setup(c =>
                c.GetByIdsForManagementAsync(
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<IReadOnlyCollection<Guid>, Guid, CancellationToken>(
                (ids, tenantId, _) =>
                {
                    requestedIds = ids;
                    requestedTenant = tenantId;
                }
            )
            .ReturnsAsync(
                new[]
                {
                    Company.CreateManaged(TenantA, "1790012345001", "Empresa A", createdBy: UserId),
                }
            );

        var handler = BuildHandler();
        var result = await handler.Handle(new ListCompaniesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        requestedTenant.Should().Be(TenantA);
        requestedIds.Should().BeEquivalentTo(new[] { companyId });
    }

    [Fact]
    public async Task Sin_membresias_activas_devuelve_lista_vacia_sin_consultar_companias()
    {
        _currentUser.Setup(u => u.UserId).Returns(UserId);
        _accessGuard
            .Setup(g => g.RequireActiveTenantAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Guid>.Success(TenantA));
        _access
            .Setup(a =>
                a.GetActiveCompanyUserMembershipsForUserSystemAsync(
                    UserId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Array.Empty<CompanyUserMembership>());

        var handler = BuildHandler();
        var result = await handler.Handle(new ListCompaniesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
        _companies.Verify(
            c =>
                c.GetByIdsForManagementAsync(
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }
}
