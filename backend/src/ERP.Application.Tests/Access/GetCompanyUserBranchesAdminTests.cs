using ERP.Application.Access.UseCases.GetCompanyUserBranchesAdmin;
using ERP.Application.Common;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Branches.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Access;

/// <summary>
/// Fase I-B: GetCompanyUserBranchesAdminHandler proyecta las sucursales activas de la empresa de
/// la membresía marcando cuáles están autorizadas — sin escribir nada. Mismo aislamiento
/// multi-tenant que GetCompanyUserPreferencesAdminHandler (Fase F): mismo mensaje para "no existe"
/// y "pertenece a otra empresa".
/// </summary>
public sealed class GetCompanyUserBranchesAdminHandlerTests
{
    private static readonly Guid CreatedBy = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CurrentCompanyId = Guid.NewGuid();
    private static readonly Guid OtherCompanyId = Guid.NewGuid();

    private static CompanyUserMembership Membership(Guid companyId) =>
        CompanyUserMembership.Create(companyId, Guid.NewGuid(), "User", null, CreatedBy);

    private static Branch NewBranch(Guid companyId, string name) => Branch.Create(
        TenantId, name, "Av. Principal 123", "001",
        null, null, null, null, null, null, null, null, null, null, null,
        null, null, null, null, null, null, null, null, true, CreatedBy,
        companyId: companyId);

    private sealed class CurrentCompanyStub : ICurrentCompany
    {
        public Guid CompanyId => CurrentCompanyId;
        public bool IsAuthenticated => true;
        public bool HasCompanyContext => true;
    }

    private sealed class CurrentTenantStub : ICurrentTenant
    {
        public Guid TenantId => GetCompanyUserBranchesAdminHandlerTests.TenantId;
        public string? Slug => null;
    }

    private sealed class Fixture
    {
        public Mock<IAccessRepository> AccessRepo { get; } = new();
        public Mock<IBranchRepository> BranchRepo { get; } = new();
        public Mock<ICompanyUserBranchRepository> CompanyUserBranchRepo { get; } = new();

        public GetCompanyUserBranchesAdminHandler BuildHandler() => new(
            AccessRepo.Object, new CurrentCompanyStub(), new CurrentTenantStub(), BranchRepo.Object, CompanyUserBranchRepo.Object);
    }

    [Fact]
    public async Task Devuelve_sucursales_activas_de_la_empresa_marcando_las_autorizadas()
    {
        var membership = Membership(CurrentCompanyId);
        var branchA = NewBranch(CurrentCompanyId, "Matriz");
        var branchB = NewBranch(CurrentCompanyId, "Sucursal Norte");
        var authorization = CompanyUserBranch.Create(TenantId, CurrentCompanyId, membership.Id, branchA.Id, CreatedBy);

        var f = new Fixture();
        f.AccessRepo.Setup(r => r.GetCompanyUserMembershipByIdAsync(membership.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);
        f.BranchRepo.Setup(r => r.GetAsync(TenantId, true, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { branchA, branchB });
        f.CompanyUserBranchRepo.Setup(r => r.GetByMembershipAsync(membership.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { authorization });

        var handler = f.BuildHandler();
        var result = await handler.Handle(new GetCompanyUserBranchesAdminQuery(membership.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.CompanyUserId.Should().Be(membership.Id);
        result.Value.Branches.Should().HaveCount(2);
        result.Value.Branches.Single(b => b.BranchId == branchA.Id).Authorized.Should().BeTrue();
        result.Value.Branches.Single(b => b.BranchId == branchB.Id).Authorized.Should().BeFalse();
    }

    [Fact]
    public async Task Excluye_sucursales_de_otra_empresa_aunque_el_repositorio_las_devuelva()
    {
        var membership = Membership(CurrentCompanyId);
        var ownBranch = NewBranch(CurrentCompanyId, "Matriz");
        var foreignBranch = NewBranch(OtherCompanyId, "Sucursal ajena");

        var f = new Fixture();
        f.AccessRepo.Setup(r => r.GetCompanyUserMembershipByIdAsync(membership.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);
        f.BranchRepo.Setup(r => r.GetAsync(TenantId, true, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ownBranch, foreignBranch });
        f.CompanyUserBranchRepo.Setup(r => r.GetByMembershipAsync(membership.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CompanyUserBranch>());

        var handler = f.BuildHandler();
        var result = await handler.Handle(new GetCompanyUserBranchesAdminQuery(membership.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Branches.Should().ContainSingle(b => b.BranchId == ownBranch.Id);
        result.Value.Branches.Should().NotContain(b => b.BranchId == foreignBranch.Id);
    }

    [Fact]
    public async Task Membresia_de_otra_empresa_devuelve_NotFound_sin_consultar_sucursales()
    {
        var membership = Membership(OtherCompanyId);
        var f = new Fixture();
        f.AccessRepo.Setup(r => r.GetCompanyUserMembershipByIdAsync(membership.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);

        var handler = f.BuildHandler();
        var result = await handler.Handle(new GetCompanyUserBranchesAdminQuery(membership.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        f.BranchRepo.Verify(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<bool?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Membresia_inexistente_devuelve_NotFound()
    {
        var missingId = Guid.NewGuid();
        var f = new Fixture();
        f.AccessRepo.Setup(r => r.GetCompanyUserMembershipByIdAsync(missingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompanyUserMembership?)null);

        var handler = f.BuildHandler();
        var result = await handler.Handle(new GetCompanyUserBranchesAdminQuery(missingId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }
}
