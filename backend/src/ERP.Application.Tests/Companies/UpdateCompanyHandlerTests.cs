using ERP.Application.Access.Caching;
using ERP.Application.Common;
using ERP.Application.Modules.Companies;
using ERP.Application.Modules.Companies.UseCases.UpdateCompany;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Kernel.Permissions;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Companies;

/// <summary>
/// MEDIO cluster (auditoría multi-tenant) — UpdateCompanyHandler nunca debe poder mutar una
/// empresa fuera del tenant del usuario autenticado: (1) sin membresía en la empresa destino el
/// guard rechaza antes de tocar el repositorio, y (2) la lectura para edición usa
/// GetTrackedByIdForTenantAsync con el TenantId resuelto por el guard (nunca uno arbitrario),
/// por lo que una empresa de otro tenant nunca puede quedar "tracked" para guardarse.
/// </summary>
public sealed class UpdateCompanyHandlerTests
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly Mock<ICompanyAccessGuard> _accessGuard = new();
    private readonly Mock<ICompanyRepository> _companies = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<IAccessRepository> _access = new();
    private readonly Mock<IEffectivePermissionKeysProvider> _permissionKeys = new();

    private UpdateCompanyHandler BuildHandler() =>
        new(
            _accessGuard.Object,
            _companies.Object,
            _currentUser.Object,
            _access.Object,
            _permissionKeys.Object
        );

    // SEC-03: membership no Admin en la empresa destino; el permiso del perfil decide.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Sin_rol_Admin_exige_erp_companies_update_en_la_empresa_destino(bool granted)
    {
        var companyId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        _accessGuard
            .Setup(g => g.RequireMembershipAsync(companyId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<CompanyAccessContext>.Success(
                    new CompanyAccessContext(UserId, TenantA, companyId, "User", true, true)
                )
            );
        _access
            .Setup(a => a.GetCompanyUserMembershipAsync(companyId, UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CompanyUserMembership.Create(companyId, UserId, "User", profileId, UserId));
        _permissionKeys
            .Setup(p => p.GetAllowedKeysAsync(TenantA, companyId, UserId, profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(granted ? [SettingsPermissions.CompaniesUpdate] : [SettingsPermissions.CompaniesView]);
        _companies
            .Setup(c => c.GetTrackedByIdForTenantAsync(companyId, TenantA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Company.CreateManaged(TenantA, "1790012345001", "Original", createdBy: UserId));

        var result = await BuildHandler().Handle(
            new UpdateCompanyCommand(companyId, "Nuevo Nombre", null, true),
            CancellationToken.None
        );

        result.IsSuccess.Should().Be(granted);
        if (!granted)
            result.Code.Should().Be(ApiResponseCodes.Common.Forbidden);
        _companies.Verify(
            c => c.GetTrackedByIdForTenantAsync(companyId, TenantA, It.IsAny<CancellationToken>()),
            granted ? Times.Once() : Times.Never()
        );
    }

    [Fact]
    public async Task Sin_membresia_en_la_empresa_destino_el_guard_rechaza_y_nunca_se_lee_para_edicion()
    {
        var otherCompanyId = Guid.NewGuid();
        _accessGuard
            .Setup(g =>
                g.RequireMembershipAsync(otherCompanyId, false, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                Result<CompanyAccessContext>.Forbidden("No tiene acceso a esta empresa.")
            );

        var handler = BuildHandler();
        var result = await handler.Handle(
            new UpdateCompanyCommand(otherCompanyId, "Nuevo Nombre", null, true),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        _companies.Verify(
            c =>
                c.GetTrackedByIdForTenantAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
        _companies.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Lectura_para_edicion_siempre_usa_el_TenantId_resuelto_por_el_guard_no_uno_arbitrario()
    {
        var companyId = Guid.NewGuid();
        _accessGuard
            .Setup(g => g.RequireMembershipAsync(companyId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<CompanyAccessContext>.Success(
                    new CompanyAccessContext(UserId, TenantA, companyId, "Admin", true, true)
                )
            );
        // Simula el comportamiento real de un repo tenant-scoped: si el TenantId que llega no es
        // exactamente TenantA, no encuentra nada (fail-closed).
        _companies
            .Setup(c =>
                c.GetTrackedByIdForTenantAsync(companyId, TenantA, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                Company.CreateManaged(TenantA, "1790012345001", "Original", createdBy: UserId)
            );

        var handler = BuildHandler();
        var result = await handler.Handle(
            new UpdateCompanyCommand(companyId, "Nuevo Nombre", null, true),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        _companies.Verify(
            c => c.GetTrackedByIdForTenantAsync(companyId, TenantA, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }
}
