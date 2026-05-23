using FluentAssertions;
using ERP.Application.Common;
using Moq;
using ERP.Application.Auth.UseCases.Login;
using ERP.Application.Common.Interfaces;
using ERP.Application.Subscriptions;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Subscribers.Entities;
using ERP.Domain.Subscribers.Interfaces;

namespace ERP.Application.Tests;

public class LoginHandlerTests
{
    [Fact]
    public async Task HandleAsync_returns_success_for_identity_user_with_single_active_membership()
    {
        var email = "test@company.local";
        var password = "Secret123!";
        var passwordHash = "hashed-password";
        var subscriberId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var role = "Admin";

        var identityUser = IdentityUser.Create("Test", "User", email, passwordHash, Guid.NewGuid());
        var membership = CompanyUserMembership.Create(companyId, identityUser.Id, role, null, Guid.NewGuid());
        var tenant = Subscriber.Create("Subscriber Name", "tenant-slug", Guid.NewGuid(), planCode: "starter");
        var company = Company.CreateFromSubscriber(subscriberId, "1999999999001", "Test Company", "Main St");
        company.Id = companyId;

        var tenantRepo = new Mock<ISubscriberRepository>(MockBehavior.Strict);
        var companyRepo = new Mock<ICompanyRepository>(MockBehavior.Strict);
        var accessRepo = new Mock<IAccessRepository>(MockBehavior.Strict);
        var accessTokenService = new Mock<IAccessTokenService>(MockBehavior.Strict);
        var passwordHasher = new Mock<IPasswordHasher>(MockBehavior.Strict);
        var companyProvisioning = new Mock<ICompanyProvisioningService>(MockBehavior.Strict);

        accessRepo.Setup(r => r.GetUserByEmailAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(identityUser);
        passwordHasher.Setup(h => h.VerifyPassword(password, passwordHash)).Returns(true);
        accessRepo.Setup(r => r.GetActiveCompanyUserMembershipsForUserSystemAsync(identityUser.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { membership });
        companyRepo.Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new[] { company });
        tenantRepo.Setup(r => r.GetByIdAsync(subscriberId, It.IsAny<CancellationToken>())).ReturnsAsync(tenant);
        companyProvisioning.Setup(s => s.EnsureDefaultCompanyAsync(tenant, It.IsAny<CancellationToken>())).ReturnsAsync(company);
        accessTokenService.Setup(s => s.GenerateSessionToken(identityUser, subscriberId, role, companyId)).Returns("session-token");

        var refreshTokenService = new Mock<IRefreshTokenService>();
        refreshTokenService
            .Setup(s => s.CreateAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(("fake-refresh-token", DateTime.UtcNow.AddDays(30)));

        var sessionModules = new Mock<ISessionModulesResolver>(MockBehavior.Strict);
        sessionModules
            .Setup(s => s.GetEnabledModuleKeysAsync(subscriberId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "inventory" });

        var deployment = new Mock<IDeploymentFeatureFlags>();
        deployment.Setup(d => d.IsPlatformPanelEnabled).Returns(true);

        var handler = new LoginHandler(
            tenantRepo.Object,
            companyRepo.Object,
            accessRepo.Object,
            accessTokenService.Object,
            passwordHasher.Object,
            refreshTokenService.Object,
            sessionModules.Object,
            companyProvisioning.Object,
            deployment.Object);

        var result = await handler.Handle(new LoginCommand(email, password), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Token.Should().Be("session-token");
        result.Value.SubscriberId.Should().Be(subscriberId);
        result.Value.CompanyId.Should().Be(companyId);
        result.Value.Role.Should().Be(role);
        result.Value.Email.Should().Be(email);
    }

    [Fact]
    public async Task HandleAsync_returns_platform_session_for_superadmin_via_unified_login()
    {
        var email = "superadmin@zh.local";
        var password = "Secret123!";
        var passwordHash = "hashed-password";

        var platformUser = IdentityUser.CreatePlatformOperator("Super", "Admin", email, passwordHash, Guid.NewGuid());

        var accessRepo = new Mock<IAccessRepository>(MockBehavior.Strict);
        var accessTokenService = new Mock<IAccessTokenService>(MockBehavior.Strict);
        var passwordHasher = new Mock<IPasswordHasher>(MockBehavior.Strict);
        var refreshTokenService = new Mock<IRefreshTokenService>();
        var deployment = new Mock<IDeploymentFeatureFlags>();

        accessRepo.Setup(r => r.GetUserByEmailAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(platformUser);
        passwordHasher.Setup(h => h.VerifyPassword(password, passwordHash)).Returns(true);
        deployment.Setup(d => d.IsPlatformPanelEnabled).Returns(true);
        accessTokenService.Setup(s => s.GeneratePlatformSessionToken(platformUser)).Returns("platform-token");
        refreshTokenService
            .Setup(s => s.CreateAsync(platformUser.Id, Guid.Empty, null, RefreshUserType.Platform, It.IsAny<CancellationToken>()))
            .ReturnsAsync(("refresh-token", DateTime.UtcNow.AddDays(30)));

        var handler = new LoginHandler(
            Mock.Of<ISubscriberRepository>(),
            Mock.Of<ICompanyRepository>(),
            accessRepo.Object,
            accessTokenService.Object,
            passwordHasher.Object,
            refreshTokenService.Object,
            Mock.Of<ISessionModulesResolver>(),
            Mock.Of<ICompanyProvisioningService>(),
            deployment.Object);

        var result = await handler.Handle(new LoginCommand(email, password), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Token.Should().Be("platform-token");
        result.Value.Role.Should().Be(PlatformAuthConstants.JwtPlatformOperatorRole);
        result.Value.SubscriberId.Should().Be(Guid.Empty);
        result.Value.UserType.Should().Be("Platform");
    }
}
