using ERP.Application.Auth.UseCases.OperateCompany;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Branches.Interfaces;
using ERP.Domain.Kernel.Security;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Tenants.Entities;
using ERP.Domain.Tenants.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Auth;

/// <summary>
/// AdminGlobalCore Fase D — OperateCompanyHandler emite el token operativo que permite a un
/// admin global entrar a operar una empresa concreta sin usar su token global directo contra
/// endpoints operativos. Mismo patrón de test que SwitchCompanyHandlerTests.
/// </summary>
public sealed class OperateCompanyHandlerTests
{
    private static readonly Guid CreatedBy = Guid.NewGuid();

    private sealed class Fixture
    {
        public Mock<IAccessRepository> AccessRepo { get; } = new();
        public Mock<ICompanyRepository> CompanyRepo { get; } = new();
        public Mock<ITenantRepository> TenantRepo { get; } = new();
        public Mock<IBranchRepository> BranchRepo { get; } = new();
        public Mock<IAccessTokenService> TokenService { get; } = new();
        public Mock<IRefreshTokenService> RefreshTokenService { get; } = new();
        public Mock<ICurrentUser> CurrentUser { get; } = new();
        public Mock<ICurrentTenant> CurrentTenant { get; } = new();

        public OperateCompanyHandler BuildHandler() =>
            new(
                AccessRepo.Object,
                CompanyRepo.Object,
                TenantRepo.Object,
                BranchRepo.Object,
                TokenService.Object,
                RefreshTokenService.Object,
                CurrentUser.Object,
                CurrentTenant.Object
            );
    }

    private static Branch NewMainBranch(Guid tenantId, Guid companyId) =>
        Branch.Create(
            tenantId,
            "Matriz",
            "Av. Principal 123",
            "001",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            isMainBranch: true,
            CreatedBy,
            companyId: companyId
        );

    private static (Fixture f, Tenant tenant, Company company, IdentityUser user) BuildValidCaller()
    {
        var f = new Fixture();
        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], CreatedBy);
        var company = Company.CreateManaged(
            tenant.Id,
            "1790012345001",
            "Test S.A.",
            createdBy: CreatedBy
        );
        var user = IdentityUser.Create(
            "global.admin",
            "Global",
            "Admin",
            "global@test.com",
            "hash",
            CreatedBy
        );
        var globalRole = GlobalUserRole.Create(user.Id, SecurityRoles.Admin, CreatedBy);

        f.CurrentUser.Setup(c => c.IsAuthenticated).Returns(true);
        f.CurrentUser.Setup(c => c.UserId).Returns(user.Id);
        f.CurrentTenant.Setup(c => c.TenantId).Returns(Guid.Empty);
        f.AccessRepo.Setup(r => r.GetUserByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        f.AccessRepo
            .Setup(r =>
                r.GetActiveGlobalUserRoleAsync(
                    user.Id,
                    SecurityRoles.Admin,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(globalRole);
        f.CompanyRepo
            .Setup(r => r.GetTrackedByIdForIntegrationAsync(company.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);
        f.TenantRepo.Setup(r => r.GetByIdAsync(tenant.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        return (f, tenant, company, user);
    }

    [Fact]
    public async Task OperateCompany_exitoso_emite_token_scoped_con_claims_de_operador()
    {
        var (f, tenant, company, user) = BuildValidCaller();
        var branch = NewMainBranch(tenant.Id, company.Id);
        f.BranchRepo
            .Setup(r =>
                r.GetByCompanyAsync(tenant.Id, company.Id, true, null, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new[] { branch });
        f.TokenService
            .Setup(s =>
                s.GenerateSessionToken(
                    user,
                    tenant.Id,
                    SecurityRoles.Admin,
                    It.IsAny<IEnumerable<System.Security.Claims.Claim>>()
                )
            )
            .Returns("operative-jwt");
        f.RefreshTokenService
            .Setup(s =>
                s.CreateAsync(
                    user.Id,
                    tenant.Id,
                    company.Id,
                    RefreshUserType.Identity,
                    It.IsAny<CancellationToken>(),
                    true,
                    user.Id
                )
            )
            .ReturnsAsync(("raw-refresh-token", DateTime.UtcNow.AddDays(1)));

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new OperateCompanyCommand(company.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.Token.Should().Be("operative-jwt");
        result.Value.TenantId.Should().Be(tenant.Id);
        result.Value.CompanyId.Should().Be(company.Id);
        result.Value.OperatorMode.Should().BeTrue();
        result.Value.GlobalAdminUserId.Should().Be(user.Id);
        // ERP-CORE-GLOBAL-ADMIN-BRANCH-ACCESS-01: el refresh token de esta sesión debe quedar
        // marcado como sesión de operador — es lo único que sobrevive un refresh/F5 (el access
        // token con operator_mode/global_admin_user_id nunca se persiste), y sin este flag
        // RefreshTokenHandler no puede reemitir esos claims al rotar.
        f.RefreshTokenService.Verify(
            s =>
                s.CreateAsync(
                    user.Id,
                    tenant.Id,
                    company.Id,
                    RefreshUserType.Identity,
                    It.IsAny<CancellationToken>(),
                    true,
                    user.Id
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task OperateCompany_con_token_no_global_falla()
    {
        var (f, _, company, _) = BuildValidCaller();
        f.CurrentTenant.Setup(c => c.TenantId).Returns(Guid.NewGuid());

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new OperateCompanyCommand(company.Id),
            CancellationToken.None
        );

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
    public async Task OperateCompany_sin_rol_global_activo_falla()
    {
        var (f, _, company, user) = BuildValidCaller();
        f.AccessRepo
            .Setup(r =>
                r.GetActiveGlobalUserRoleAsync(
                    user.Id,
                    SecurityRoles.Admin,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((GlobalUserRole?)null);

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new OperateCompanyCommand(company.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("administrador global");
    }

    [Fact]
    public async Task OperateCompany_empresa_inactiva_falla()
    {
        var (f, _, company, _) = BuildValidCaller();
        company.UpdateAdminIdentity(company.LegalName, company.TradeName, isActive: false, CreatedBy);

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new OperateCompanyCommand(company.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task OperateCompany_tenant_destino_inactivo_falla()
    {
        var (f, tenant, company, _) = BuildValidCaller();
        tenant.Deactivate(CreatedBy);

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new OperateCompanyCommand(company.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task OperateCompany_sin_sucursal_principal_resoluble_falla()
    {
        var (f, tenant, company, _) = BuildValidCaller();
        f.BranchRepo
            .Setup(r =>
                r.GetByCompanyAsync(tenant.Id, company.Id, true, null, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(Array.Empty<Branch>());

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new OperateCompanyCommand(company.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
    }
}
