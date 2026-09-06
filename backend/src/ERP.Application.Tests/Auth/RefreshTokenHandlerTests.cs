using System.Security.Claims;
using ERP.Application.Auth.UseCases.RefreshToken;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Kernel.Security;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Tenants.Entities;
using ERP.Domain.Tenants.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Auth;

/// <summary>
/// ERP-CORE-GLOBAL-ADMIN-BRANCH-ACCESS-01 — regresión del bug real encontrado en revisión
/// manual: el access token que OperateCompanyHandler emite con
/// operator_mode/global_admin_user_id NUNCA se persiste (el navegador solo conserva la cookie
/// httpOnly del refresh token), así que cada refresh/F5 reconstruía el access token desde cero
/// a partir de la CompanyUserMembership real del usuario — perdiendo esos claims para siempre y
/// degradando silenciosamente la sesión de "admin global operando" a "admin de empresa normal".
/// Estos tests cubren que RefreshTokenHandler ahora preserva y revalida ese contexto usando
/// RefreshToken.IsOperatorSession/GlobalAdminUserId (ver RefreshTokenServiceTests para la
/// persistencia/rotación de esos campos).
/// </summary>
public sealed class RefreshTokenHandlerTests
{
    private static readonly Guid CreatedBy = Guid.NewGuid();

    private sealed class Fixture
    {
        public Mock<IRefreshTokenService> RefreshTokenService { get; } = new();
        public Mock<IAccessRepository> AccessRepo { get; } = new();
        public Mock<ITenantRepository> TenantRepo { get; } = new();
        public Mock<IAccessTokenService> TokenService { get; } = new();
        public Mock<ICompanyRepository> CompanyRepo { get; } = new();

        public RefreshTokenHandler BuildHandler() =>
            new(
                RefreshTokenService.Object,
                AccessRepo.Object,
                TenantRepo.Object,
                TokenService.Object,
                CompanyRepo.Object
            );
    }

    private static (Fixture f, Tenant tenant, Company company, IdentityUser user) BuildBaseContext()
    {
        var f = new Fixture();
        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], CreatedBy);
        var company = Company.CreateManaged(
            tenant.Id,
            "1790012345001",
            "Empresa A S.A.",
            createdBy: CreatedBy
        );
        var user = IdentityUser.Create("sadmin", "Super", "Admin", "sadmin@test.com", "hash", CreatedBy);

        f.AccessRepo.Setup(a => a.GetUserByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        f.TenantRepo.Setup(t => t.GetByIdAsync(tenant.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        f.CompanyRepo
            .Setup(c => c.GetByIdForTenantAsync(company.Id, tenant.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);

        return (f, tenant, company, user);
    }

    private static RefreshTokenValidationResult OperatorSession(
        Guid userId,
        Guid tenantId,
        Guid companyId
    ) =>
        RefreshTokenValidationResult.Ok(
            userId,
            tenantId,
            companyId,
            RefreshUserType.Identity,
            "new-raw-refresh-token",
            DateTime.UtcNow.AddDays(1),
            isOperatorSession: true,
            globalAdminUserId: userId
        );

    private static RefreshTokenValidationResult NormalSession(
        Guid userId,
        Guid tenantId,
        Guid companyId
    ) =>
        RefreshTokenValidationResult.Ok(
            userId,
            tenantId,
            companyId,
            RefreshUserType.Identity,
            "new-raw-refresh-token",
            DateTime.UtcNow.AddDays(1),
            isOperatorSession: false,
            globalAdminUserId: null
        );

    [Fact]
    public async Task Refresh_de_sesion_de_operador_con_GlobalUserRole_activo_reemite_operator_mode_y_global_admin_user_id()
    {
        var (f, tenant, company, user) = BuildBaseContext();
        var globalRole = GlobalUserRole.Create(user.Id, SecurityRoles.Admin, CreatedBy);

        f.RefreshTokenService
            .Setup(s => s.ValidateAndRotateAsync("raw-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperatorSession(user.Id, tenant.Id, company.Id));
        f.AccessRepo
            .Setup(a =>
                a.GetActiveGlobalUserRoleAsync(user.Id, SecurityRoles.Admin, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(globalRole);
        f.TokenService
            .Setup(s =>
                s.GenerateSessionToken(
                    user,
                    tenant.Id,
                    SecurityRoles.Admin,
                    It.Is<IEnumerable<Claim>>(claims =>
                        claims.Any(c => c.Type == "operator_mode" && c.Value == "true")
                        && claims.Any(c =>
                            c.Type == "global_admin_user_id" && c.Value == user.Id.ToString()
                        )
                    )
                )
            )
            .Returns("operator-jwt-renewed");

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new RefreshTokenCommand("raw-token"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.Token.Should().Be("operator-jwt-renewed");
        result.Value.OperatorMode.Should().BeTrue();
        result.Value.GlobalAdminUserId.Should().Be(user.Id);
        result.Value.CompanyId.Should().Be(company.Id);
        // Nunca debe derivar el rol de una CompanyUserMembership para una sesión de operador —
        // el admin global puede no tener ninguna, o tener una restringida que sería irrelevante.
        f.AccessRepo.Verify(
            a =>
                a.GetCompanyUserMembershipAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task Refresh_de_sesion_normal_no_agrega_operator_mode()
    {
        var (f, tenant, company, user) = BuildBaseContext();
        var membership = CompanyUserMembership.Create(company.Id, user.Id, "Admin", null, CreatedBy);

        f.RefreshTokenService
            .Setup(s => s.ValidateAndRotateAsync("raw-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(NormalSession(user.Id, tenant.Id, company.Id));
        f.AccessRepo
            .Setup(a =>
                a.GetCompanyUserMembershipAsync(company.Id, user.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(membership);
        f.TokenService
            .Setup(s => s.GenerateSessionToken(user, tenant.Id, membership.Role))
            .Returns("normal-jwt-renewed");

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new RefreshTokenCommand("raw-token"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.Token.Should().Be("normal-jwt-renewed");
        result.Value.OperatorMode.Should().BeFalse();
        result.Value.GlobalAdminUserId.Should().BeNull();
        // Nunca debe consultar GlobalUserRole para una sesión que no vino de operate-company.
        f.AccessRepo.Verify(
            a =>
                a.GetActiveGlobalUserRoleAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    /// <summary>
    /// Regla explícita del fix: si el GlobalUserRole ya no está activo al momento del refresh
    /// (revocado después de operate-company), no se falla — se degrada a la resolución normal
    /// por CompanyUserMembership, exactamente como si nunca hubiera sido una sesión de operador.
    /// Si esa membership también autoriza, el refresh igual tiene éxito, solo que sin claims de
    /// operador.
    /// </summary>
    [Fact]
    public async Task Refresh_de_sesion_de_operador_con_GlobalUserRole_revocado_degrada_a_membership_normal()
    {
        var (f, tenant, company, user) = BuildBaseContext();
        var membership = CompanyUserMembership.Create(company.Id, user.Id, "Admin", null, CreatedBy);

        f.RefreshTokenService
            .Setup(s => s.ValidateAndRotateAsync("raw-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperatorSession(user.Id, tenant.Id, company.Id));
        f.AccessRepo
            .Setup(a =>
                a.GetActiveGlobalUserRoleAsync(user.Id, SecurityRoles.Admin, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((GlobalUserRole?)null);
        f.AccessRepo
            .Setup(a =>
                a.GetCompanyUserMembershipAsync(company.Id, user.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(membership);
        f.TokenService
            .Setup(s => s.GenerateSessionToken(user, tenant.Id, membership.Role))
            .Returns("degraded-jwt");

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new RefreshTokenCommand("raw-token"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.Token.Should().Be("degraded-jwt");
        result.Value.OperatorMode.Should().BeFalse();
        result.Value.GlobalAdminUserId.Should().BeNull();
    }

    /// <summary>
    /// Si el GlobalUserRole fue revocado Y tampoco hay CompanyUserMembership (o no está activa),
    /// el refresh falla con el mismo mensaje que ya existía para ese caso — no se relaja nada.
    /// </summary>
    [Fact]
    public async Task Refresh_de_sesion_de_operador_con_GlobalUserRole_revocado_y_sin_membership_falla()
    {
        var (f, tenant, company, user) = BuildBaseContext();

        f.RefreshTokenService
            .Setup(s => s.ValidateAndRotateAsync("raw-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperatorSession(user.Id, tenant.Id, company.Id));
        f.AccessRepo
            .Setup(a =>
                a.GetActiveGlobalUserRoleAsync(user.Id, SecurityRoles.Admin, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((GlobalUserRole?)null);
        f.AccessRepo
            .Setup(a =>
                a.GetCompanyUserMembershipAsync(company.Id, user.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((CompanyUserMembership?)null);

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new RefreshTokenCommand("raw-token"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Membresía no activa para la empresa.");
    }
}
