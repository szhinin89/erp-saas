using FluentAssertions;
using Moq;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Platform.Auth.UseCases.PlatformLogin;
using ERP.Application.Subscriptions;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;

namespace ERP.Application.Tests;

public class PlatformLoginHandlerTests
{
    [Fact]
    public async Task Handle_returns_success_for_platform_superadmin()
    {
        const string email = "super@erp.local";
        const string password = "Secret123!";
        const string hash = "hashed";

        var user = IdentityUser.CreatePlatformSuperAdmin("Super", "Admin", email, hash, Guid.NewGuid());

        var accessRepo = new Mock<IAccessRepository>(MockBehavior.Strict);
        var tokenService = new Mock<IAccessTokenService>(MockBehavior.Strict);
        var deployment = new Mock<IDeploymentFeatureFlags>(MockBehavior.Strict);
        var passwordHasher = new Mock<IPasswordHasher>(MockBehavior.Strict);
        var refresh = new Mock<IRefreshTokenService>(MockBehavior.Strict);

        deployment.SetupGet(d => d.IsSuperAdminPanelEnabled).Returns(true);
        accessRepo.Setup(r => r.GetPlatformSuperAdminByEmailAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        passwordHasher.Setup(h => h.VerifyPassword(password, hash)).Returns(true);
        tokenService.Setup(t => t.GeneratePlatformSessionToken(user)).Returns("platform-jwt");
        refresh.Setup(r => r.CreateAsync(user.Id, Guid.Empty, null, RefreshUserType.Platform, It.IsAny<CancellationToken>()))
            .ReturnsAsync(("refresh", DateTime.UtcNow.AddDays(30)));

        var handler = new PlatformLoginHandler(
            accessRepo.Object,
            tokenService.Object,
            deployment.Object,
            passwordHasher.Object,
            refresh.Object);

        var result = await handler.Handle(new PlatformLoginCommand(email, password), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Token.Should().Be("platform-jwt");
        result.Value.UserType.Should().Be("Platform");
        result.Value.PlatformRole.Should().Be("SuperAdmin");
        result.Value.EnabledModules.Should().BeEquivalentTo(SubscriberSubscriptionCatalog.AllModuleKeys);
    }
}
