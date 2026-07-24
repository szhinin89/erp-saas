using ERP.Application.Access.DTOs;
using ERP.Application.Access.UseCases.CreateAuthenticatedSession;
using ERP.Application.Access.UseCases.GetCompanyUserPreferences;
using ERP.Application.Access.UseCases.UpdateCompanyUserPreferences;
using ERP.Application.Auth.DTOs;
using ERP.Application.Auth.UseCases.SwitchCompany;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Enums;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Branches.Interfaces;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Tenants.Entities;
using ERP.Domain.Tenants.Interfaces;
using FluentAssertions;
using MediatR;
using Moq;

namespace ERP.Application.Tests.Auth;

/// <summary>
/// Fase 7: primeros tests de SwitchCompanyHandler (no existía cobertura previa). SwitchCompany
/// ya emitía un RefreshToken nuevo antes de esta fase (no solo reescribía claims) — por eso
/// también se integra con CreateAuthenticatedSessionCommand, con la misma resolución interina
/// de sucursal principal que LoginHandler.
/// </summary>
public sealed class SwitchCompanyHandlerTests
{
    private static readonly Guid CreatedBy = Guid.NewGuid();

    private sealed class Fixture
    {
        public Mock<IAccessRepository> AccessRepo { get; } = new();
        public Mock<ICompanyRepository> CompanyRepo { get; } = new();
        public Mock<IAccessTokenService> TokenService { get; } = new();
        public Mock<ICurrentUser> CurrentUser { get; } = new();
        public Mock<ICurrentTenant> CurrentTenant { get; } = new();
        public Mock<IRefreshTokenService> RefreshTokenService { get; } = new();
        public Mock<ITenantRepository> TenantRepo { get; } = new();
        public Mock<IBranchRepository> BranchRepo { get; } = new();
        public Mock<IMediator> Mediator { get; } = new();

        public SwitchCompanyHandler BuildHandler() => new(
            AccessRepo.Object, CompanyRepo.Object, TokenService.Object, CurrentUser.Object,
            CurrentTenant.Object, RefreshTokenService.Object, TenantRepo.Object,
            BranchRepo.Object, Mediator.Object);
    }

    private static Branch NewMainBranch(Guid tenantId, Guid companyId) => Branch.Create(
        tenantId, "Matriz", "Av. Principal 123", "001",
        null, null, null, null, null, null, null, null, null, null, null,
        null, null, null, null, null, null, null, null, isMainBranch: true, CreatedBy,
        companyId: companyId);

    private static (Fixture f, Tenant tenant, Company company, IdentityUser user, CompanyUserMembership membership) BuildValidSwitch()
    {
        var f = new Fixture();
        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], CreatedBy);
        var company = Company.CreateManaged(tenant.Id, "1790012345001", "Test S.A.", createdBy: CreatedBy);
        var user = IdentityUser.Create("ana.perez", "Ana", "Perez", "ana@test.com", "hash", CreatedBy);
        var membership = CompanyUserMembership.Create(company.Id, user.Id, "Admin", null, CreatedBy);

        f.CurrentUser.Setup(c => c.IsAuthenticated).Returns(true);
        f.CurrentUser.Setup(c => c.UserId).Returns(user.Id);
        f.CurrentTenant.Setup(c => c.TenantId).Returns(tenant.Id);
        f.CompanyRepo.Setup(r => r.GetByIdForTenantAsync(company.Id, tenant.Id, It.IsAny<CancellationToken>())).ReturnsAsync(company);
        f.AccessRepo.Setup(r => r.GetUserByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        f.AccessRepo.Setup(r => r.GetCompanyUserMembershipAsync(company.Id, user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(membership);
        f.TokenService.Setup(s => s.GenerateSessionToken(user, tenant.Id, membership.Role)).Returns("jwt-token");
        f.TenantRepo.Setup(r => r.GetByIdAsync(tenant.Id, It.IsAny<CancellationToken>())).ReturnsAsync(tenant);
        // Fase E: por defecto simula un usuario histórico sin CompanyUserPreferences — preserva
        // el heurístico IsMainBranch salvo que un test sobrescriba este mock.
        f.Mediator.Setup(m => m.Send(It.IsAny<GetCompanyUserPreferencesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CompanyUserPreferencesDto?>.Success(null));

        return (f, tenant, company, user, membership);
    }

    private static CompanyUserPreferencesDto Preferences(
        Guid membershipId, Guid companyId, Guid tenantId, CompanyUserLoginMode loginMode, Guid? defaultBranchId) =>
        new(Guid.NewGuid(), tenantId, companyId, membershipId, defaultBranchId, loginMode.ToString());

    [Fact]
    public async Task SwitchCompany_con_sucursal_principal_resoluble_crea_UserSession_via_CreateAuthenticatedSessionCommand()
    {
        var (f, tenant, company, user, _) = BuildValidSwitch();
        var branch = NewMainBranch(tenant.Id, company.Id);
        f.BranchRepo.Setup(r => r.GetAsync(tenant.Id, true, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { branch });

        var sessionDto = new AuthenticatedSessionDto(
            new UserSessionDto(Guid.NewGuid(), tenant.Id, company.Id, user.Id, branch.Id, "terminal-unresolved", "Active", DateTime.UtcNow, null, null),
            "raw-refresh-token", DateTime.UtcNow.AddDays(30));

        CreateAuthenticatedSessionCommand? sentCommand = null;
        f.Mediator.Setup(m => m.Send(It.IsAny<CreateAuthenticatedSessionCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<AuthenticatedSessionDto>>, CancellationToken>((cmd, _) => sentCommand = (CreateAuthenticatedSessionCommand)cmd)
            .ReturnsAsync(Result<AuthenticatedSessionDto>.Success(sessionDto));

        var handler = f.BuildHandler();
        var result = await handler.Handle(new SwitchCompanyCommand(company.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RefreshToken.Should().Be("raw-refresh-token");
        result.Value.CompanyId.Should().Be(company.Id);

        sentCommand.Should().NotBeNull();
        sentCommand!.CompanyId.Should().Be(company.Id);
        sentCommand.BranchId.Should().Be(branch.Id);

        f.RefreshTokenService.Verify(s => s.CreateAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SwitchCompany_sin_sucursal_principal_resoluble_preserva_el_flujo_anterior()
    {
        var (f, tenant, company, user, _) = BuildValidSwitch();
        f.BranchRepo.Setup(r => r.GetAsync(tenant.Id, true, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Branch>());
        f.RefreshTokenService.Setup(s => s.CreateAsync(
                user.Id, tenant.Id, company.Id, RefreshUserType.Identity, It.IsAny<CancellationToken>()))
            .ReturnsAsync(("legacy-refresh-token", DateTime.UtcNow.AddDays(30)));

        var handler = f.BuildHandler();
        var result = await handler.Handle(new SwitchCompanyCommand(company.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RefreshToken.Should().Be("legacy-refresh-token");
        f.Mediator.Verify(m => m.Send(It.IsAny<CreateAuthenticatedSessionCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SwitchCompany_usuario_no_autenticado_falla_igual_que_antes_regresion()
    {
        var f = new Fixture();
        f.CurrentUser.Setup(c => c.IsAuthenticated).Returns(false);

        var handler = f.BuildHandler();
        var result = await handler.Handle(new SwitchCompanyCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        f.Mediator.Verify(m => m.Send(It.IsAny<CreateAuthenticatedSessionCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SwitchCompany_sin_membresia_falla_igual_que_antes_regresion()
    {
        var (f, tenant, company, user, _) = BuildValidSwitch();
        f.AccessRepo.Setup(r => r.GetCompanyUserMembershipAsync(company.Id, user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompanyUserMembership?)null);

        var handler = f.BuildHandler();
        var result = await handler.Handle(new SwitchCompanyCommand(company.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        f.Mediator.Verify(m => m.Send(It.IsAny<CreateAuthenticatedSessionCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        f.BranchRepo.Verify(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<bool?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SwitchCompany_UserSession_en_conflicto_devuelve_Result_Conflict()
    {
        var (f, tenant, company, user, _) = BuildValidSwitch();
        var branch = NewMainBranch(tenant.Id, company.Id);
        f.BranchRepo.Setup(r => r.GetAsync(tenant.Id, true, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { branch });
        f.Mediator.Setup(m => m.Send(It.IsAny<CreateAuthenticatedSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AuthenticatedSessionDto>.Conflict("Ya existe una sesión activa."));

        var handler = f.BuildHandler();
        var result = await handler.Handle(new SwitchCompanyCommand(company.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.Conflict);
    }

    // ── Fase E — lectura de CompanyUserPreferences durante SwitchCompany ────────────────────

    [Fact]
    public async Task SwitchCompany_DirectToDefault_con_sucursal_autorizada_usa_esa_sucursal_sin_heuristico()
    {
        var (f, tenant, company, user, membership) = BuildValidSwitch();
        var defaultBranchId = Guid.NewGuid();
        f.Mediator.Setup(m => m.Send(It.IsAny<GetCompanyUserPreferencesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CompanyUserPreferencesDto?>.Success(
                Preferences(membership.Id, company.Id, tenant.Id, CompanyUserLoginMode.DirectToDefault, defaultBranchId)));
        f.Mediator.Setup(m => m.Send(It.IsAny<UpdateCompanyUserPreferencesCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CompanyUserPreferencesDto>.Success(
                Preferences(membership.Id, company.Id, tenant.Id, CompanyUserLoginMode.DirectToDefault, defaultBranchId)));

        var sessionDto = new AuthenticatedSessionDto(
            new UserSessionDto(Guid.NewGuid(), tenant.Id, company.Id, user.Id, defaultBranchId, "terminal-unresolved", "Active", DateTime.UtcNow, null, null),
            "raw-refresh-token", DateTime.UtcNow.AddDays(30));
        CreateAuthenticatedSessionCommand? sentCommand = null;
        f.Mediator.Setup(m => m.Send(It.IsAny<CreateAuthenticatedSessionCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<AuthenticatedSessionDto>>, CancellationToken>((cmd, _) => sentCommand = (CreateAuthenticatedSessionCommand)cmd)
            .ReturnsAsync(Result<AuthenticatedSessionDto>.Success(sessionDto));

        var handler = f.BuildHandler();
        var result = await handler.Handle(new SwitchCompanyCommand(company.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        sentCommand!.BranchId.Should().Be(defaultBranchId);
        f.BranchRepo.Verify(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<bool?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SwitchCompany_DirectToDefault_con_sucursal_ya_no_autorizada_falla_con_ValidationFailure()
    {
        var (f, tenant, company, user, membership) = BuildValidSwitch();
        var revokedBranchId = Guid.NewGuid();
        f.Mediator.Setup(m => m.Send(It.IsAny<GetCompanyUserPreferencesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CompanyUserPreferencesDto?>.Success(
                Preferences(membership.Id, company.Id, tenant.Id, CompanyUserLoginMode.DirectToDefault, revokedBranchId)));
        f.Mediator.Setup(m => m.Send(It.IsAny<UpdateCompanyUserPreferencesCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CompanyUserPreferencesDto>.ValidationFailure(
                "La sucursal por defecto debe estar previamente autorizada para este usuario (CompanyUserBranch)."));

        var handler = f.BuildHandler();
        var result = await handler.Handle(new SwitchCompanyCommand(company.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        f.Mediator.Verify(m => m.Send(It.IsAny<CreateAuthenticatedSessionCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SwitchCompany_AskBranch_explicito_no_usa_el_heuristico_ni_crea_UserSession()
    {
        // Fase I-7: mismo criterio que LoginHandler — AskBranch explícito ya no cae al
        // heurístico IsMainBranch ahora que existe el selector post-login.
        var (f, tenant, company, user, membership) = BuildValidSwitch();
        var branch = NewMainBranch(tenant.Id, company.Id);
        f.Mediator.Setup(m => m.Send(It.IsAny<GetCompanyUserPreferencesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CompanyUserPreferencesDto?>.Success(
                Preferences(membership.Id, company.Id, tenant.Id, CompanyUserLoginMode.AskBranch, null)));
        f.BranchRepo.Setup(r => r.GetAsync(tenant.Id, true, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { branch });

        var handler = f.BuildHandler();
        var result = await handler.Handle(new SwitchCompanyCommand(company.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        f.Mediator.Verify(m => m.Send(It.IsAny<CreateAuthenticatedSessionCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        f.Mediator.Verify(m => m.Send(It.IsAny<UpdateCompanyUserPreferencesCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
