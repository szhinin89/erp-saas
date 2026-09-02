using ERP.Application.Access.UseCases.UpdateCompanyUserBranchesAdmin;
using ERP.Application.Common;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Branches.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Access;

/// <summary>
/// Fase I-B: UpdateCompanyUserBranchesAdminHandler reemplaza la autorización de sucursales de una
/// membresía todo-o-nada. CompanyUserBranch sigue siendo la única fuente de verdad — estos tests
/// no tocan Membership/Preferences/IdentityUser.
/// </summary>
public sealed class UpdateCompanyUserBranchesAdminHandlerTests
{
    private static readonly Guid CreatedBy = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CurrentCompanyId = Guid.NewGuid();
    private static readonly Guid OtherCompanyId = Guid.NewGuid();

    private static CompanyUserMembership Membership(Guid companyId) =>
        CompanyUserMembership.Create(companyId, Guid.NewGuid(), "User", null, CreatedBy);

    private static Branch NewBranch(Guid companyId, string name) =>
        Branch.Create(
            TenantId,
            name,
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

    private static Branch NewInactiveBranch(Guid companyId, string name)
    {
        var branch = NewBranch(companyId, name);
        branch.Disable(CreatedBy);
        return branch;
    }

    private sealed class CurrentCompanyStub : ICurrentCompany
    {
        public Guid CompanyId => CurrentCompanyId;
        public bool IsAuthenticated => true;
        public bool HasCompanyContext => true;
    }

    private sealed class CurrentTenantStub : ICurrentTenant
    {
        public Guid TenantId => UpdateCompanyUserBranchesAdminHandlerTests.TenantId;
        public string? Slug => null;
    }

    private sealed class CurrentUserStub : ICurrentUser
    {
        public Guid UserId => CreatedBy;
        public bool IsAuthenticated => true;
        public string? Username => null;
        public string? Email => null;
        public string? FullName => null;
        public string? Role => null;
    }

    private sealed class Fixture
    {
        public Mock<IAccessRepository> AccessRepo { get; } = new();
        public Mock<IBranchRepository> BranchRepo { get; } = new();
        public Mock<ICompanyUserBranchRepository> CompanyUserBranchRepo { get; } = new();

        public UpdateCompanyUserBranchesAdminHandler BuildHandler() =>
            new(
                AccessRepo.Object,
                new CurrentCompanyStub(),
                new CurrentTenantStub(),
                new CurrentUserStub(),
                BranchRepo.Object,
                CompanyUserBranchRepo.Object
            );
    }

    private static Fixture BuildBaseFixture(
        CompanyUserMembership membership,
        IReadOnlyList<Branch> companyBranches,
        IReadOnlyList<CompanyUserBranch> existingAuthorizations
    )
    {
        var f = new Fixture();
        f.AccessRepo.Setup(r =>
                r.GetCompanyUserMembershipByIdAsync(membership.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(membership);
        foreach (var branch in companyBranches.Where(b => b.CompanyId == membership.CompanyId))
            f.BranchRepo.Setup(r =>
                    r.GetByIdForCompanyAsync(
                        TenantId,
                        membership.CompanyId,
                        branch.Id,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(branch);
        f.BranchRepo.Setup(r =>
                r.GetByCompanyAsync(
                    TenantId,
                    membership.CompanyId,
                    true,
                    null,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(companyBranches.Where(b => b.CompanyId == membership.CompanyId && b.IsActive).ToList());
        f.CompanyUserBranchRepo.Setup(r =>
                r.GetByMembershipAsync(membership.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(existingAuthorizations);
        return f;
    }

    [Fact]
    public async Task Reemplaza_A_B_C_por_A_D_reactivando_A_desactivando_B_y_C_y_creando_D()
    {
        var membership = Membership(CurrentCompanyId);
        var branchA = NewBranch(CurrentCompanyId, "A");
        var branchB = NewBranch(CurrentCompanyId, "B");
        var branchC = NewBranch(CurrentCompanyId, "C");
        var branchD = NewBranch(CurrentCompanyId, "D");

        var authA = CompanyUserBranch.Create(
            TenantId,
            CurrentCompanyId,
            membership.Id,
            branchA.Id,
            CreatedBy
        );
        var authB = CompanyUserBranch.Create(
            TenantId,
            CurrentCompanyId,
            membership.Id,
            branchB.Id,
            CreatedBy
        );
        var authC = CompanyUserBranch.Create(
            TenantId,
            CurrentCompanyId,
            membership.Id,
            branchC.Id,
            CreatedBy
        );

        var f = BuildBaseFixture(
            membership,
            new[] { branchA, branchB, branchC, branchD },
            new[] { authA, authB, authC }
        );

        CompanyUserBranch? addedAuthorization = null;
        f.CompanyUserBranchRepo.Setup(r =>
                r.AddAsync(It.IsAny<CompanyUserBranch>(), It.IsAny<CancellationToken>())
            )
            .Callback<CompanyUserBranch, CancellationToken>(
                (entity, _) => addedAuthorization = entity
            )
            .Returns(Task.CompletedTask);

        var handler = f.BuildHandler();
        var command = new UpdateCompanyUserBranchesAdminCommand(
            membership.Id,
            new[] { branchA.Id, branchD.Id }
        );
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        authA.IsActive.Should().BeTrue("A sigue solicitada — se reactiva/permanece activa");
        authB.IsActive.Should().BeFalse("B ya no está en la lista solicitada — se desactiva");
        authC.IsActive.Should().BeFalse("C ya no está en la lista solicitada — se desactiva");
        addedAuthorization.Should().NotBeNull("D es nueva — se crea una fila CompanyUserBranch");
        addedAuthorization!.BranchId.Should().Be(branchD.Id);
        addedAuthorization.IsActive.Should().BeTrue();
        f.CompanyUserBranchRepo.Verify(
            r => r.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once
        );

        result.Value!.Branches.Single(b => b.BranchId == branchA.Id).Authorized.Should().BeTrue();
        result.Value.Branches.Single(b => b.BranchId == branchD.Id).Authorized.Should().BeTrue();
        result.Value.Branches.Single(b => b.BranchId == branchB.Id).Authorized.Should().BeFalse();
        result.Value.Branches.Single(b => b.BranchId == branchC.Id).Authorized.Should().BeFalse();
    }

    [Fact]
    public async Task Reactivar_una_autorizacion_previamente_desactivada_no_crea_fila_duplicada()
    {
        var membership = Membership(CurrentCompanyId);
        var branchA = NewBranch(CurrentCompanyId, "A");
        var authA = CompanyUserBranch.Create(
            TenantId,
            CurrentCompanyId,
            membership.Id,
            branchA.Id,
            CreatedBy
        );
        authA.Deactivate(CreatedBy);

        var f = BuildBaseFixture(membership, new[] { branchA }, new[] { authA });

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new UpdateCompanyUserBranchesAdminCommand(membership.Id, new[] { branchA.Id }),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        authA.IsActive.Should().BeTrue();
        f.CompanyUserBranchRepo.Verify(
            r => r.AddAsync(It.IsAny<CompanyUserBranch>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Lista_vacia_desactiva_todas_las_autorizaciones_existentes()
    {
        var membership = Membership(CurrentCompanyId);
        var branchA = NewBranch(CurrentCompanyId, "A");
        var authA = CompanyUserBranch.Create(
            TenantId,
            CurrentCompanyId,
            membership.Id,
            branchA.Id,
            CreatedBy
        );

        var f = BuildBaseFixture(membership, new[] { branchA }, new[] { authA });

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new UpdateCompanyUserBranchesAdminCommand(membership.Id, Array.Empty<Guid>()),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        authA.IsActive.Should().BeFalse();
        result.Value!.Branches.Should().OnlyContain(b => !b.Authorized);
    }

    [Fact]
    public async Task Sucursal_inexistente_devuelve_ValidationFailure_y_no_persiste_ningun_cambio()
    {
        var membership = Membership(CurrentCompanyId);
        var branchA = NewBranch(CurrentCompanyId, "A");
        var missingBranchId = Guid.NewGuid();
        var authA = CompanyUserBranch.Create(
            TenantId,
            CurrentCompanyId,
            membership.Id,
            branchA.Id,
            CreatedBy
        );

        var f = BuildBaseFixture(membership, new[] { branchA }, new[] { authA });
        f.BranchRepo.Setup(r =>
                r.GetByIdForCompanyAsync(
                    TenantId,
                    CurrentCompanyId,
                    missingBranchId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((Branch?)null);

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new UpdateCompanyUserBranchesAdminCommand(membership.Id, new[] { missingBranchId }),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        authA
            .IsActive.Should()
            .BeTrue("no debe quedar estado parcial: A conserva su autorización previa");
        f.CompanyUserBranchRepo.Verify(
            r => r.AddAsync(It.IsAny<CompanyUserBranch>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        f.CompanyUserBranchRepo.Verify(
            r => r.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Sucursal_de_otra_empresa_devuelve_ValidationFailure_y_no_persiste_ningun_cambio()
    {
        var membership = Membership(CurrentCompanyId);
        var foreignBranch = NewBranch(OtherCompanyId, "Ajena");

        var f = BuildBaseFixture(
            membership,
            new[] { foreignBranch },
            Array.Empty<CompanyUserBranch>()
        );

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new UpdateCompanyUserBranchesAdminCommand(membership.Id, new[] { foreignBranch.Id }),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        f.CompanyUserBranchRepo.Verify(
            r => r.AddAsync(It.IsAny<CompanyUserBranch>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        f.CompanyUserBranchRepo.Verify(
            r => r.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Sucursal_inactiva_devuelve_ValidationFailure_y_no_persiste_ningun_cambio()
    {
        var membership = Membership(CurrentCompanyId);
        var inactiveBranch = NewInactiveBranch(CurrentCompanyId, "Cerrada");

        var f = BuildBaseFixture(
            membership,
            new[] { inactiveBranch },
            Array.Empty<CompanyUserBranch>()
        );

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new UpdateCompanyUserBranchesAdminCommand(membership.Id, new[] { inactiveBranch.Id }),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        f.CompanyUserBranchRepo.Verify(
            r => r.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Membresia_de_otra_empresa_devuelve_NotFound_y_no_valida_sucursales()
    {
        var membership = Membership(OtherCompanyId);
        var f = new Fixture();
        f.AccessRepo.Setup(r =>
                r.GetCompanyUserMembershipByIdAsync(membership.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(membership);

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new UpdateCompanyUserBranchesAdminCommand(membership.Id, new[] { Guid.NewGuid() }),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        f.BranchRepo.Verify(
            r =>
                r.GetByIdForCompanyAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task Membresia_revocada_devuelve_Forbidden_y_no_toca_ninguna_autorizacion()
    {
        var membership = Membership(CurrentCompanyId);
        membership.Deactivate(CreatedBy);
        var branchA = NewBranch(CurrentCompanyId, "A");
        var authA = CompanyUserBranch.Create(
            TenantId,
            CurrentCompanyId,
            membership.Id,
            branchA.Id,
            CreatedBy
        );

        var f = BuildBaseFixture(membership, new[] { branchA }, new[] { authA });

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new UpdateCompanyUserBranchesAdminCommand(membership.Id, new[] { branchA.Id }),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.Forbidden);
        f.BranchRepo.Verify(
            r =>
                r.GetByIdForCompanyAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
        f.CompanyUserBranchRepo.Verify(
            r => r.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Membresia_inexistente_devuelve_NotFound()
    {
        var missingId = Guid.NewGuid();
        var f = new Fixture();
        f.AccessRepo.Setup(r =>
                r.GetCompanyUserMembershipByIdAsync(missingId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((CompanyUserMembership?)null);

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new UpdateCompanyUserBranchesAdminCommand(missingId, Array.Empty<Guid>()),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }
}
