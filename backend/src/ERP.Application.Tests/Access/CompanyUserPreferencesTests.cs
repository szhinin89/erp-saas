using ERP.Application.Access.UseCases.CreateCompanyUserPreferences;
using ERP.Application.Access.UseCases.GetCompanyUserPreferences;
using ERP.Application.Access.UseCases.UpdateCompanyUserPreferences;
using ERP.Application.Common;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Enums;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Branches.Interfaces;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Access;

public sealed class CreateCompanyUserPreferencesCommandValidatorTests
{
    private static readonly CreateCompanyUserPreferencesCommandValidator Validator = new();

    private static CreateCompanyUserPreferencesCommand ValidCommand() =>
        new(Guid.NewGuid(), nameof(CompanyUserLoginMode.AskBranch), null);

    [Fact]
    public void Command_valido_no_tiene_errores()
    {
        Validator.Validate(ValidCommand()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void CompanyUserMembershipId_vacio_es_invalido()
    {
        var result = Validator.Validate(
            ValidCommand() with
            {
                CompanyUserMembershipId = Guid.Empty,
            }
        );
        result
            .Errors.Should()
            .Contain(e =>
                e.PropertyName
                == nameof(CreateCompanyUserPreferencesCommand.CompanyUserMembershipId)
            );
    }

    [Fact]
    public void LoginMode_desconocido_es_invalido()
    {
        var result = Validator.Validate(ValidCommand() with { LoginMode = "NoExiste" });
        result
            .Errors.Should()
            .Contain(e => e.PropertyName == nameof(CreateCompanyUserPreferencesCommand.LoginMode));
    }

    [Fact]
    public void DefaultBranchId_Guid_Empty_es_invalido()
    {
        var result = Validator.Validate(ValidCommand() with { DefaultBranchId = Guid.Empty });
        result
            .Errors.Should()
            .Contain(e =>
                e.PropertyName == nameof(CreateCompanyUserPreferencesCommand.DefaultBranchId)
            );
    }
}

public sealed class UpdateCompanyUserPreferencesCommandValidatorTests
{
    private static readonly UpdateCompanyUserPreferencesCommandValidator Validator = new();

    private static UpdateCompanyUserPreferencesCommand ValidCommand() =>
        new(Guid.NewGuid(), nameof(CompanyUserLoginMode.AskBranch), null);

    [Fact]
    public void Command_valido_no_tiene_errores()
    {
        Validator.Validate(ValidCommand()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void LoginMode_desconocido_es_invalido()
    {
        var result = Validator.Validate(ValidCommand() with { LoginMode = "NoExiste" });
        result
            .Errors.Should()
            .Contain(e => e.PropertyName == nameof(UpdateCompanyUserPreferencesCommand.LoginMode));
    }
}

/// <summary>
/// CompanyUserPreferencesMapper es internal (mismo criterio que AccessSessionMapper) — se
/// verifica su fidelidad de mapeo indirectamente a través del DTO devuelto por un handler real,
/// campo por campo, en vez de romper el encapsulamiento del mapper para el test.
/// </summary>
public sealed class CompanyUserPreferencesMapperTests
{
    [Fact]
    public async Task El_Dto_devuelto_por_el_handler_refleja_todos_los_campos_de_la_entidad()
    {
        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var membershipId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var entity = CompanyUserPreferences.Create(
            tenantId,
            companyId,
            membershipId,
            CompanyUserLoginMode.DirectToDefault,
            branchId,
            Guid.NewGuid()
        );
        var repo = new Mock<ICompanyUserPreferencesRepository>();
        repo.Setup(r => r.GetByMembershipAsync(membershipId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var handler = new GetCompanyUserPreferencesHandler(repo.Object);
        var result = await handler.Handle(
            new GetCompanyUserPreferencesQuery(membershipId),
            CancellationToken.None
        );

        var dto = result.Value!;
        dto.Id.Should().Be(entity.Id);
        dto.TenantId.Should().Be(tenantId);
        dto.CompanyId.Should().Be(companyId);
        dto.CompanyUserMembershipId.Should().Be(membershipId);
        dto.DefaultBranchId.Should().Be(branchId);
        dto.LoginMode.Should().Be(nameof(CompanyUserLoginMode.DirectToDefault));
    }
}

public sealed class CreateCompanyUserPreferencesHandlerTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid ActorUserId = Guid.NewGuid();

    // CompanyUserMembership.Create genera su propio Id — se usa membership.Id (nunca un Guid
    // constante inventado) para mantener consistencia con lo que devolvería el repositorio real.
    private static CompanyUserMembership Membership() =>
        CompanyUserMembership.Create(CompanyId, Guid.NewGuid(), "User", null, Guid.NewGuid());

    private static Company CompanyEntity(Guid tenantId) =>
        Company.CreateManaged(tenantId, "1790012345001", "Test S.A.", createdBy: Guid.NewGuid());

    private static Branch BranchEntity(Guid tenantId) =>
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
            Guid.NewGuid(),
            companyId: CompanyId
        );

    private sealed class Fixture
    {
        public Mock<IAccessRepository> AccessRepository { get; } = new();
        public Mock<ICompanyRepository> CompanyRepository { get; } = new();
        public Mock<IBranchRepository> BranchRepository { get; } = new();
        public Mock<ICompanyUserBranchRepository> CompanyUserBranchRepository { get; } = new();
        public Mock<ICompanyUserPreferencesRepository> PreferencesRepository { get; } = new();
        public Mock<ICurrentUser> CurrentUser { get; } = new();

        public Fixture()
        {
            CurrentUser.SetupGet(x => x.UserId).Returns(ActorUserId);
        }

        public CreateCompanyUserPreferencesHandler BuildHandler() =>
            new(
                AccessRepository.Object,
                CompanyRepository.Object,
                BranchRepository.Object,
                CompanyUserBranchRepository.Object,
                PreferencesRepository.Object,
                CurrentUser.Object
            );
    }

    private static CreateCompanyUserPreferencesCommand Command(
        Guid membershipId,
        string loginMode = nameof(CompanyUserLoginMode.AskBranch),
        Guid? defaultBranchId = null
    ) => new(membershipId, loginMode, defaultBranchId);

    [Fact]
    public async Task Creacion_valida_persiste_y_devuelve_el_dto()
    {
        var f = new Fixture();
        var membership = Membership();
        var tenantId = Guid.NewGuid();
        f.AccessRepository.Setup(r =>
                r.GetCompanyUserMembershipByIdAsync(membership.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(membership);
        f.PreferencesRepository.Setup(r =>
                r.ExistsAsync(membership.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(false);
        f.CompanyRepository.Setup(r => r.GetByIdAsync(CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CompanyEntity(tenantId));

        var handler = f.BuildHandler();
        var result = await handler.Handle(Command(membership.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.CompanyUserMembershipId.Should().Be(membership.Id);
        result.Value.TenantId.Should().Be(tenantId);
        result.Value.LoginMode.Should().Be(nameof(CompanyUserLoginMode.AskBranch));

        f.PreferencesRepository.Verify(
            r =>
                r.AddAsync(
                    It.Is<CompanyUserPreferences>(p =>
                        p.CompanyUserMembershipId == membership.Id && p.CreatedBy == ActorUserId
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        f.PreferencesRepository.Verify(
            r => r.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Duplicado_devuelve_Conflict_y_no_persiste()
    {
        var f = new Fixture();
        var membership = Membership();
        f.AccessRepository.Setup(r =>
                r.GetCompanyUserMembershipByIdAsync(membership.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(membership);
        f.PreferencesRepository.Setup(r =>
                r.ExistsAsync(membership.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(true);

        var handler = f.BuildHandler();
        var result = await handler.Handle(Command(membership.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.Conflict);
        f.PreferencesRepository.Verify(
            r => r.AddAsync(It.IsAny<CompanyUserPreferences>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Membership_inexistente_devuelve_NotFound()
    {
        var f = new Fixture();
        var missingMembershipId = Guid.NewGuid();
        f.AccessRepository.Setup(r =>
                r.GetCompanyUserMembershipByIdAsync(
                    missingMembershipId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((CompanyUserMembership?)null);

        var handler = f.BuildHandler();
        var result = await handler.Handle(Command(missingMembershipId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        f.PreferencesRepository.Verify(
            r => r.ExistsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Branch_inexistente_devuelve_NotFound()
    {
        var f = new Fixture();
        var membership = Membership();
        var tenantId = Guid.NewGuid();
        f.AccessRepository.Setup(r =>
                r.GetCompanyUserMembershipByIdAsync(membership.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(membership);
        f.PreferencesRepository.Setup(r =>
                r.ExistsAsync(membership.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(false);
        f.CompanyRepository.Setup(r => r.GetByIdAsync(CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CompanyEntity(tenantId));
        f.BranchRepository.Setup(r =>
                r.GetByIdAsync(tenantId, BranchId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((Branch?)null);

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            Command(membership.Id, nameof(CompanyUserLoginMode.DirectToDefault), BranchId),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        f.PreferencesRepository.Verify(
            r => r.AddAsync(It.IsAny<CompanyUserPreferences>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    /// <summary>
    /// Validación crítica del roadmap: CompanyUserBranch es la única fuente de autorización de
    /// sucursal. Aunque la sucursal exista realmente, si no está autorizada para esta membresía
    /// vía CompanyUserBranch, la creación debe rechazarse — nunca insertar la autorización ni
    /// asumir Branch.IsMainBranch.
    /// </summary>
    [Fact]
    public async Task Branch_no_autorizada_para_la_membresia_devuelve_ValidationFailure()
    {
        var f = new Fixture();
        var membership = Membership();
        var tenantId = Guid.NewGuid();
        f.AccessRepository.Setup(r =>
                r.GetCompanyUserMembershipByIdAsync(membership.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(membership);
        f.PreferencesRepository.Setup(r =>
                r.ExistsAsync(membership.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(false);
        f.CompanyRepository.Setup(r => r.GetByIdAsync(CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CompanyEntity(tenantId));
        f.BranchRepository.Setup(r =>
                r.GetByIdAsync(tenantId, BranchId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(BranchEntity(tenantId));
        f.CompanyUserBranchRepository.Setup(r =>
                r.ExistsAsync(membership.Id, BranchId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(false);

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            Command(membership.Id, nameof(CompanyUserLoginMode.DirectToDefault), BranchId),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        f.CompanyUserBranchRepository.Verify(
            r => r.AddAsync(It.IsAny<CompanyUserBranch>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        f.PreferencesRepository.Verify(
            r => r.AddAsync(It.IsAny<CompanyUserPreferences>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    /// <summary>
    /// Fase H — hallazgo de auditoría: IBranchRepository.GetByIdAsync no filtra por IsActive.
    /// Sin este chequeo, una sucursal desactivada (soft-delete) seguía aceptándose como
    /// DefaultBranchId. Se prueba explícitamente que una sucursal inactiva se rechaza aunque
    /// exista y esté autorizada.
    /// </summary>
    [Fact]
    public async Task Branch_inactiva_devuelve_ValidationFailure_aunque_este_autorizada()
    {
        var f = new Fixture();
        var membership = Membership();
        var tenantId = Guid.NewGuid();
        var inactiveBranch = BranchEntity(tenantId);
        inactiveBranch.Disable(Guid.NewGuid());
        f.AccessRepository.Setup(r =>
                r.GetCompanyUserMembershipByIdAsync(membership.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(membership);
        f.PreferencesRepository.Setup(r =>
                r.ExistsAsync(membership.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(false);
        f.CompanyRepository.Setup(r => r.GetByIdAsync(CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CompanyEntity(tenantId));
        f.BranchRepository.Setup(r =>
                r.GetByIdAsync(tenantId, BranchId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(inactiveBranch);
        f.CompanyUserBranchRepository.Setup(r =>
                r.ExistsAsync(membership.Id, BranchId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(true);

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            Command(membership.Id, nameof(CompanyUserLoginMode.DirectToDefault), BranchId),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        f.PreferencesRepository.Verify(
            r => r.AddAsync(It.IsAny<CompanyUserPreferences>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task DirectToDefault_sin_DefaultBranchId_devuelve_ValidationFailure()
    {
        var f = new Fixture();
        var membership = Membership();
        var tenantId = Guid.NewGuid();
        f.AccessRepository.Setup(r =>
                r.GetCompanyUserMembershipByIdAsync(membership.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(membership);
        f.PreferencesRepository.Setup(r =>
                r.ExistsAsync(membership.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(false);
        f.CompanyRepository.Setup(r => r.GetByIdAsync(CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CompanyEntity(tenantId));

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            Command(
                membership.Id,
                nameof(CompanyUserLoginMode.DirectToDefault),
                defaultBranchId: null
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        f.BranchRepository.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task AskBranch_con_DefaultBranchId_null_es_valido()
    {
        var f = new Fixture();
        var membership = Membership();
        var tenantId = Guid.NewGuid();
        f.AccessRepository.Setup(r =>
                r.GetCompanyUserMembershipByIdAsync(membership.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(membership);
        f.PreferencesRepository.Setup(r =>
                r.ExistsAsync(membership.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(false);
        f.CompanyRepository.Setup(r => r.GetByIdAsync(CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CompanyEntity(tenantId));

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            Command(membership.Id, nameof(CompanyUserLoginMode.AskBranch), defaultBranchId: null),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.DefaultBranchId.Should().BeNull();
    }
}

public sealed class UpdateCompanyUserPreferencesHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid MembershipId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid ActorUserId = Guid.NewGuid();

    private static CompanyUserPreferences Existing(
        CompanyUserLoginMode loginMode = CompanyUserLoginMode.AskBranch,
        Guid? defaultBranchId = null
    ) =>
        CompanyUserPreferences.Create(
            TenantId,
            CompanyId,
            MembershipId,
            loginMode,
            defaultBranchId,
            Guid.NewGuid()
        );

    private static Branch BranchEntity() =>
        Branch.Create(
            TenantId,
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
            Guid.NewGuid(),
            companyId: CompanyId
        );

    private sealed class Fixture
    {
        public Mock<ICompanyUserPreferencesRepository> PreferencesRepository { get; } = new();
        public Mock<IBranchRepository> BranchRepository { get; } = new();
        public Mock<ICompanyUserBranchRepository> CompanyUserBranchRepository { get; } = new();
        public Mock<ICurrentUser> CurrentUser { get; } = new();

        public Fixture()
        {
            CurrentUser.SetupGet(x => x.UserId).Returns(ActorUserId);
        }

        public UpdateCompanyUserPreferencesHandler BuildHandler() =>
            new(
                PreferencesRepository.Object,
                BranchRepository.Object,
                CompanyUserBranchRepository.Object,
                CurrentUser.Object
            );
    }

    private static UpdateCompanyUserPreferencesCommand Command(
        string loginMode = nameof(CompanyUserLoginMode.AskBranch),
        Guid? defaultBranchId = null
    ) => new(MembershipId, loginMode, defaultBranchId);

    [Fact]
    public async Task Actualizacion_valida_cambia_sucursal_y_modo_sin_tocar_CompanyUserMembership()
    {
        var f = new Fixture();
        var existing = Existing();
        f.PreferencesRepository.Setup(r =>
                r.GetByMembershipAsync(MembershipId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(existing);
        f.BranchRepository.Setup(r =>
                r.GetByIdAsync(TenantId, BranchId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(BranchEntity());
        f.CompanyUserBranchRepository.Setup(r =>
                r.ExistsAsync(MembershipId, BranchId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(true);

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            Command(nameof(CompanyUserLoginMode.DirectToDefault), BranchId),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        existing.DefaultBranchId.Should().Be(BranchId);
        existing.LoginMode.Should().Be(CompanyUserLoginMode.DirectToDefault);
        existing.UpdatedBy.Should().Be(ActorUserId);
        f.PreferencesRepository.Verify(
            r => r.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Preferencias_inexistentes_devuelve_NotFound_y_no_llama_a_SaveChanges()
    {
        var f = new Fixture();
        f.PreferencesRepository.Setup(r =>
                r.GetByMembershipAsync(MembershipId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((CompanyUserPreferences?)null);

        var handler = f.BuildHandler();
        var result = await handler.Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        f.PreferencesRepository.Verify(
            r => r.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Branch_no_autorizada_devuelve_ValidationFailure_y_no_persiste()
    {
        var f = new Fixture();
        var existing = Existing();
        f.PreferencesRepository.Setup(r =>
                r.GetByMembershipAsync(MembershipId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(existing);
        f.BranchRepository.Setup(r =>
                r.GetByIdAsync(TenantId, BranchId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(BranchEntity());
        f.CompanyUserBranchRepository.Setup(r =>
                r.ExistsAsync(MembershipId, BranchId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(false);

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            Command(nameof(CompanyUserLoginMode.DirectToDefault), BranchId),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        existing.DefaultBranchId.Should().BeNull();
        f.PreferencesRepository.Verify(
            r => r.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    /// <summary>Fase H — mismo hallazgo que en CreateCompanyUserPreferencesHandlerTests: una
    /// sucursal desactivada nunca debe aceptarse como DefaultBranchId, aunque exista y esté
    /// autorizada en CompanyUserBranch.</summary>
    [Fact]
    public async Task Branch_inactiva_devuelve_ValidationFailure_y_no_persiste()
    {
        var f = new Fixture();
        var existing = Existing();
        var inactiveBranch = BranchEntity();
        inactiveBranch.Disable(Guid.NewGuid());
        f.PreferencesRepository.Setup(r =>
                r.GetByMembershipAsync(MembershipId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(existing);
        f.BranchRepository.Setup(r =>
                r.GetByIdAsync(TenantId, BranchId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(inactiveBranch);
        f.CompanyUserBranchRepository.Setup(r =>
                r.ExistsAsync(MembershipId, BranchId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(true);

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            Command(nameof(CompanyUserLoginMode.DirectToDefault), BranchId),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        f.PreferencesRepository.Verify(
            r => r.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never
        );
    }
}

public sealed class GetCompanyUserPreferencesHandlerTests
{
    [Fact]
    public async Task Devuelve_el_dto_cuando_existen_preferencias()
    {
        var membershipId = Guid.NewGuid();
        var entity = CompanyUserPreferences.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            membershipId,
            CompanyUserLoginMode.AskBranch,
            null,
            Guid.NewGuid()
        );
        var repo = new Mock<ICompanyUserPreferencesRepository>();
        repo.Setup(r => r.GetByMembershipAsync(membershipId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var handler = new GetCompanyUserPreferencesHandler(repo.Object);
        var result = await handler.Handle(
            new GetCompanyUserPreferencesQuery(membershipId),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.CompanyUserMembershipId.Should().Be(membershipId);
    }

    [Fact]
    public async Task Devuelve_null_sin_crear_nada_cuando_no_existen_preferencias()
    {
        var membershipId = Guid.NewGuid();
        var repo = new Mock<ICompanyUserPreferencesRepository>();
        repo.Setup(r => r.GetByMembershipAsync(membershipId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompanyUserPreferences?)null);

        var handler = new GetCompanyUserPreferencesHandler(repo.Object);
        var result = await handler.Handle(
            new GetCompanyUserPreferencesQuery(membershipId),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
        repo.Verify(
            r => r.AddAsync(It.IsAny<CompanyUserPreferences>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }
}
