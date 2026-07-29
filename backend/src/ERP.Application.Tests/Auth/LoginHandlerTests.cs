using ERP.Application.Access.DTOs;
using ERP.Application.Access.UseCases.CreateAuthenticatedSession;
using ERP.Application.Access.UseCases.GetCompanyUserPreferences;
using ERP.Application.Access.UseCases.UpdateCompanyUserPreferences;
using ERP.Application.Auth.UseCases.Login;
using ERP.Application.Common;
using ERP.Application.Common.Config;
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
using Microsoft.Extensions.Options;
using Moq;

namespace ERP.Application.Tests.Auth;

/// <summary>
/// Fase 7: primeros tests de LoginHandler (no existía cobertura previa). Cubren tanto el
/// comportamiento nuevo (integración con CreateAuthenticatedSessionCommand) como el
/// comportamiento heredado que debe permanecer intacto (multi-empresa, credenciales inválidas).
/// </summary>
public sealed class LoginHandlerTests
{
    private static readonly Guid CreatedBy = Guid.NewGuid();
    private const string Username = "ana.perez";
    private const string Password = "Sup3rSecret!";
    private const string PasswordHash = "hashed-password";

    private static IdentityUser NewUser() =>
        IdentityUser.Create(Username, "Ana", "Perez", "ana@test.com", PasswordHash, CreatedBy);

    private static Tenant NewTenant() =>
        Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], CreatedBy);

    private static Company NewCompany(Guid tenantId) =>
        Company.CreateManaged(tenantId, "1790012345001", "Test S.A.", createdBy: CreatedBy);

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

    private sealed class Fixture
    {
        public Mock<ITenantRepository> TenantRepo { get; } = new();
        public Mock<ICompanyRepository> CompanyRepo { get; } = new();
        public Mock<IAccessRepository> AccessRepo { get; } = new();
        public Mock<IAccessTokenService> TokenService { get; } = new();
        public Mock<IPasswordHasher> PasswordHasher { get; } = new();
        public Mock<IRefreshTokenService> RefreshTokenService { get; } = new();
        public Mock<ICompanyProvisioningService> CompanyProvisioning { get; } = new();
        public Mock<IBranchRepository> BranchRepo { get; } = new();
        public Mock<IMediator> Mediator { get; } = new();
        public Mock<IPasswordResetTokenRepository> PasswordResetTokenRepo { get; } = new();
        public IOptions<PasswordResetOptions> PasswordResetOptions { get; } =
            Microsoft.Extensions.Options.Options.Create(new PasswordResetOptions());

        public LoginHandler BuildHandler() =>
            new(
                TenantRepo.Object,
                CompanyRepo.Object,
                AccessRepo.Object,
                TokenService.Object,
                PasswordHasher.Object,
                RefreshTokenService.Object,
                CompanyProvisioning.Object,
                BranchRepo.Object,
                Mediator.Object,
                PasswordResetTokenRepo.Object,
                PasswordResetOptions
            );
    }

    private static Fixture BuildValidLoginFixture(
        IdentityUser user,
        Tenant tenant,
        Company company,
        CompanyUserMembership membership
    )
    {
        var f = new Fixture();
        f.AccessRepo.Setup(r => r.GetUserByUsernameAsync(Username, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        f.AccessRepo.Setup(r =>
                r.GetActiveCompanyUserMembershipsForUserSystemAsync(
                    user.Id,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new[] { membership });
        f.CompanyRepo.Setup(r =>
                r.GetByIdsAsync(
                    It.Is<IReadOnlyCollection<Guid>>(c => c.Contains(company.Id)),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new[] { company });
        f.TenantRepo.Setup(r => r.GetByIdAsync(tenant.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        f.PasswordHasher.Setup(h => h.VerifyPassword(Password, PasswordHash)).Returns(true);
        f.TokenService.Setup(s => s.GenerateSessionToken(user, tenant.Id, membership.Role))
            .Returns("jwt-token");
        f.CompanyProvisioning.Setup(s =>
                s.EnsureDefaultCompanyAsync(tenant, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(company);
        // Fase E: por defecto simula un usuario histórico sin fila de CompanyUserPreferences —
        // preserva exactamente el comportamiento heredado (heurístico IsMainBranch) salvo que un
        // test individual sobrescriba este mock para ejercer AskBranch/DirectToDefault.
        f.Mediator.Setup(m =>
                m.Send(It.IsAny<GetCompanyUserPreferencesQuery>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(Result<CompanyUserPreferencesDto?>.Success(null));
        return f;
    }

    [Fact]
    public async Task Login_con_sucursal_principal_resoluble_crea_UserSession_via_CreateAuthenticatedSessionCommand()
    {
        var user = NewUser();
        var tenant = NewTenant();
        var company = NewCompany(tenant.Id);
        var membership = CompanyUserMembership.Create(
            company.Id,
            user.Id,
            "Admin",
            null,
            CreatedBy
        );
        var branch = NewMainBranch(tenant.Id, company.Id);

        var f = BuildValidLoginFixture(user, tenant, company, membership);
        f.BranchRepo.Setup(r => r.GetAsync(tenant.Id, true, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { branch });

        var sessionDto = new AuthenticatedSessionDto(
            new UserSessionDto(
                Guid.NewGuid(),
                tenant.Id,
                company.Id,
                user.Id,
                branch.Id,
                "terminal-unresolved",
                "Active",
                DateTime.UtcNow,
                null,
                null
            ),
            "raw-refresh-token",
            DateTime.UtcNow.AddDays(30)
        );

        CreateAuthenticatedSessionCommand? sentCommand = null;
        f.Mediator.Setup(m =>
                m.Send(It.IsAny<CreateAuthenticatedSessionCommand>(), It.IsAny<CancellationToken>())
            )
            .Callback<IRequest<Result<AuthenticatedSessionDto>>, CancellationToken>(
                (cmd, _) => sentCommand = (CreateAuthenticatedSessionCommand)cmd
            )
            .ReturnsAsync(Result<AuthenticatedSessionDto>.Success(sessionDto));

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new LoginCommand(Username, Password),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.RefreshToken.Should().Be("raw-refresh-token");
        result.Value.Token.Should().Be("jwt-token");
        result.Value.CompanyId.Should().Be(company.Id);

        sentCommand.Should().NotBeNull();
        sentCommand!.TenantId.Should().Be(tenant.Id);
        sentCommand.CompanyId.Should().Be(company.Id);
        sentCommand.IdentityUserId.Should().Be(user.Id);
        sentCommand.BranchId.Should().Be(branch.Id);

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
    public async Task Login_sin_sucursal_principal_resoluble_preserva_el_flujo_anterior_sin_UserSession()
    {
        var user = NewUser();
        var tenant = NewTenant();
        var company = NewCompany(tenant.Id);
        var membership = CompanyUserMembership.Create(
            company.Id,
            user.Id,
            "Admin",
            null,
            CreatedBy
        );

        var f = BuildValidLoginFixture(user, tenant, company, membership);
        // Ninguna sucursal marcada como IsMainBranch → no hay resolución posible.
        f.BranchRepo.Setup(r => r.GetAsync(tenant.Id, true, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Branch>());
        f.RefreshTokenService.Setup(s =>
                s.CreateAsync(
                    user.Id,
                    tenant.Id,
                    company.Id,
                    RefreshUserType.Identity,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(("legacy-refresh-token", DateTime.UtcNow.AddDays(30)));

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new LoginCommand(Username, Password),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.RefreshToken.Should().Be("legacy-refresh-token");

        f.Mediator.Verify(
            m =>
                m.Send(
                    It.IsAny<CreateAuthenticatedSessionCommand>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task Login_con_multiples_empresas_no_intenta_crear_UserSession_regresion()
    {
        var user = NewUser();
        var tenant = NewTenant();
        var companyA = NewCompany(tenant.Id);
        var companyB = Company.CreateManaged(
            tenant.Id,
            "1790012345002",
            "Otra S.A.",
            createdBy: CreatedBy
        );
        var membershipA = CompanyUserMembership.Create(
            companyA.Id,
            user.Id,
            "Admin",
            null,
            CreatedBy
        );
        var membershipB = CompanyUserMembership.Create(
            companyB.Id,
            user.Id,
            "Admin",
            null,
            CreatedBy
        );

        var f = new Fixture();
        f.AccessRepo.Setup(r => r.GetUserByUsernameAsync(Username, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        f.AccessRepo.Setup(r =>
                r.GetActiveCompanyUserMembershipsForUserSystemAsync(
                    user.Id,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new[] { membershipA, membershipB });
        f.CompanyRepo.Setup(r =>
                r.GetByIdsAsync(
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new[] { companyA, companyB });
        f.TenantRepo.Setup(r => r.GetByIdAsync(tenant.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        f.PasswordHasher.Setup(h => h.VerifyPassword(Password, PasswordHash)).Returns(true);
        f.TokenService.Setup(s => s.GenerateSessionToken(user, tenant.Id, It.IsAny<string>()))
            .Returns("bootstrap-jwt");
        f.RefreshTokenService.Setup(s =>
                s.CreateAsync(
                    user.Id,
                    tenant.Id,
                    null,
                    RefreshUserType.Identity,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(("bootstrap-refresh", DateTime.UtcNow.AddDays(30)));

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new LoginCommand(Username, Password),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.RequiresCompanySelection.Should().BeTrue();
        result.Value.CompanyId.Should().BeNull();

        f.Mediator.Verify(
            m =>
                m.Send(
                    It.IsAny<CreateAuthenticatedSessionCommand>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
        f.BranchRepo.Verify(
            r =>
                r.GetAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<bool?>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task Login_credenciales_invalidas_falla_igual_que_antes_regresion()
    {
        var user = NewUser();
        var f = new Fixture();
        f.AccessRepo.Setup(r => r.GetUserByUsernameAsync(Username, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        f.PasswordHasher.Setup(h => h.VerifyPassword(Password, PasswordHash)).Returns(false);

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new LoginCommand(Username, Password),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        f.Mediator.Verify(
            m =>
                m.Send(
                    It.IsAny<CreateAuthenticatedSessionCommand>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task Login_UserSession_en_conflicto_devuelve_Result_Conflict()
    {
        var user = NewUser();
        var tenant = NewTenant();
        var company = NewCompany(tenant.Id);
        var membership = CompanyUserMembership.Create(
            company.Id,
            user.Id,
            "Admin",
            null,
            CreatedBy
        );
        var branch = NewMainBranch(tenant.Id, company.Id);

        var f = BuildValidLoginFixture(user, tenant, company, membership);
        f.BranchRepo.Setup(r => r.GetAsync(tenant.Id, true, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { branch });
        f.Mediator.Setup(m =>
                m.Send(It.IsAny<CreateAuthenticatedSessionCommand>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(Result<AuthenticatedSessionDto>.Conflict("Ya existe una sesión activa."));

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new LoginCommand(Username, Password),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.Conflict);
    }

    // ── Fase E — lectura de CompanyUserPreferences durante el login ─────────────────────────

    private static CompanyUserPreferencesDto Preferences(
        Guid membershipId,
        Guid companyId,
        Guid tenantId,
        CompanyUserLoginMode loginMode,
        Guid? defaultBranchId
    ) =>
        new(
            Guid.NewGuid(),
            tenantId,
            companyId,
            membershipId,
            defaultBranchId,
            loginMode.ToString()
        );

    [Fact]
    public async Task Usuario_DirectToDefault_con_sucursal_autorizada_crea_sesion_sin_usar_el_heuristico()
    {
        var user = NewUser();
        var tenant = NewTenant();
        var company = NewCompany(tenant.Id);
        var membership = CompanyUserMembership.Create(
            company.Id,
            user.Id,
            "Admin",
            null,
            CreatedBy
        );
        var defaultBranchId = Guid.NewGuid();

        var f = BuildValidLoginFixture(user, tenant, company, membership);
        f.Mediator.Setup(m =>
                m.Send(It.IsAny<GetCompanyUserPreferencesQuery>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                Result<CompanyUserPreferencesDto?>.Success(
                    Preferences(
                        membership.Id,
                        company.Id,
                        tenant.Id,
                        CompanyUserLoginMode.DirectToDefault,
                        defaultBranchId
                    )
                )
            );
        f.Mediator.Setup(m =>
                m.Send(
                    It.IsAny<UpdateCompanyUserPreferencesCommand>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                Result<CompanyUserPreferencesDto>.Success(
                    Preferences(
                        membership.Id,
                        company.Id,
                        tenant.Id,
                        CompanyUserLoginMode.DirectToDefault,
                        defaultBranchId
                    )
                )
            );

        var sessionDto = new AuthenticatedSessionDto(
            new UserSessionDto(
                Guid.NewGuid(),
                tenant.Id,
                company.Id,
                user.Id,
                defaultBranchId,
                "terminal-unresolved",
                "Active",
                DateTime.UtcNow,
                null,
                null
            ),
            "raw-refresh-token",
            DateTime.UtcNow.AddDays(30)
        );
        CreateAuthenticatedSessionCommand? sentCommand = null;
        f.Mediator.Setup(m =>
                m.Send(It.IsAny<CreateAuthenticatedSessionCommand>(), It.IsAny<CancellationToken>())
            )
            .Callback<IRequest<Result<AuthenticatedSessionDto>>, CancellationToken>(
                (cmd, _) => sentCommand = (CreateAuthenticatedSessionCommand)cmd
            )
            .ReturnsAsync(Result<AuthenticatedSessionDto>.Success(sessionDto));

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new LoginCommand(Username, Password),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        sentCommand.Should().NotBeNull();
        sentCommand!.BranchId.Should().Be(defaultBranchId);

        // No consulta el heurístico de sucursal principal — DirectToDefault resuelve la
        // sucursal directamente desde CompanyUserPreferences, sin lecturas adicionales.
        f.BranchRepo.Verify(
            r =>
                r.GetAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<bool?>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task Usuario_DirectToDefault_con_sucursal_ya_no_autorizada_falla_con_ValidationFailure()
    {
        var user = NewUser();
        var tenant = NewTenant();
        var company = NewCompany(tenant.Id);
        var membership = CompanyUserMembership.Create(
            company.Id,
            user.Id,
            "Admin",
            null,
            CreatedBy
        );
        var revokedBranchId = Guid.NewGuid();

        var f = BuildValidLoginFixture(user, tenant, company, membership);
        f.Mediator.Setup(m =>
                m.Send(It.IsAny<GetCompanyUserPreferencesQuery>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                Result<CompanyUserPreferencesDto?>.Success(
                    Preferences(
                        membership.Id,
                        company.Id,
                        tenant.Id,
                        CompanyUserLoginMode.DirectToDefault,
                        revokedBranchId
                    )
                )
            );
        f.Mediator.Setup(m =>
                m.Send(
                    It.IsAny<UpdateCompanyUserPreferencesCommand>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                Result<CompanyUserPreferencesDto>.ValidationFailure(
                    "La sucursal por defecto debe estar previamente autorizada para este usuario (CompanyUserBranch)."
                )
            );

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new LoginCommand(Username, Password),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        f.Mediator.Verify(
            m =>
                m.Send(
                    It.IsAny<CreateAuthenticatedSessionCommand>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task Usuario_AskBranch_explicito_no_usa_el_heuristico_ni_crea_UserSession()
    {
        // Fase I-7: antes de que existiera el selector post-login (BranchSelectorModal/
        // useBranchGate), AskBranch caía al mismo heurístico IsMainBranch que un usuario sin
        // preferencias — ahora que el selector existe, AskBranch explícito debe recibir
        // BranchId null en el login (sin UserSession) y dejar que el frontend pida la sucursal.
        var user = NewUser();
        var tenant = NewTenant();
        var company = NewCompany(tenant.Id);
        var membership = CompanyUserMembership.Create(
            company.Id,
            user.Id,
            "Admin",
            null,
            CreatedBy
        );
        var branch = NewMainBranch(tenant.Id, company.Id);

        var f = BuildValidLoginFixture(user, tenant, company, membership);
        f.Mediator.Setup(m =>
                m.Send(It.IsAny<GetCompanyUserPreferencesQuery>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                Result<CompanyUserPreferencesDto?>.Success(
                    Preferences(
                        membership.Id,
                        company.Id,
                        tenant.Id,
                        CompanyUserLoginMode.AskBranch,
                        null
                    )
                )
            );
        f.BranchRepo.Setup(r => r.GetAsync(tenant.Id, true, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { branch });

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new LoginCommand(Username, Password),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        f.Mediator.Verify(
            m =>
                m.Send(
                    It.IsAny<CreateAuthenticatedSessionCommand>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
        f.Mediator.Verify(
            m =>
                m.Send(
                    It.IsAny<UpdateCompanyUserPreferencesCommand>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task Usuario_historico_sin_preferencias_no_rompe_el_login_y_usa_el_heuristico()
    {
        // Cubierto también de forma implícita por todos los tests que usan BuildValidLoginFixture
        // (su mock por defecto simula ausencia de preferencias) — este test lo hace explícito.
        var user = NewUser();
        var tenant = NewTenant();
        var company = NewCompany(tenant.Id);
        var membership = CompanyUserMembership.Create(
            company.Id,
            user.Id,
            "Admin",
            null,
            CreatedBy
        );
        var branch = NewMainBranch(tenant.Id, company.Id);

        var f = BuildValidLoginFixture(user, tenant, company, membership);
        f.BranchRepo.Setup(r => r.GetAsync(tenant.Id, true, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { branch });

        var sessionDto = new AuthenticatedSessionDto(
            new UserSessionDto(
                Guid.NewGuid(),
                tenant.Id,
                company.Id,
                user.Id,
                branch.Id,
                "terminal-unresolved",
                "Active",
                DateTime.UtcNow,
                null,
                null
            ),
            "raw-refresh-token",
            DateTime.UtcNow.AddDays(30)
        );
        f.Mediator.Setup(m =>
                m.Send(It.IsAny<CreateAuthenticatedSessionCommand>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(Result<AuthenticatedSessionDto>.Success(sessionDto));

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new LoginCommand(Username, Password),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        f.Mediator.Verify(
            m =>
                m.Send(
                    It.IsAny<UpdateCompanyUserPreferencesCommand>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    // ── Fase H — RequirePasswordReset ────────────────────────────────────────────────────────

    [Fact]
    public async Task Usuario_con_RequirePasswordReset_no_recibe_JWT_y_recibe_un_PasswordResetToken()
    {
        var user = IdentityUser.Create(
            Username,
            "Ana",
            "Perez",
            "ana@test.com",
            PasswordHash,
            CreatedBy
        );
        user.MarkRequirePasswordReset(CreatedBy);
        var tenant = NewTenant();
        var company = NewCompany(tenant.Id);
        var membership = CompanyUserMembership.Create(
            company.Id,
            user.Id,
            "Admin",
            null,
            CreatedBy
        );

        var f = new Fixture();
        f.AccessRepo.Setup(r => r.GetUserByUsernameAsync(Username, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        f.AccessRepo.Setup(r =>
                r.GetActiveCompanyUserMembershipsForUserSystemAsync(
                    user.Id,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new[] { membership });
        f.CompanyRepo.Setup(r =>
                r.GetByIdsAsync(
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new[] { company });
        f.PasswordHasher.Setup(h => h.VerifyPassword(Password, PasswordHash)).Returns(true);

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new LoginCommand(Username, Password),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.RequiresPasswordReset.Should().BeTrue();
        result.Value.PasswordResetToken.Should().NotBeNullOrEmpty();
        result.Value.PasswordResetTokenExpiresIn.Should().NotBeNull();
        result.Value.Token.Should().BeEmpty();
        result.Value.RefreshToken.Should().BeNull();

        f.PasswordResetTokenRepo.Verify(
            r =>
                r.AddAsync(
                    It.IsAny<ERP.Domain.Auth.Entities.PasswordResetToken>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        f.TokenService.Verify(
            s =>
                s.GenerateSessionToken(
                    It.IsAny<IdentityUser>(),
                    It.IsAny<Guid>(),
                    It.IsAny<string>()
                ),
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
        f.Mediator.Verify(
            m =>
                m.Send(
                    It.IsAny<CreateAuthenticatedSessionCommand>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task Usuario_con_RequirePasswordReset_pero_credenciales_invalidas_no_revela_el_estado_ni_emite_token()
    {
        var user = IdentityUser.Create(
            Username,
            "Ana",
            "Perez",
            "ana@test.com",
            PasswordHash,
            CreatedBy
        );
        user.MarkRequirePasswordReset(CreatedBy);

        var f = new Fixture();
        f.AccessRepo.Setup(r => r.GetUserByUsernameAsync(Username, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        f.PasswordHasher.Setup(h => h.VerifyPassword(Password, PasswordHash)).Returns(false);

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new LoginCommand(Username, Password),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        f.PasswordResetTokenRepo.Verify(
            r =>
                r.AddAsync(
                    It.IsAny<ERP.Domain.Auth.Entities.PasswordResetToken>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }
}
