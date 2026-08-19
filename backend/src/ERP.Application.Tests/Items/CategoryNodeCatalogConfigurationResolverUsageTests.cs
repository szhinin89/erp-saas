using ERP.Application.Common;
using ERP.Application.Items.UseCases.CategoryNodes;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Enums;
using ERP.Domain.Modules.Items.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Items;

/// <summary>
/// CONFIG-FOUNDATION-P1-04 — CreateCategoryNodeCommandHandler/GetCategoryTreeQueryHandler ya no
/// leen IOrgSettingsRepository directamente (antes vía el static helper CategoryDepthResolver):
/// dependen de ICatalogConfigurationResolver.
/// </summary>
public sealed class CategoryNodeCatalogConfigurationResolverUsageTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private sealed class Fixture
    {
        public Mock<ICategoryNodeRepository> Repo { get; } = new();
        public Mock<ICatalogConfigurationResolver> CatalogResolver { get; } = new();
        public Mock<ICurrentTenant> Tenant { get; } = new();
        public Mock<ICurrentCompany> Company { get; } = new();
        public Mock<ICurrentUser> User { get; } = new();

        public Fixture()
        {
            Tenant.Setup(t => t.TenantId).Returns(TenantId);
            Company.Setup(c => c.CompanyId).Returns(CompanyId);
            User.Setup(u => u.UserId).Returns(UserId);
        }

        public CreateCategoryNodeCommandHandler BuildCreateHandler() =>
            new(Repo.Object, CatalogResolver.Object, Tenant.Object, Company.Object, User.Object);

        public GetCategoryTreeQueryHandler BuildGetTreeHandler() =>
            new(Repo.Object, CatalogResolver.Object, Tenant.Object, Company.Object);
    }

    [Fact]
    public async Task Create_consulta_max_depth_al_resolver_y_rechaza_si_lo_excede()
    {
        var f = new Fixture();
        f.CatalogResolver
            .Setup(r =>
                r.ResolveMaxCategoryDepthAsync(TenantId, CompanyId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(1);
        f.Repo.Setup(r => r.CodeExistsAsync("FAM01", TenantId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var parent = ItemCategoryNode.Create(
            TenantId,
            "PARENT",
            "Padre",
            CategoryNodeLevel.Family,
            UserId
        );
        parent.SetPath($"/{parent.Id}");
        f.Repo
            .Setup(r => r.GetByIdAsync(parent.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parent);

        var result = await f.BuildCreateHandler()
            .Handle(
                new CreateCategoryNodeCommand(parent.Id, "FAM01", "Hijo", null, "Category"),
                default
            );

        result.IsSuccess.Should().BeFalse();
        f.CatalogResolver.Verify(
            r =>
                r.ResolveMaxCategoryDepthAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Create_permite_el_nodo_cuando_no_excede_max_depth()
    {
        var f = new Fixture();
        f.CatalogResolver
            .Setup(r =>
                r.ResolveMaxCategoryDepthAsync(TenantId, CompanyId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(3);
        f.Repo
            .Setup(r => r.CodeExistsAsync("FAM01", TenantId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await f.BuildCreateHandler()
            .Handle(new CreateCategoryNodeCommand(null, "FAM01", "Familia", null, "Family"), default);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task GetCategoryTree_expone_el_max_depth_resuelto_en_el_DTO()
    {
        var f = new Fixture();
        f.CatalogResolver
            .Setup(r =>
                r.ResolveMaxCategoryDepthAsync(TenantId, CompanyId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(5);
        f.Repo
            .Setup(r => r.GetAllAsync(TenantId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ItemCategoryNode>());

        var result = await f.BuildGetTreeHandler().Handle(new GetCategoryTreeQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.MaxDepth.Should().Be(5);
    }
}
