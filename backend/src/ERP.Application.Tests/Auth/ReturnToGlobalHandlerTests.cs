using ERP.Application.Auth.UseCases.ReturnToGlobal;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Kernel.Security;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Auth;

/// <summary>AdminGlobalCore Fase E — ReturnToGlobalHandler solo funciona si la sesión actual proviene de operate-company.</summary>
public sealed class ReturnToGlobalHandlerTests
{
    private static readonly Guid CreatedBy = Guid.NewGuid();

    private sealed class Fixture
    {
        public Mock<ICurrentOperatorContext> CurrentOperatorContext { get; } = new();
        public Mock<IAccessRepository> AccessRepo { get; } = new();
        public Mock<IAccessTokenService> TokenService { get; } = new();
        public Mock<IRefreshTokenService> RefreshTokenService { get; } = new();

        public ReturnToGlobalHandler BuildHandler() =>
            new(
                CurrentOperatorContext.Object,
                AccessRepo.Object,
                TokenService.Object,
                RefreshTokenService.Object
            );
    }

    [Fact]
    public async Task Return_exitoso_emite_token_global()
    {
        var f = new Fixture();
        var user = IdentityUser.Create(
            "global.admin",
            "Global",
            "Admin",
            "global@test.com",
            "hash",
            CreatedBy
        );
        var globalRole = GlobalUserRole.Create(user.Id, SecurityRoles.Admin, CreatedBy);

        f.CurrentOperatorContext.Setup(c => c.IsOperatorMode).Returns(true);
        f.CurrentOperatorContext.Setup(c => c.GlobalAdminUserId).Returns(user.Id);
        f.AccessRepo
            .Setup(r =>
                r.GetActiveGlobalUserRoleAsync(
                    user.Id,
                    SecurityRoles.Admin,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(globalRole);
        f.AccessRepo.Setup(r => r.GetUserByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        f.TokenService
            .Setup(s => s.GenerateSessionToken(user, Guid.Empty, SecurityRoles.Admin))
            .Returns("global-jwt");
        f.RefreshTokenService
            .Setup(s =>
                s.CreateAsync(
                    user.Id,
                    Guid.Empty,
                    null,
                    RefreshUserType.Identity,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(("raw-refresh-token", DateTime.UtcNow.AddDays(1)));

        var handler = f.BuildHandler();
        var result = await handler.Handle(new ReturnToGlobalCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Token.Should().Be("global-jwt");
        result.Value.TenantId.Should().Be(Guid.Empty);
        result.Value.CompanyId.Should().BeNull();
        result.Value.OperatorMode.Should().BeFalse();
    }

    [Fact]
    public async Task Return_sin_operator_mode_falla()
    {
        var f = new Fixture();
        f.CurrentOperatorContext.Setup(c => c.IsOperatorMode).Returns(false);

        var handler = f.BuildHandler();
        var result = await handler.Handle(new ReturnToGlobalCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        f.AccessRepo.Verify(
            r => r.GetActiveGlobalUserRoleAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()
            ),
            Times.Never
        );
    }

    [Fact]
    public async Task Return_con_rol_global_ya_inactivo_falla()
    {
        var f = new Fixture();
        var adminId = Guid.NewGuid();
        f.CurrentOperatorContext.Setup(c => c.IsOperatorMode).Returns(true);
        f.CurrentOperatorContext.Setup(c => c.GlobalAdminUserId).Returns(adminId);
        f.AccessRepo
            .Setup(r =>
                r.GetActiveGlobalUserRoleAsync(
                    adminId,
                    SecurityRoles.Admin,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((GlobalUserRole?)null);

        var handler = f.BuildHandler();
        var result = await handler.Handle(new ReturnToGlobalCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }
}
