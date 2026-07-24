using ERP.Application.Access.UseCases.GetCompanyUserMembershipsAdmin;
using ERP.Application.Common;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Access;

/// <summary>
/// Fase I-C: GetCompanyUserMembershipsAdminHandler es una proyección de solo lectura que junta
/// CompanyUserMembership + IdentityUser + AccessProfile, todos ya expuestos individualmente por
/// otros endpoints admin — bloqueo real detectado al construir /access/users (ningún endpoint
/// existente lista memberships con inactivas + ProfileName).
/// </summary>
public sealed class GetCompanyUserMembershipsAdminHandlerTests
{
    private static readonly Guid CreatedBy = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CurrentCompanyId = Guid.NewGuid();

    private static IdentityUser NewUser(string first, string last, string email) =>
        IdentityUser.Create(email.Split('@')[0], first, last, email, "hash", CreatedBy);

    private static AccessProfile NewProfile(string name) =>
        AccessProfile.Create(TenantId, name, null, CreatedBy);

    private sealed class CurrentCompanyStub : ICurrentCompany
    {
        public CurrentCompanyStub(bool hasContext = true) => HasCompanyContext = hasContext;
        public Guid CompanyId => CurrentCompanyId;
        public bool IsAuthenticated => true;
        public bool HasCompanyContext { get; }
    }

    private sealed class CurrentTenantStub : ICurrentTenant
    {
        public CurrentTenantStub(Guid tenantId) => TenantId = tenantId;
        public Guid TenantId { get; }
        public string? Slug => null;
    }

    private sealed class Fixture
    {
        public Mock<IAccessRepository> AccessRepo { get; } = new();

        public GetCompanyUserMembershipsAdminHandler BuildHandler(bool hasCompanyContext = true) => new(
            AccessRepo.Object, new CurrentCompanyStub(hasCompanyContext), new CurrentTenantStub(TenantId));
    }

    [Fact]
    public async Task Devuelve_memberships_de_la_empresa_activa_con_usuario_y_nombre_de_perfil_resueltos()
    {
        var profile = NewProfile("Ventas");
        var user = NewUser("Ana", "Perez", "ana@test.com");
        var membership = CompanyUserMembership.Create(CurrentCompanyId, user.Id, "User", profile.Id, CreatedBy);

        var f = new Fixture();
        f.AccessRepo.Setup(r => r.GetCompanyUserMembershipsByCompanyAsync(CurrentCompanyId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { membership });
        f.AccessRepo.Setup(r => r.GetUsersByIdsAsync(It.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(user.Id)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { user });
        f.AccessRepo.Setup(r => r.GetProfilesByTenantAsync(TenantId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { profile });

        var handler = f.BuildHandler();
        var result = await handler.Handle(new GetCompanyUserMembershipsAdminQuery(OnlyActive: false), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        var dto = result.Value![0];
        dto.CompanyUserId.Should().Be(membership.Id);
        dto.IdentityUserId.Should().Be(user.Id);
        dto.FullName.Should().Be("Ana Perez");
        dto.Email.Should().Be("ana@test.com");
        dto.Role.Should().Be("User");
        dto.IsActive.Should().BeTrue();
        dto.ProfileId.Should().Be(profile.Id);
        dto.ProfileName.Should().Be("Ventas");
    }

    [Fact]
    public async Task Membership_sin_perfil_asignado_devuelve_ProfileName_null()
    {
        var user = NewUser("Ana", "Perez", "ana@test.com");
        var membership = CompanyUserMembership.Create(CurrentCompanyId, user.Id, "User", null, CreatedBy);

        var f = new Fixture();
        f.AccessRepo.Setup(r => r.GetCompanyUserMembershipsByCompanyAsync(CurrentCompanyId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { membership });
        f.AccessRepo.Setup(r => r.GetUsersByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { user });
        f.AccessRepo.Setup(r => r.GetProfilesByTenantAsync(TenantId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AccessProfile>());

        var handler = f.BuildHandler();
        var result = await handler.Handle(new GetCompanyUserMembershipsAdminQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value![0].ProfileId.Should().BeNull();
        result.Value[0].ProfileName.Should().BeNull();
    }

    [Fact]
    public async Task Pasa_OnlyActive_al_repositorio_tal_cual_se_recibe()
    {
        var f = new Fixture();
        f.AccessRepo.Setup(r => r.GetCompanyUserMembershipsByCompanyAsync(CurrentCompanyId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CompanyUserMembership>());

        var handler = f.BuildHandler();
        var result = await handler.Handle(new GetCompanyUserMembershipsAdminQuery(OnlyActive: true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
        f.AccessRepo.Verify(r => r.GetCompanyUserMembershipsByCompanyAsync(CurrentCompanyId, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Sin_empresa_activa_devuelve_Forbidden()
    {
        var f = new Fixture();
        var handler = f.BuildHandler(hasCompanyContext: false);

        var result = await handler.Handle(new GetCompanyUserMembershipsAdminQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.Forbidden);
        f.AccessRepo.Verify(r => r.GetCompanyUserMembershipsByCompanyAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
