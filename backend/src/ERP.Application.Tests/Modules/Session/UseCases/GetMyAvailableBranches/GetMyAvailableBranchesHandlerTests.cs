using ERP.Application.Common;
using ERP.Application.Modules.Session.UseCases.GetMyAvailableBranches;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Enums;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Branches.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Modules.Session.UseCases.GetMyAvailableBranches;

public sealed class GetMyAvailableBranchesHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private sealed class Fixture
    {
        public Mock<ICurrentUser> CurrentUser { get; } = new();
        public Mock<ICurrentCompany> CurrentCompany { get; } = new();
        public Mock<ICurrentTenant> CurrentTenant { get; } = new();
        public Mock<IAccessRepository> AccessRepository { get; } = new();
        public Mock<IBranchRepository> BranchRepository { get; } = new();
        public Mock<ICompanyUserBranchRepository> CompanyUserBranchRepository { get; } = new();
        public Mock<ICompanyUserPreferencesRepository> PreferencesRepository { get; } = new();

        public Fixture()
        {
            CurrentUser.Setup(u => u.UserId).Returns(UserId);
            CurrentCompany.Setup(c => c.CompanyId).Returns(CompanyId);
            CurrentTenant.Setup(t => t.TenantId).Returns(TenantId);
        }

        public GetMyAvailableBranchesHandler BuildHandler() =>
            new(
                CurrentUser.Object,
                CurrentCompany.Object,
                CurrentTenant.Object,
                AccessRepository.Object,
                BranchRepository.Object,
                CompanyUserBranchRepository.Object,
                PreferencesRepository.Object
            );
    }

    private static Branch CreateBranch(string name, bool isMain, Guid? companyId = null) =>
        Branch.Create(
            tenantId: TenantId,
            name: name,
            address: "Av. Siempre Viva 123",
            code: "B01",
            description: null,
            reference: null,
            postalCode: null,
            phone: null,
            secondaryPhone: null,
            email: null,
            website: null,
            managerName: null,
            managerPosition: null,
            managerEmail: null,
            managerPhone: null,
            countryId: null,
            provinceId: null,
            cantonId: null,
            parishId: null,
            latitude: null,
            longitude: null,
            openingDate: null,
            internalNotes: null,
            isMainBranch: isMain,
            createdBy: Guid.NewGuid(),
            companyId: companyId ?? CompanyId
        );

    [Fact]
    public async Task Sin_membership_activa_rechaza()
    {
        var f = new Fixture();
        f.AccessRepository.Setup(r =>
                r.GetCompanyUserMembershipAsync(CompanyId, UserId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((CompanyUserMembership?)null);

        var result = await f.BuildHandler()
            .Handle(new GetMyAvailableBranchesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("No tiene acceso a esta empresa.");
    }

    [Fact]
    public async Task Retorna_solo_sucursales_activas_y_autorizadas_ordenadas_con_principal_primero()
    {
        var f = new Fixture();
        var membership = CompanyUserMembership.Create(
            CompanyId,
            UserId,
            "Cajero",
            null,
            Guid.NewGuid()
        );
        f.AccessRepository.Setup(r =>
                r.GetCompanyUserMembershipAsync(CompanyId, UserId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(membership);

        var mainBranch = CreateBranch("Matriz", isMain: true);
        var northBranch = CreateBranch("Norte", isMain: false);
        var revokedBranch = CreateBranch("Sur (revocada)", isMain: false);
        var otherCompanyBranch = CreateBranch(
            "Otra empresa",
            isMain: false,
            companyId: Guid.NewGuid()
        );

        f.BranchRepository.Setup(r =>
                r.GetAsync(TenantId, true, null, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new[] { mainBranch, northBranch, revokedBranch, otherCompanyBranch });

        var authorizations = new[]
        {
            CompanyUserBranch.Create(
                TenantId,
                CompanyId,
                membership.Id,
                mainBranch.Id,
                Guid.NewGuid()
            ),
            CompanyUserBranch.Create(
                TenantId,
                CompanyId,
                membership.Id,
                northBranch.Id,
                Guid.NewGuid()
            ),
            CompanyUserBranch.Create(
                TenantId,
                CompanyId,
                membership.Id,
                revokedBranch.Id,
                Guid.NewGuid()
            ),
        };
        authorizations[2].Deactivate(Guid.NewGuid());
        f.CompanyUserBranchRepository.Setup(r =>
                r.GetByMembershipAsync(membership.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(authorizations);

        f.PreferencesRepository.Setup(r =>
                r.GetByMembershipAsync(membership.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((CompanyUserPreferences?)null);

        var result = await f.BuildHandler()
            .Handle(new GetMyAvailableBranchesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Branches.Should().HaveCount(2);
        result.Value!.Branches[0].Name.Should().Be("Matriz");
        result.Value!.Branches[0].IsMainBranch.Should().BeTrue();
        result.Value!.Branches[1].Name.Should().Be("Norte");
        result.Value!.LoginMode.Should().Be(nameof(CompanyUserLoginMode.AskBranch));
        result.Value!.DefaultBranchId.Should().BeNull();
    }

    [Fact]
    public async Task Sin_preferencias_creadas_retorna_AskBranch_por_defecto()
    {
        var f = new Fixture();
        var membership = CompanyUserMembership.Create(
            CompanyId,
            UserId,
            "Cajero",
            null,
            Guid.NewGuid()
        );
        f.AccessRepository.Setup(r =>
                r.GetCompanyUserMembershipAsync(CompanyId, UserId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(membership);
        f.BranchRepository.Setup(r =>
                r.GetAsync(TenantId, true, null, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(Array.Empty<Branch>());
        f.CompanyUserBranchRepository.Setup(r =>
                r.GetByMembershipAsync(membership.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(Array.Empty<CompanyUserBranch>());
        f.PreferencesRepository.Setup(r =>
                r.GetByMembershipAsync(membership.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((CompanyUserPreferences?)null);

        var result = await f.BuildHandler()
            .Handle(new GetMyAvailableBranchesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Branches.Should().BeEmpty();
        result.Value!.LoginMode.Should().Be(nameof(CompanyUserLoginMode.AskBranch));
    }

    [Fact]
    public async Task Con_preferencias_DirectToDefault_expone_loginMode_y_defaultBranchId()
    {
        var f = new Fixture();
        var membership = CompanyUserMembership.Create(
            CompanyId,
            UserId,
            "Cajero",
            null,
            Guid.NewGuid()
        );
        f.AccessRepository.Setup(r =>
                r.GetCompanyUserMembershipAsync(CompanyId, UserId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(membership);

        var mainBranch = CreateBranch("Matriz", isMain: true);
        f.BranchRepository.Setup(r =>
                r.GetAsync(TenantId, true, null, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new[] { mainBranch });
        f.CompanyUserBranchRepository.Setup(r =>
                r.GetByMembershipAsync(membership.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                new[]
                {
                    CompanyUserBranch.Create(
                        TenantId,
                        CompanyId,
                        membership.Id,
                        mainBranch.Id,
                        Guid.NewGuid()
                    ),
                }
            );

        var preferences = CompanyUserPreferences.Create(
            TenantId,
            CompanyId,
            membership.Id,
            CompanyUserLoginMode.DirectToDefault,
            mainBranch.Id,
            Guid.NewGuid()
        );
        f.PreferencesRepository.Setup(r =>
                r.GetByMembershipAsync(membership.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(preferences);

        var result = await f.BuildHandler()
            .Handle(new GetMyAvailableBranchesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.LoginMode.Should().Be(nameof(CompanyUserLoginMode.DirectToDefault));
        result.Value!.DefaultBranchId.Should().Be(mainBranch.Id);
    }
}
