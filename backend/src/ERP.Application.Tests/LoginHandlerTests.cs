using FluentAssertions;
using Moq;
using ERP.Application.Auth.UseCases.Login;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Auth.Entities;
using ERP.Domain.Auth.Interfaces;
using ERP.Domain.Tenants.Entities;
using ERP.Domain.Tenants.Interfaces;

namespace ERP.Application.Tests;

public class LoginHandlerTests
{
    [Fact]
    public async Task HandleAsync_returns_success_for_identity_user_with_single_active_membership()
    {
        var email = "test@company.local";
        var password = "Secret123!";
        var passwordHash = "hashed-password";
        var tenantId = Guid.NewGuid();
        var role = "Admin";

        var identityUser = IdentityUser.Create("Test", "User", email, passwordHash, Guid.NewGuid());
        var membership = Membership.Create(tenantId, identityUser.Id, role, null, Guid.NewGuid());
        var tenant = Tenant.Create("Tenant Name", "tenant-slug", Guid.NewGuid(), planCode: "starter", enabledModuleKeys: new[] { "inventario" });

        var userRepo = new Mock<IUserRepository>(MockBehavior.Strict);
        var tenantRepo = new Mock<ITenantRepository>(MockBehavior.Strict);
        var jwtService = new Mock<IJwtService>(MockBehavior.Strict);
        var deployment = new Mock<IDeploymentFeatureFlags>(MockBehavior.Strict);
        var accessRepo = new Mock<IAccessRepository>(MockBehavior.Strict);
        var accessTokenService = new Mock<IAccessTokenService>(MockBehavior.Strict);
        var passwordHasher = new Mock<IPasswordHasher>(MockBehavior.Strict);

        deployment.SetupGet(d => d.IsSuperAdminPanelEnabled).Returns(true);
        userRepo.Setup(r => r.GetSingleSuperAdminByEmailAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        accessRepo.Setup(r => r.GetUserByEmailAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(identityUser);
        passwordHasher.Setup(h => h.VerifyPassword(password, passwordHash)).Returns(true);
        accessRepo.Setup(r => r.GetActiveMembershipsForUserSystemAsync(identityUser.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { membership });
        tenantRepo.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>())).ReturnsAsync(tenant);
        accessTokenService.Setup(s => s.GenerateSessionToken(identityUser, tenantId, role)).Returns("session-token");

        var refreshTokenService = new Mock<IRefreshTokenService>();
        refreshTokenService
            .Setup(s => s.CreateAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(("fake-refresh-token", DateTime.UtcNow.AddDays(30)));

        var handler = new LoginHandler(
            userRepo.Object,
            tenantRepo.Object,
            jwtService.Object,
            deployment.Object,
            accessRepo.Object,
            accessTokenService.Object,
            passwordHasher.Object,
            refreshTokenService.Object);

        var result = await handler.HandleAsync(new LoginCommand(email, password));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Token.Should().Be("session-token");
        result.Value.TenantId.Should().Be(tenantId);
        result.Value.Role.Should().Be(role);
        result.Value.Email.Should().Be(email);
    }
}
