using ERP.Application.Auth.UseCases.Reauthenticate;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Tenants.Entities;
using ERP.Domain.Tenants.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Auth;

/// <summary>
/// Fase 4: reautenticación tras bloqueo por inactividad (SessionLockOverlay). El foco de estos
/// tests es la identidad — la contraseña incorrecta no debe tocar el refresh token vigente, y el
/// userId/tenantId/companyId siempre vienen del refresh token ya validado, nunca del body.
/// </summary>
public sealed class ReauthenticateHandlerTests
{
    private static readonly Guid CreatedBy = Guid.NewGuid();
    private const string Password = "Sup3rSecret!";
    private const string PasswordHash = "hashed-password";
    private const string RawRefreshToken = "raw-refresh-token";

    private static IdentityUser NewUser(string username = "ana.perez") =>
        IdentityUser.Create(username, "Ana", "Perez", "ana@test.com", PasswordHash, CreatedBy);

    private static Tenant NewTenant() =>
        Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], CreatedBy);

    private static Company NewCompany(Guid tenantId) =>
        Company.CreateManaged(tenantId, "1790012345001", "Test S.A.", createdBy: CreatedBy);

    private sealed class Fixture
    {
        public Mock<IRefreshTokenService> RefreshTokenService { get; } = new();
        public Mock<IAccessRepository> AccessRepo { get; } = new();
        public Mock<ITenantRepository> TenantRepo { get; } = new();
        public Mock<ICompanyRepository> CompanyRepo { get; } = new();
        public Mock<IAccessTokenService> TokenService { get; } = new();
        public Mock<IPasswordHasher> PasswordHasher { get; } = new();

        public ReauthenticateHandler BuildHandler() =>
            new(
                RefreshTokenService.Object,
                AccessRepo.Object,
                TenantRepo.Object,
                CompanyRepo.Object,
                TokenService.Object,
                PasswordHasher.Object
            );
    }

    private static Fixture BuildValidSessionFixture(
        IdentityUser user,
        Tenant tenant,
        Company company,
        CompanyUserMembership membership
    )
    {
        var f = new Fixture();
        f.RefreshTokenService.Setup(s =>
                s.ValidateWithoutRotatingAsync(RawRefreshToken, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                RefreshTokenValidationResult.Identity(
                    user.Id,
                    tenant.Id,
                    company.Id,
                    RefreshUserType.Identity
                )
            );
        f.AccessRepo.Setup(r => r.GetUserByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        f.TenantRepo.Setup(r => r.GetByIdAsync(tenant.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        f.CompanyRepo.Setup(r =>
                r.GetByIdForTenantAsync(company.Id, tenant.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(company);
        f.AccessRepo.Setup(r =>
                r.GetCompanyUserMembershipAsync(company.Id, user.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(membership);
        return f;
    }

    [Fact]
    public async Task Reautenticacion_exitosa_emite_nuevo_access_token_y_refresh_token()
    {
        var user = NewUser();
        var tenant = NewTenant();
        var company = NewCompany(tenant.Id);
        var membership = CompanyUserMembership.Create(company.Id, user.Id, "Admin", null, CreatedBy);

        var f = BuildValidSessionFixture(user, tenant, company, membership);
        f.PasswordHasher.Setup(h => h.VerifyPassword(Password, PasswordHash)).Returns(true);
        f.TokenService.Setup(s => s.GenerateSessionToken(user, tenant.Id, membership.Role))
            .Returns("new-jwt");
        f.RefreshTokenService.Setup(s =>
                s.CreateAsync(
                    user.Id,
                    tenant.Id,
                    company.Id,
                    RefreshUserType.Identity,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(("new-refresh-token", DateTime.UtcNow.AddHours(8)));

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new ReauthenticateCommand(RawRefreshToken, Password),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Token.Should().Be("new-jwt");
        result.Value.RefreshToken.Should().Be("new-refresh-token");
        result.Value.CompanyId.Should().Be(company.Id);
        result.Value.UserId.Should().Be(user.Id);

        f.RefreshTokenService.Verify(
            s => s.RevokeAsync(RawRefreshToken, "Reautenticación", It.IsAny<CancellationToken>()),
            Times.Once
        );
        // Reautenticación = nueva sesión (como login), nunca una rotación silenciosa.
        f.RefreshTokenService.Verify(
            s => s.ValidateAndRotateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Contrasena_incorrecta_falla_y_no_toca_el_refresh_token_vigente()
    {
        var user = NewUser();
        var tenant = NewTenant();
        var company = NewCompany(tenant.Id);
        var membership = CompanyUserMembership.Create(company.Id, user.Id, "Admin", null, CreatedBy);

        var f = BuildValidSessionFixture(user, tenant, company, membership);
        f.PasswordHasher.Setup(h => h.VerifyPassword(Password, PasswordHash)).Returns(false);

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new ReauthenticateCommand(RawRefreshToken, Password),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("incorrecta");

        // La sesión vigente sigue viva: una contraseña mal tipeada no debe forzar re-login.
        f.RefreshTokenService.Verify(
            s => s.RevokeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        f.RefreshTokenService.Verify(
            s =>
                s.CreateAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task Refresh_token_invalido_o_revocado_falla_sin_verificar_contrasena()
    {
        var f = new Fixture();
        f.RefreshTokenService.Setup(s =>
                s.ValidateWithoutRotatingAsync(RawRefreshToken, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(RefreshTokenValidationResult.Fail("Refresh token revocado. Inicia sesión nuevamente."));

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new ReauthenticateCommand(RawRefreshToken, Password),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("revocado");
        f.PasswordHasher.Verify(
            h => h.VerifyPassword(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Sesion_absoluta_vencida_falla_sin_verificar_contrasena()
    {
        var f = new Fixture();
        f.RefreshTokenService.Setup(s =>
                s.ValidateWithoutRotatingAsync(RawRefreshToken, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(RefreshTokenValidationResult.Fail("Sesión expirada. Inicia sesión nuevamente."));

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new ReauthenticateCommand(RawRefreshToken, Password),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Sesión expirada");
        f.PasswordHasher.Verify(
            h => h.VerifyPassword(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Usuario_inactivo_no_puede_reautenticarse()
    {
        var user = NewUser();
        user.Deactivate(CreatedBy);
        var f = new Fixture();
        f.RefreshTokenService.Setup(s =>
                s.ValidateWithoutRotatingAsync(RawRefreshToken, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                RefreshTokenValidationResult.Identity(
                    user.Id,
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    RefreshUserType.Identity
                )
            );
        f.AccessRepo.Setup(r => r.GetUserByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new ReauthenticateCommand(RawRefreshToken, Password),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        f.PasswordHasher.Verify(
            h => h.VerifyPassword(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Membresia_inactiva_para_la_empresa_no_puede_reautenticarse()
    {
        var user = NewUser();
        var tenant = NewTenant();
        var company = NewCompany(tenant.Id);
        var membership = CompanyUserMembership.Create(company.Id, user.Id, "Admin", null, CreatedBy);
        membership.Deactivate(CreatedBy);

        var f = BuildValidSessionFixture(user, tenant, company, membership);
        f.PasswordHasher.Setup(h => h.VerifyPassword(Password, PasswordHash)).Returns(true);

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new ReauthenticateCommand(RawRefreshToken, Password),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        f.RefreshTokenService.Verify(
            s =>
                s.CreateAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task Tenant_inactivo_no_puede_reautenticarse()
    {
        var user = NewUser();
        var tenant = NewTenant();
        tenant.Deactivate(CreatedBy);
        var company = NewCompany(tenant.Id);
        var membership = CompanyUserMembership.Create(company.Id, user.Id, "Admin", null, CreatedBy);

        var f = BuildValidSessionFixture(user, tenant, company, membership);
        f.PasswordHasher.Setup(h => h.VerifyPassword(Password, PasswordHash)).Returns(true);

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new ReauthenticateCommand(RawRefreshToken, Password),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task La_identidad_viene_siempre_del_refresh_token_nunca_del_body()
    {
        // El command no tiene ningún campo de userId/username — solo el token y la contraseña.
        // Este test documenta esa garantía estructural: GetUserByIdAsync se llama exactamente
        // con el userId resuelto por ValidateWithoutRotatingAsync, sin ninguna otra fuente.
        var user = NewUser();
        var tenant = NewTenant();
        var company = NewCompany(tenant.Id);
        var membership = CompanyUserMembership.Create(company.Id, user.Id, "Admin", null, CreatedBy);
        var f = BuildValidSessionFixture(user, tenant, company, membership);
        f.PasswordHasher.Setup(h => h.VerifyPassword(Password, PasswordHash)).Returns(true);
        f.TokenService.Setup(s => s.GenerateSessionToken(user, tenant.Id, membership.Role))
            .Returns("jwt");
        f.RefreshTokenService.Setup(s =>
                s.CreateAsync(
                    user.Id,
                    tenant.Id,
                    company.Id,
                    RefreshUserType.Identity,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(("new-refresh", DateTime.UtcNow.AddHours(8)));

        var handler = f.BuildHandler();
        await handler.Handle(new ReauthenticateCommand(RawRefreshToken, Password), CancellationToken.None);

        f.AccessRepo.Verify(r => r.GetUserByIdAsync(user.Id, It.IsAny<CancellationToken>()), Times.Once);
        f.AccessRepo.Verify(
            r => r.GetUserByIdAsync(It.Is<Guid>(id => id != user.Id), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }
}
