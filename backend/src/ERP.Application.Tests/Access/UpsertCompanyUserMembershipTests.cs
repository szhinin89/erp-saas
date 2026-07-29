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
/// modifica y se cubre aquí para detectar regresiones.
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
        public Mock<ICompanyProvisioningService> CompanyProvisioning { get; } = new();
        public Mock<IPermissionsCacheInvalidator> PermissionsCache { get; } = new();
        public Mock<INavigationBuilder> NavigationBuilder { get; } = new();
        public Mock<IBranchRepository> BranchRepo { get; } = new();
        public Mock<ICompanyUserBranchRepository> CompanyUserBranchRepo { get; } = new();
        public Mock<IMediator> Mediator { get; } = new();

        public Fixture()
        {
            CurrentUser.SetupGet(x => x.UserId).Returns(CreatedBy);
        }

        public UpsertCompanyUserMembershipHandler BuildHandler() =>
            new(
                AccessRepo.Object,
                CurrentUser.Object,
                TenantRepo.Object,
                CompanyProvisioning.Object,
                PermissionsCache.Object,
                NavigationBuilder.Object,
                BranchRepo.Object,
                CompanyUserBranchRepo.Object,
                Mediator.Object
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
        f.CompanyProvisioning.Setup(s =>
                s.EnsureDefaultCompanyAsync(tenant, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(company);
        f.AccessRepo.Setup(r => r.GetUserByUsernameAsync(Username, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        f.AccessRepo.Setup(r =>
                r.GetCompanyUserMembershipAsync(company.Id, user.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(existingMembership);
        return f;
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
            new UpsertCompanyUserMembershipCommand(tenant.Id, Username, "User"),
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
        f.BranchRepo.Setup(r => r.GetByIdAsync(tenant.Id, branch.Id, It.IsAny<CancellationToken>()))
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
                r.GetByIdAsync(tenant.Id, missingBranchId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((Branch?)null);

        var handler = f.BuildHandler();
        var command = new UpsertCompanyUserMembershipCommand(
            tenant.Id,
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
        f.BranchRepo.Setup(r => r.GetByIdAsync(tenant.Id, branch.Id, It.IsAny<CancellationToken>()))
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
            new UpsertCompanyUserMembershipCommand(tenant.Id, Username, "Admin"),
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
            new UpsertCompanyUserMembershipCommand(tenant.Id, Username, "User"),
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
            new UpsertCompanyUserMembershipCommand(tenant.Id, Username, "User"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        membership.Role.Should().Be("User");
        f.AccessRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
