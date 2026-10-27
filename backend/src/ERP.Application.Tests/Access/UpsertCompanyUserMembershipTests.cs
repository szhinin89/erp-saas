using ERP.Application.Access;
using ERP.Application.Access.Caching;
using ERP.Application.Access.DTOs;
using ERP.Application.Access.UseCases.CreateCompanyUserPreferences;
using ERP.Application.Access.UseCases.GetCompanyUserPreferences;
using ERP.Application.Access.UseCases.UpdateCompanyUserPreferences;
using ERP.Application.Access.UseCases.UpsertCompanyUserMembership;
using ERP.Application.Common;
using ERP.Application.Navigation;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Enums;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Branches.Interfaces;
using ERP.Domain.Kernel.Security;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Tenants.Entities;
using ERP.Domain.Tenants.Interfaces;
using FluentAssertions;
using MediatR;
using Moq;

namespace ERP.Application.Tests.Access;

/// <summary>
/// Fase D: extensión de UpsertCompanyUserMembership para autorizar sucursales
/// (CompanyUserBranch, único flujo de producción que la escribe) y configurar preferencias
/// operativas (CompanyUserPreferences, siempre vía sus UseCases ya existentes de Fase C — nunca
/// se reimplementa su validación). El comportamiento heredado (crear/reactivar membresía) no se
/// modifica y se cubre aquí para detectar regresiones. SEC-05: anti-escalamiento — rol Admin solo
/// por Admin de la empresa u operador global, sin autoescalamiento sobre la propia membership, y
/// perfil existente, activo, de la misma empresa y dentro del alcance del asignador no Admin.
/// </summary>
public sealed class UpsertCompanyUserMembershipHandlerTests
{
    private static readonly Guid CreatedBy = Guid.NewGuid();
    private const string Username = "ana.perez";

    private static IdentityUser NewUser() =>
        IdentityUser.Create(Username, "Ana", "Perez", "ana@test.com", "hash", CreatedBy);

    private static Tenant NewTenant() =>
        Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], CreatedBy);

    private static Company NewCompany(Guid tenantId) =>
        Company.CreateManaged(tenantId, "1790012345001", "Test S.A.", createdBy: CreatedBy);

    private static Branch NewBranch(Guid tenantId, Guid companyId) =>
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
            true,
            CreatedBy,
            companyId: companyId
        );

    private sealed class Fixture
    {
        public Mock<IAccessRepository> AccessRepo { get; } = new();
        public Mock<ICurrentUser> CurrentUser { get; } = new();
        public Mock<ITenantRepository> TenantRepo { get; } = new();
        public Mock<ICompanyRepository> CompanyRepo { get; } = new();
        public Mock<IPermissionsCacheInvalidator> PermissionsCache { get; } =
            PermissionsCacheInvalidatorMocks.Healthy();
        public Mock<INavigationBuilder> NavigationBuilder { get; } = new();
        public Mock<IBranchRepository> BranchRepo { get; } = new();
        public Mock<ICompanyUserBranchRepository> CompanyUserBranchRepo { get; } = new();
        public Mock<IMediator> Mediator { get; } = new();
        public Mock<ICompanyContextProvider> CompanyContext { get; } = new();
        public Mock<IEffectivePermissionKeysProvider> EffectivePermissionKeys { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();

        public Fixture()
        {
            CurrentUser.SetupGet(x => x.UserId).Returns(CreatedBy);
        }

        public UpsertCompanyUserMembershipHandler BuildHandler() =>
            new(
                AccessRepo.Object,
                CurrentUser.Object,
                TenantRepo.Object,
                CompanyRepo.Object,
                PermissionsCache.Object,
                NavigationBuilder.Object,
                BranchRepo.Object,
                CompanyUserBranchRepo.Object,
                Mediator.Object,
                CompanyContext.Object,
                EffectivePermissionKeys.Object,
                UnitOfWork.Object
            );
    }

    private static Fixture BuildBaseFixture(
        IdentityUser user,
        Tenant tenant,
        Company company,
        CompanyUserMembership? existingMembership
    )
    {
        var f = new Fixture();
        f.TenantRepo.Setup(r => r.GetByIdAsync(tenant.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        f.CompanyRepo.Setup(r =>
                r.GetByIdForTenantAsync(company.Id, tenant.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(company);
        f.AccessRepo.Setup(r => r.GetUserByUsernameAsync(Username, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        f.AccessRepo.Setup(r =>
                r.GetCompanyUserMembershipAsync(company.Id, user.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(existingMembership);
        // Por defecto el que asigna es Admin de la empresa del comando (flujo administrativo normal).
        SetActor(
            f,
            new OperationalCompanyContext(
                company.Id,
                CreatedBy,
                null,
                IsActiveMembership: true,
                MembershipRole: SecurityRoles.Admin
            )
        );
        return f;
    }

    private static void SetActor(Fixture f, OperationalCompanyContext? actor) =>
        f.CompanyContext.Setup(c =>
                c.ResolveOperationalForCurrentUserAsync(It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(actor);

    private static readonly Guid DelegateProfileId = Guid.NewGuid();

    // Usuario delegado (no Admin) con access.company_user_memberships.view en la empresa.
    private static void SetDelegateActor(Fixture f, Guid companyId, params string[] effectiveKeys)
    {
        SetActor(
            f,
            new OperationalCompanyContext(
                companyId,
                CreatedBy,
                DelegateProfileId,
                IsActiveMembership: true,
                MembershipRole: SecurityRoles.User
            )
        );
        f.EffectivePermissionKeys.Setup(k =>
                k.GetAllowedKeysAsync(
                    It.IsAny<Guid>(),
                    companyId,
                    CreatedBy,
                    DelegateProfileId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(effectiveKeys);
    }

    private static AccessProfile SetupProfile(
        Fixture f,
        Guid tenantId,
        Guid companyId,
        bool active = true,
        params string[] allowedKeys
    )
    {
        var profile = AccessProfile.Create(tenantId, companyId, "Perfil", null, CreatedBy);
        if (!active)
            profile.Deactivate(CreatedBy);
        f.AccessRepo.Setup(r =>
                r.GetProfileByIdAsync(
                    tenantId,
                    companyId,
                    profile.Id,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(profile);
        f.AccessRepo.Setup(r =>
                r.GetProfilePermissionsAsync(tenantId, profile.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                allowedKeys
                    .Select(k =>
                        AccessProfilePermission.Create(tenantId, profile.Id, k, true, CreatedBy)
                    )
                    .ToList()
            );
        return profile;
    }

    private static void AssertNothingPersisted(Fixture f)
    {
        f.AccessRepo.Verify(
            r =>
                r.AddCompanyUserMembershipAsync(
                    It.IsAny<CompanyUserMembership>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
        f.AccessRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static void SetupNoExistingPreferences(Fixture f)
    {
        f.Mediator.Setup(m =>
                m.Send(It.IsAny<GetCompanyUserPreferencesQuery>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(Result<CompanyUserPreferencesDto?>.Success(null));
    }

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
    public async Task Membresia_nueva_sin_preferencias_informadas_crea_preferencias_AskBranch_por_defecto()
    {
        var user = NewUser();
        var tenant = NewTenant();
        var company = NewCompany(tenant.Id);
        var f = BuildBaseFixture(user, tenant, company, existingMembership: null);
        SetupNoExistingPreferences(f);

        CreateCompanyUserPreferencesCommand? sentCreate = null;
        f.Mediator.Setup(m =>
                m.Send(
                    It.IsAny<CreateCompanyUserPreferencesCommand>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<IRequest<Result<CompanyUserPreferencesDto>>, CancellationToken>(
                (cmd, _) => sentCreate = (CreateCompanyUserPreferencesCommand)cmd
            )
            .ReturnsAsync(
                (IRequest<Result<CompanyUserPreferencesDto>> cmd, CancellationToken _) =>
                {
                    var c = (CreateCompanyUserPreferencesCommand)cmd;
                    return Result<CompanyUserPreferencesDto>.Success(
                        Preferences(
                            c.CompanyUserMembershipId,
                            company.Id,
                            tenant.Id,
                            CompanyUserLoginMode.AskBranch,
                            null
                        )
                    );
                }
            );

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new UpsertCompanyUserMembershipCommand(tenant.Id, company.Id, Username, "User"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        sentCreate.Should().NotBeNull();
        sentCreate!.LoginMode.Should().Be(nameof(CompanyUserLoginMode.AskBranch));
        sentCreate.DefaultBranchId.Should().BeNull();
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
    public async Task Membresia_nueva_con_sucursal_a_autorizar_y_DirectToDefault_autoriza_y_crea_preferencias()
    {
        var user = NewUser();
        var tenant = NewTenant();
        var company = NewCompany(tenant.Id);
        var branch = NewBranch(tenant.Id, company.Id);
        var f = BuildBaseFixture(user, tenant, company, existingMembership: null);
        SetupNoExistingPreferences(f);
        f.BranchRepo.Setup(r =>
                r.GetByIdForCompanyAsync(
                    tenant.Id,
                    company.Id,
                    branch.Id,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(branch);
        f.CompanyUserBranchRepo.Setup(r =>
                r.ExistsAsync(It.IsAny<Guid>(), branch.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(false);

        CreateCompanyUserPreferencesCommand? sentCreate = null;
        f.Mediator.Setup(m =>
                m.Send(
                    It.IsAny<CreateCompanyUserPreferencesCommand>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<IRequest<Result<CompanyUserPreferencesDto>>, CancellationToken>(
                (cmd, _) => sentCreate = (CreateCompanyUserPreferencesCommand)cmd
            )
            .ReturnsAsync(
                (IRequest<Result<CompanyUserPreferencesDto>> cmd, CancellationToken _) =>
                {
                    var c = (CreateCompanyUserPreferencesCommand)cmd;
                    return Result<CompanyUserPreferencesDto>.Success(
                        Preferences(
                            c.CompanyUserMembershipId,
                            company.Id,
                            tenant.Id,
                            CompanyUserLoginMode.DirectToDefault,
                            c.DefaultBranchId
                        )
                    );
                }
            );

        var handler = f.BuildHandler();
        var command = new UpsertCompanyUserMembershipCommand(
            tenant.Id,
            company.Id,
            Username,
            "User",
            AuthorizedBranchIds: new[] { branch.Id },
            DefaultBranchId: branch.Id,
            LoginMode: nameof(CompanyUserLoginMode.DirectToDefault)
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        f.CompanyUserBranchRepo.Verify(
            r =>
                r.AddAsync(
                    It.Is<CompanyUserBranch>(b =>
                        b.BranchId == branch.Id && b.CreatedBy == CreatedBy
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        f.CompanyUserBranchRepo.Verify(
            r => r.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once
        );
        sentCreate!.DefaultBranchId.Should().Be(branch.Id);
        sentCreate.LoginMode.Should().Be(nameof(CompanyUserLoginMode.DirectToDefault));
    }

    [Fact]
    public async Task Sucursal_a_autorizar_inexistente_devuelve_NotFound_y_no_crea_ninguna_autorizacion()
    {
        var user = NewUser();
        var tenant = NewTenant();
        var company = NewCompany(tenant.Id);
        var missingBranchId = Guid.NewGuid();
        var f = BuildBaseFixture(user, tenant, company, existingMembership: null);
        f.BranchRepo.Setup(r =>
                r.GetByIdForCompanyAsync(
                    tenant.Id,
                    company.Id,
                    missingBranchId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((Branch?)null);

        var handler = f.BuildHandler();
        var command = new UpsertCompanyUserMembershipCommand(
            tenant.Id,
            company.Id,
            Username,
            "User",
            AuthorizedBranchIds: new[] { missingBranchId }
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        f.CompanyUserBranchRepo.Verify(
            r => r.AddAsync(It.IsAny<CompanyUserBranch>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        f.Mediator.Verify(
            m => m.Send(It.IsAny<GetCompanyUserPreferencesQuery>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Sucursal_ya_autorizada_no_se_duplica()
    {
        var user = NewUser();
        var tenant = NewTenant();
        var company = NewCompany(tenant.Id);
        var branch = NewBranch(tenant.Id, company.Id);
        var f = BuildBaseFixture(user, tenant, company, existingMembership: null);
        SetupNoExistingPreferences(f);
        f.BranchRepo.Setup(r =>
                r.GetByIdForCompanyAsync(
                    tenant.Id,
                    company.Id,
                    branch.Id,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(branch);
        f.CompanyUserBranchRepo.Setup(r =>
                r.ExistsAsync(It.IsAny<Guid>(), branch.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(true);
        f.Mediator.Setup(m =>
                m.Send(
                    It.IsAny<CreateCompanyUserPreferencesCommand>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                Result<CompanyUserPreferencesDto>.Success(
                    Preferences(
                        Guid.NewGuid(),
                        company.Id,
                        tenant.Id,
                        CompanyUserLoginMode.AskBranch,
                        null
                    )
                )
            );

        var handler = f.BuildHandler();
        var command = new UpsertCompanyUserMembershipCommand(
            tenant.Id,
            company.Id,
            Username,
            "User",
            AuthorizedBranchIds: new[] { branch.Id }
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        f.CompanyUserBranchRepo.Verify(
            r => r.AddAsync(It.IsAny<CompanyUserBranch>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task DefaultBranchId_no_autorizada_propaga_ValidationFailure_desde_CreateCompanyUserPreferences()
    {
        var user = NewUser();
        var tenant = NewTenant();
        var company = NewCompany(tenant.Id);
        var branch = NewBranch(tenant.Id, company.Id);
        var f = BuildBaseFixture(user, tenant, company, existingMembership: null);
        SetupNoExistingPreferences(f);
        f.Mediator.Setup(m =>
                m.Send(
                    It.IsAny<CreateCompanyUserPreferencesCommand>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                Result<CompanyUserPreferencesDto>.ValidationFailure(
                    "La sucursal por defecto debe estar previamente autorizada para este usuario (CompanyUserBranch)."
                )
            );

        var handler = f.BuildHandler();
        var command = new UpsertCompanyUserMembershipCommand(
            tenant.Id,
            company.Id,
            Username,
            "User",
            DefaultBranchId: branch.Id,
            LoginMode: nameof(CompanyUserLoginMode.DirectToDefault)
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
    }

    [Fact]
    public async Task Reactivar_membresia_existente_con_preferencias_sin_informar_campos_no_actualiza_preferencias()
    {
        var user = NewUser();
        var tenant = NewTenant();
        var company = NewCompany(tenant.Id);
        var membership = CompanyUserMembership.Create(company.Id, user.Id, "User", null, CreatedBy);
        var f = BuildBaseFixture(user, tenant, company, existingMembership: membership);
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
                        Guid.NewGuid()
                    )
                )
            );

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new UpsertCompanyUserMembershipCommand(tenant.Id, company.Id, Username, "Admin"),
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
        f.Mediator.Verify(
            m =>
                m.Send(
                    It.IsAny<CreateCompanyUserPreferencesCommand>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task Reactivar_membresia_existente_informando_solo_LoginMode_preserva_DefaultBranchId_previo()
    {
        var user = NewUser();
        var tenant = NewTenant();
        var company = NewCompany(tenant.Id);
        var membership = CompanyUserMembership.Create(company.Id, user.Id, "User", null, CreatedBy);
        var previousBranchId = Guid.NewGuid();
        var f = BuildBaseFixture(user, tenant, company, existingMembership: membership);
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
                        previousBranchId
                    )
                )
            );

        UpdateCompanyUserPreferencesCommand? sentUpdate = null;
        f.Mediator.Setup(m =>
                m.Send(
                    It.IsAny<UpdateCompanyUserPreferencesCommand>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<IRequest<Result<CompanyUserPreferencesDto>>, CancellationToken>(
                (cmd, _) => sentUpdate = (UpdateCompanyUserPreferencesCommand)cmd
            )
            .ReturnsAsync(
                Result<CompanyUserPreferencesDto>.Success(
                    Preferences(
                        membership.Id,
                        company.Id,
                        tenant.Id,
                        CompanyUserLoginMode.DirectToDefault,
                        previousBranchId
                    )
                )
            );

        var handler = f.BuildHandler();
        var command = new UpsertCompanyUserMembershipCommand(
            tenant.Id,
            company.Id,
            Username,
            "Admin",
            LoginMode: nameof(CompanyUserLoginMode.DirectToDefault)
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        sentUpdate.Should().NotBeNull();
        sentUpdate!.DefaultBranchId.Should().Be(previousBranchId);
        sentUpdate.LoginMode.Should().Be(nameof(CompanyUserLoginMode.DirectToDefault));
    }

    // ── CompanyAdministratorInvariant — degradar el rol de una membresía activa ──────────────

    [Fact]
    public async Task Degradar_al_unico_Admin_activo_falla_con_el_mensaje_del_invariante_y_no_cambia_el_rol()
    {
        var user = NewUser();
        var tenant = NewTenant();
        var company = NewCompany(tenant.Id);
        var membership = CompanyUserMembership.Create(
            company.Id,
            user.Id,
            SecurityRoles.Admin,
            null,
            CreatedBy
        );
        var f = BuildBaseFixture(user, tenant, company, existingMembership: membership);
        f.AccessRepo.Setup(r =>
                r.GetCompanyUserMembershipsByCompanyAsync(
                    company.Id,
                    true,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new[] { membership });

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new UpsertCompanyUserMembershipCommand(tenant.Id, company.Id, Username, "User"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        result.Error.Should().Be("La empresa debe conservar al menos un administrador activo.");
        membership.Role.Should().Be(SecurityRoles.Admin);
        f.AccessRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        f.Mediator.Verify(
            m => m.Send(It.IsAny<GetCompanyUserPreferencesQuery>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Degradar_Admin_cuando_existe_otro_Admin_activo_succeeds()
    {
        var user = NewUser();
        var tenant = NewTenant();
        var company = NewCompany(tenant.Id);
        var membership = CompanyUserMembership.Create(
            company.Id,
            user.Id,
            SecurityRoles.Admin,
            null,
            CreatedBy
        );
        var otherAdmin = CompanyUserMembership.Create(
            company.Id,
            Guid.NewGuid(),
            SecurityRoles.Admin,
            null,
            CreatedBy
        );
        var f = BuildBaseFixture(user, tenant, company, existingMembership: membership);
        f.AccessRepo.Setup(r =>
                r.GetCompanyUserMembershipsByCompanyAsync(
                    company.Id,
                    true,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new[] { membership, otherAdmin });
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

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new UpsertCompanyUserMembershipCommand(tenant.Id, company.Id, Username, "User"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        membership.Role.Should().Be("User");
        f.AccessRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Tenant_con_empresa_A_y_B_comando_para_B_crea_membership_en_B_no_en_A()
    {
        var user = NewUser();
        var tenant = NewTenant();
        var companyA = NewCompany(tenant.Id);
        var companyB = NewCompany(tenant.Id);
        var f = BuildBaseFixture(user, tenant, companyB, existingMembership: null);
        SetupNoExistingPreferences(f);
        f.Mediator.Setup(m =>
                m.Send(
                    It.IsAny<CreateCompanyUserPreferencesCommand>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                Result<CompanyUserPreferencesDto>.Success(
                    Preferences(
                        Guid.NewGuid(),
                        companyB.Id,
                        tenant.Id,
                        CompanyUserLoginMode.AskBranch,
                        null
                    )
                )
            );

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new UpsertCompanyUserMembershipCommand(tenant.Id, companyB.Id, Username, "User"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        f.AccessRepo.Verify(
            r =>
                r.GetCompanyUserMembershipAsync(
                    companyB.Id,
                    user.Id,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        f.AccessRepo.Verify(
            r =>
                r.GetCompanyUserMembershipAsync(
                    companyA.Id,
                    user.Id,
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
        f.AccessRepo.Verify(
            r =>
                r.AddCompanyUserMembershipAsync(
                    It.Is<CompanyUserMembership>(m => m.CompanyId == companyB.Id),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        f.PermissionsCache.Verify(
            c => c.TryCompleteAsync(PermissionsCacheScope.User(companyB.Id, user.Id), It.IsAny<CancellationToken>()),
            Times.Once
        );
        f.NavigationBuilder.Verify(n => n.InvalidateCache(tenant.Id, companyB.Id, user.Id));
    }

    [Fact]
    public async Task AuthorizedBranchIds_de_empresa_A_no_puede_asignarse_a_membership_de_empresa_B()
    {
        var user = NewUser();
        var tenant = NewTenant();
        var companyA = NewCompany(tenant.Id);
        var companyB = NewCompany(tenant.Id);
        var branchA = NewBranch(tenant.Id, companyA.Id);
        var f = BuildBaseFixture(user, tenant, companyB, existingMembership: null);
        f.BranchRepo.Setup(r =>
                r.GetByIdForCompanyAsync(
                    tenant.Id,
                    companyB.Id,
                    branchA.Id,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((Branch?)null);

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new UpsertCompanyUserMembershipCommand(
                tenant.Id,
                companyB.Id,
                Username,
                "User",
                AuthorizedBranchIds: new[] { branchA.Id }
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        f.CompanyUserBranchRepo.Verify(
            r => r.AddAsync(It.IsAny<CompanyUserBranch>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        f.Mediator.Verify(
            m => m.Send(It.IsAny<GetCompanyUserPreferencesQuery>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    // ── SEC-05: anti-escalamiento ───────────────────────────────────────────────────────

    [Fact]
    public async Task No_Admin_no_puede_asignarse_el_rol_Admin()
    {
        var actorAsTarget = NewUser();
        var tenant = NewTenant();
        var company = NewCompany(tenant.Id);
        var f = BuildBaseFixture(actorAsTarget, tenant, company, existingMembership: null);
        f.CurrentUser.SetupGet(x => x.UserId).Returns(actorAsTarget.Id);
        SetActor(
            f,
            new OperationalCompanyContext(
                company.Id,
                actorAsTarget.Id,
                DelegateProfileId,
                IsActiveMembership: true,
                MembershipRole: SecurityRoles.User
            )
        );

        var result = await f.BuildHandler()
            .Handle(
                new UpsertCompanyUserMembershipCommand(tenant.Id, company.Id, Username, "admin"),
                CancellationToken.None
            );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.Forbidden);
        AssertNothingPersisted(f);
    }

    [Fact]
    public async Task No_Admin_no_puede_conceder_el_rol_Admin_a_otro_usuario()
    {
        var user = NewUser();
        var tenant = NewTenant();
        var company = NewCompany(tenant.Id);
        var f = BuildBaseFixture(user, tenant, company, existingMembership: null);
        SetDelegateActor(f, company.Id, "access.company_user_memberships.view");

        var result = await f.BuildHandler()
            .Handle(
                new UpsertCompanyUserMembershipCommand(
                    tenant.Id,
                    company.Id,
                    Username,
                    SecurityRoles.Admin
                ),
                CancellationToken.None
            );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.Forbidden);
        AssertNothingPersisted(f);
    }

    [Fact]
    public async Task No_Admin_no_puede_retirar_ni_modificar_una_membership_Admin()
    {
        var user = NewUser();
        var tenant = NewTenant();
        var company = NewCompany(tenant.Id);
        var adminMembership = CompanyUserMembership.Create(
            company.Id,
            user.Id,
            SecurityRoles.Admin,
            null,
            CreatedBy
        );
        var f = BuildBaseFixture(user, tenant, company, existingMembership: adminMembership);
        SetDelegateActor(f, company.Id, "access.company_user_memberships.view");

        var result = await f.BuildHandler()
            .Handle(
                new UpsertCompanyUserMembershipCommand(tenant.Id, company.Id, Username, "User"),
                CancellationToken.None
            );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.Forbidden);
        adminMembership.Role.Should().Be(SecurityRoles.Admin);
        f.AccessRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task No_Admin_no_puede_modificar_su_propia_membership()
    {
        var self = NewUser();
        var tenant = NewTenant();
        var company = NewCompany(tenant.Id);
        var f = BuildBaseFixture(self, tenant, company, existingMembership: null);
        var richer = SetupProfile(f, tenant.Id, company.Id, true, "access.sessions.view");
        SetActor(
            f,
            new OperationalCompanyContext(
                company.Id,
                self.Id,
                DelegateProfileId,
                IsActiveMembership: true,
                MembershipRole: SecurityRoles.User
            )
        );

        var result = await f.BuildHandler()
            .Handle(
                new UpsertCompanyUserMembershipCommand(
                    tenant.Id,
                    company.Id,
                    Username,
                    "User",
                    richer.Id
                ),
                CancellationToken.None
            );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.Forbidden);
        AssertNothingPersisted(f);
    }

    [Fact]
    public async Task No_Admin_no_puede_asignar_un_perfil_con_permisos_superiores_a_los_propios()
    {
        var user = NewUser();
        var tenant = NewTenant();
        var company = NewCompany(tenant.Id);
        var f = BuildBaseFixture(user, tenant, company, existingMembership: null);
        SetDelegateActor(f, company.Id, "access.company_user_memberships.view");
        var richer = SetupProfile(
            f,
            tenant.Id,
            company.Id,
            true,
            "access.company_user_memberships.view",
            "access.sessions.view"
        );

        var result = await f.BuildHandler()
            .Handle(
                new UpsertCompanyUserMembershipCommand(
                    tenant.Id,
                    company.Id,
                    Username,
                    "User",
                    richer.Id
                ),
                CancellationToken.None
            );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.Forbidden);
        result.Error.Should().Contain("access.sessions.view");
        AssertNothingPersisted(f);
    }

    [Fact]
    public async Task No_Admin_puede_asignar_un_perfil_dentro_de_su_alcance()
    {
        var user = NewUser();
        var tenant = NewTenant();
        var company = NewCompany(tenant.Id);
        var f = BuildBaseFixture(user, tenant, company, existingMembership: null);
        SetupNoExistingPreferences(f);
        f.Mediator.Setup(m =>
                m.Send(
                    It.IsAny<CreateCompanyUserPreferencesCommand>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                Result<CompanyUserPreferencesDto>.Success(
                    Preferences(
                        Guid.NewGuid(),
                        company.Id,
                        tenant.Id,
                        CompanyUserLoginMode.AskBranch,
                        null
                    )
                )
            );
        SetDelegateActor(
            f,
            company.Id,
            "access.company_user_memberships.view",
            "access.sessions.view"
        );
        var subset = SetupProfile(f, tenant.Id, company.Id, true, "access.sessions.view");

        var result = await f.BuildHandler()
            .Handle(
                new UpsertCompanyUserMembershipCommand(
                    tenant.Id,
                    company.Id,
                    Username,
                    "User",
                    subset.Id
                ),
                CancellationToken.None
            );

        result.IsSuccess.Should().BeTrue();
        f.AccessRepo.Verify(
            r =>
                r.AddCompanyUserMembershipAsync(
                    It.Is<CompanyUserMembership>(m =>
                        m.CompanyId == company.Id && m.ProfileId == subset.Id
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task Admin_legitimo_asigna_perfil_de_su_propia_empresa()
    {
        var user = NewUser();
        var tenant = NewTenant();
        var company = NewCompany(tenant.Id);
        var f = BuildBaseFixture(user, tenant, company, existingMembership: null);
        SetupNoExistingPreferences(f);
        f.Mediator.Setup(m =>
                m.Send(
                    It.IsAny<CreateCompanyUserPreferencesCommand>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                Result<CompanyUserPreferencesDto>.Success(
                    Preferences(
                        Guid.NewGuid(),
                        company.Id,
                        tenant.Id,
                        CompanyUserLoginMode.AskBranch,
                        null
                    )
                )
            );
        var profile = SetupProfile(f, tenant.Id, company.Id, true, "access.sessions.view");

        var result = await f.BuildHandler()
            .Handle(
                new UpsertCompanyUserMembershipCommand(
                    tenant.Id,
                    company.Id,
                    Username,
                    "User",
                    profile.Id
                ),
                CancellationToken.None
            );

        result.IsSuccess.Should().BeTrue();
        f.AccessRepo.Verify(
            r =>
                r.AddCompanyUserMembershipAsync(
                    It.Is<CompanyUserMembership>(m => m.ProfileId == profile.Id),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        // Admin de la empresa: sin chequeo de alcance propio.
        f.EffectivePermissionKeys.Verify(
            k =>
                k.GetAllowedKeysAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task Perfil_de_otra_empresa_del_mismo_tenant_es_rechazado()
    {
        var user = NewUser();
        var tenant = NewTenant();
        var companyA = NewCompany(tenant.Id);
        var companyB = Company.CreateManaged(
            tenant.Id,
            "1790099999001",
            "Empresa B S.A.",
            createdBy: CreatedBy
        );
        var f = BuildBaseFixture(user, tenant, companyA, existingMembership: null);
        // El perfil existe, pero en B: para (tenant, A) no se resuelve.
        var profileOfB = SetupProfile(f, tenant.Id, companyB.Id, true, "access.sessions.view");

        var result = await f.BuildHandler()
            .Handle(
                new UpsertCompanyUserMembershipCommand(
                    tenant.Id,
                    companyA.Id,
                    Username,
                    "User",
                    profileOfB.Id
                ),
                CancellationToken.None
            );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        AssertNothingPersisted(f);
    }

    [Fact]
    public async Task Perfil_inactivo_es_rechazado()
    {
        var user = NewUser();
        var tenant = NewTenant();
        var company = NewCompany(tenant.Id);
        var f = BuildBaseFixture(user, tenant, company, existingMembership: null);
        var inactive = SetupProfile(f, tenant.Id, company.Id, active: false);

        var result = await f.BuildHandler()
            .Handle(
                new UpsertCompanyUserMembershipCommand(
                    tenant.Id,
                    company.Id,
                    Username,
                    "User",
                    inactive.Id
                ),
                CancellationToken.None
            );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        AssertNothingPersisted(f);
    }

    [Fact]
    public async Task Autoridad_resuelta_en_otra_empresa_es_rechazada()
    {
        var user = NewUser();
        var tenant = NewTenant();
        var company = NewCompany(tenant.Id);
        var f = BuildBaseFixture(user, tenant, company, existingMembership: null);
        SetActor(
            f,
            new OperationalCompanyContext(
                Guid.NewGuid(),
                CreatedBy,
                null,
                IsActiveMembership: true,
                MembershipRole: SecurityRoles.Admin
            )
        );

        var result = await f.BuildHandler()
            .Handle(
                new UpsertCompanyUserMembershipCommand(tenant.Id, company.Id, Username, "User"),
                CancellationToken.None
            );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.Forbidden);
        AssertNothingPersisted(f);
    }

    [Fact]
    public async Task Administrador_global_en_modo_operador_puede_asignar_el_rol_Admin()
    {
        var user = NewUser();
        var tenant = NewTenant();
        var company = NewCompany(tenant.Id);
        var f = BuildBaseFixture(user, tenant, company, existingMembership: null);
        SetupNoExistingPreferences(f);
        f.Mediator.Setup(m =>
                m.Send(
                    It.IsAny<CreateCompanyUserPreferencesCommand>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                Result<CompanyUserPreferencesDto>.Success(
                    Preferences(
                        Guid.NewGuid(),
                        company.Id,
                        tenant.Id,
                        CompanyUserLoginMode.AskBranch,
                        null
                    )
                )
            );
        SetActor(
            f,
            new OperationalCompanyContext(
                company.Id,
                CreatedBy,
                null,
                IsActiveMembership: false,
                MembershipRole: SecurityRoles.Admin,
                IsAuthorizedOperator: true
            )
        );

        var result = await f.BuildHandler()
            .Handle(
                new UpsertCompanyUserMembershipCommand(
                    tenant.Id,
                    company.Id,
                    Username,
                    SecurityRoles.Admin
                ),
                CancellationToken.None
            );

        result.IsSuccess.Should().BeTrue();
    }

    // ── SEC-06 (R5): un cambio de rol/perfil nunca queda persistido sin la invalidación ─────────

    private sealed record ProfileChange(
        Fixture F,
        CompanyUserMembership Membership,
        Guid OldProfileId,
        AccessProfile NewProfile,
        Tenant Tenant,
        Company Company,
        IdentityUser User
    )
    {
        public UpsertCompanyUserMembershipCommand Command(
            Guid[]? branchIds = null,
            string? loginMode = null
        ) =>
            new(
                Tenant.Id,
                Company.Id,
                Username,
                "User",
                NewProfile.Id,
                AuthorizedBranchIds: branchIds,
                LoginMode: loginMode
            );
    }

    private static ProfileChange ProfileChangeFixture()
    {
        var user = NewUser();
        var tenant = NewTenant();
        var company = NewCompany(tenant.Id);
        var oldProfileId = Guid.NewGuid();
        var membership = CompanyUserMembership.Create(
            company.Id,
            user.Id,
            "User",
            oldProfileId,
            CreatedBy
        );
        var f = BuildBaseFixture(user, tenant, company, existingMembership: membership);
        f.AccessRepo.Setup(r =>
                r.GetCompanyUserMembershipsByCompanyAsync(
                    company.Id,
                    true,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new[]
                {
                    membership,
                    CompanyUserMembership.Create(
                        company.Id,
                        Guid.NewGuid(),
                        SecurityRoles.Admin,
                        null,
                        CreatedBy
                    ),
                }
            );
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
        var newProfile = SetupProfile(f, tenant.Id, company.Id);
        return new ProfileChange(f, membership, oldProfileId, newProfile, tenant, company, user);
    }

    [Fact]
    public async Task SEC06_sucursal_inexistente_no_persiste_el_cambio_de_perfil_ni_inicia_invalidacion()
    {
        var c = ProfileChangeFixture();
        var missingBranchId = Guid.NewGuid();
        c.F.BranchRepo.Setup(r =>
                r.GetByIdForCompanyAsync(
                    c.Tenant.Id,
                    c.Company.Id,
                    missingBranchId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((Branch?)null);

        var result = await c.F.BuildHandler()
            .Handle(c.Command(branchIds: new[] { missingBranchId }), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        c.Membership.ProfileId.Should().Be(c.OldProfileId);
        AssertNothingPersisted(c.F);
        c.F.PermissionsCache.Verify(
            p => p.TryBeginAsync(It.IsAny<PermissionsCacheScope>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task SEC06_cambio_de_perfil_se_persiste_en_transaccion_entre_Begin_y_Complete()
    {
        var c = ProfileChangeFixture();
        var scope = PermissionsCacheScope.User(c.Company.Id, c.User.Id);
        var steps = new List<string>();
        c.F.PermissionsCache.Setup(p => p.TryBeginAsync(scope, It.IsAny<CancellationToken>()))
            .Callback(() => steps.Add("begin"))
            .ReturnsAsync(true);
        c.F.PermissionsCache.Setup(p => p.TryCompleteAsync(scope, It.IsAny<CancellationToken>()))
            .Callback(() => steps.Add("complete"))
            .ReturnsAsync(true);
        c.F.UnitOfWork.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .Callback(() => steps.Add("tx"))
            .Returns(Task.CompletedTask);
        c.F.AccessRepo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => steps.Add("save"))
            .Returns(Task.CompletedTask);
        c.F.UnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .Callback(() => steps.Add("commit"))
            .Returns(Task.CompletedTask);

        var result = await c.F.BuildHandler().Handle(c.Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        c.Membership.ProfileId.Should().Be(c.NewProfile.Id);
        steps.Should().Equal("begin", "tx", "save", "commit", "complete");
    }

    [Fact]
    public async Task SEC06_fallo_de_preferencias_revierte_la_transaccion_y_completa_la_invalidacion()
    {
        var c = ProfileChangeFixture();
        c.F.Mediator.Setup(m =>
                m.Send(
                    It.IsAny<UpdateCompanyUserPreferencesCommand>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                Result<CompanyUserPreferencesDto>.ValidationFailure("Preferencias inválidas.")
            );

        var result = await c.F.BuildHandler()
            .Handle(
                c.Command(loginMode: nameof(CompanyUserLoginMode.AskBranch)),
                CancellationToken.None
            );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        c.F.UnitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        c.F.UnitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        c.F.PermissionsCache.Verify(
            p =>
                p.TryCompleteAsync(
                    PermissionsCacheScope.User(c.Company.Id, c.User.Id),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task SEC06_sin_garantia_de_invalidacion_no_persiste_el_cambio_de_perfil()
    {
        var c = ProfileChangeFixture();
        c.F.PermissionsCache.Setup(p =>
                p.TryBeginAsync(It.IsAny<PermissionsCacheScope>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(false);

        var result = await c.F.BuildHandler().Handle(c.Command(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Access.PermissionsInvalidationUnavailable);
        c.Membership.ProfileId.Should().Be(c.OldProfileId);
        AssertNothingPersisted(c.F);
    }

    [Fact]
    public async Task SEC06_invalidacion_incompleta_tras_guardar_se_informa_como_fallo()
    {
        var c = ProfileChangeFixture();
        c.F.PermissionsCache.Setup(p =>
                p.TryCompleteAsync(It.IsAny<PermissionsCacheScope>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(false);

        var result = await c.F.BuildHandler().Handle(c.Command(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Access.PermissionsInvalidationIncomplete);
        c.F.UnitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SEC06_con_transaccion_ambiente_no_abre_ni_confirma_una_propia()
    {
        var c = ProfileChangeFixture();
        c.F.UnitOfWork.SetupGet(u => u.HasActiveTransaction).Returns(true);

        var result = await c.F.BuildHandler().Handle(c.Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        c.F.UnitOfWork.Verify(
            u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()),
            Times.Never
        );
        c.F.UnitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
