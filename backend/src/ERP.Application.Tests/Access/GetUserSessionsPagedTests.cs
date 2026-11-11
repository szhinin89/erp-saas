using ERP.Application.Access.DTOs;
using ERP.Application.Access.UseCases.GetUserSessionsPaged;
using ERP.Application.Common;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Enums;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Branches.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Access;

public sealed class GetUserSessionsPagedValidatorTests
{
    private static readonly GetUserSessionsPagedValidator Validator = new();

    [Fact]
    public void Query_por_defecto_no_tiene_errores()
    {
        Validator.Validate(new GetUserSessionsPagedQuery()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void PageNumber_menor_a_1_es_invalido()
    {
        var result = Validator.Validate(new GetUserSessionsPagedQuery(PageNumber: 0));
        result
            .Errors.Should()
            .Contain(e => e.PropertyName == nameof(GetUserSessionsPagedQuery.PageNumber));
    }

    [Fact]
    public void PageSize_mayor_a_200_es_invalido()
    {
        var result = Validator.Validate(new GetUserSessionsPagedQuery(PageSize: 500));
        result
            .Errors.Should()
            .Contain(e => e.PropertyName == nameof(GetUserSessionsPagedQuery.PageSize));
    }

    [Fact]
    public void Status_invalido_es_rechazado()
    {
        var result = Validator.Validate(new GetUserSessionsPagedQuery(Status: "NoExiste"));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Status_valido_no_tiene_errores()
    {
        var result = Validator.Validate(new GetUserSessionsPagedQuery(Status: "Active"));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void FromUtc_posterior_a_ToUtc_es_invalido()
    {
        var result = Validator.Validate(
            new GetUserSessionsPagedQuery(
                FromUtc: DateTime.UtcNow,
                ToUtc: DateTime.UtcNow.AddDays(-1)
            )
        );
        result.IsValid.Should().BeFalse();
    }
}

public sealed class GetUserSessionsPagedHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid IdentityUserId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();

    private static (Mock<IUserSessionRepository> repo, Mock<ICurrentTenant> tenant) BuildMocks()
    {
        var repo = new Mock<IUserSessionRepository>();
        var tenant = new Mock<ICurrentTenant>();
        tenant.Setup(t => t.TenantId).Returns(TenantId);
        return (repo, tenant);
    }

    [Fact]
    public async Task Aplica_los_filtros_recibidos_al_repositorio()
    {
        var (repo, tenant) = BuildMocks();
        repo.Setup(r =>
                r.GetPagedAsync(
                    TenantId,
                    IdentityUserId,
                    CompanyId,
                    UserSessionStatus.Active,
                    It.IsAny<DateTime?>(),
                    It.IsAny<DateTime?>(),
                    2,
                    10,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((Array.Empty<UserSession>(), 0));

        var handler = NewHandler(repo, tenant);
        var query = new GetUserSessionsPagedQuery(
            IdentityUserId,
            CompanyId,
            "Active",
            PageNumber: 2,
            PageSize: 10
        );

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        repo.VerifyAll();
    }

    [Fact]
    public async Task Mapea_correctamente_a_UserSessionAdminDto()
    {
        var (repo, tenant) = BuildMocks();
        var session = UserSession.Create(TenantId, CompanyId, IdentityUserId, BranchId, "device-1");
        repo.Setup(r =>
                r.GetPagedAsync(
                    TenantId,
                    null,
                    CompanyId,
                    null,
                    null,
                    null,
                    1,
                    25,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((new[] { session }, 1));

        var handler = NewHandler(repo, tenant);
        var result = await handler.Handle(new GetUserSessionsPagedQuery(), CancellationToken.None);

        result.Value!.Items.Should().ContainSingle();
        var dto = result.Value.Items[0];
        dto.SessionId.Should().Be(session.Id);
        dto.IdentityUserId.Should().Be(session.IdentityUserId);
        dto.CompanyId.Should().Be(session.CompanyId);
        dto.BranchId.Should().Be(session.BranchId);
        dto.TerminalId.Should().Be(session.TerminalId);
        dto.Status.Should().Be("Active");
        result.Value.TotalCount.Should().Be(1);
    }

    [Fact]
    public void UserSessionAdminDto_no_expone_RefreshTokenId_ni_datos_internos()
    {
        var properties = typeof(UserSessionAdminDto).GetProperties();
        properties
            .Should()
            .NotContain(p =>
                p.Name.Contains("RefreshToken", StringComparison.OrdinalIgnoreCase)
                || p.Name.Contains("Hash", StringComparison.OrdinalIgnoreCase)
                || p.Name.Contains("Token", StringComparison.OrdinalIgnoreCase)
            );
    }

    private static GetUserSessionsPagedHandler NewHandler(
        Mock<IUserSessionRepository> repo,
        Mock<ICurrentTenant> tenant,
        IReadOnlyList<IdentityUser>? users = null,
        IReadOnlyList<Branch>? branches = null
    )
    {
        var access = new Mock<IAccessRepository>();
        access
            .Setup(a =>
                a.GetUsersByIdsAsync(
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(users ?? Array.Empty<IdentityUser>());
        var branchRepo = new Mock<IBranchRepository>();
        branchRepo
            .Setup(r =>
                r.GetByCompanyAsync(
                    TenantId,
                    CompanyId,
                    null,
                    null,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(branches ?? Array.Empty<Branch>());
        return new GetUserSessionsPagedHandler(
            repo.Object,
            tenant.Object,
            CurrentCompany().Object,
            access.Object,
            branchRepo.Object
        );
    }

    [Fact]
    public async Task Resuelve_nombre_de_usuario_y_sucursal_solo_con_datos_de_la_empresa_operativa()
    {
        var (repo, tenant) = BuildMocks();
        var user = IdentityUser.Create("jperez", "Juan", "Perez", null, "hash", Guid.NewGuid());
        var branch = Branch.Create(
            TenantId, "Matriz", "Av. 1", "001", null, null, null, null, null, null, null, null,
            null, null, null, null, null, null, null, null, null, null, null, true, Guid.NewGuid(),
            companyId: CompanyId
        );
        var known = UserSession.Create(TenantId, CompanyId, user.Id, branch.Id, "caja-1");
        var unknown = UserSession.Create(TenantId, CompanyId, Guid.NewGuid(), Guid.NewGuid(), "caja-2");
        repo.Setup(r =>
                r.GetPagedAsync(TenantId, null, CompanyId, null, null, null, 1, 25, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((new[] { known, unknown }, 2));

        var handler = NewHandler(repo, tenant, new[] { user }, new[] { branch });
        var result = await handler.Handle(new GetUserSessionsPagedQuery(), CancellationToken.None);

        var items = result.Value!.Items;
        items[0].UserFullName.Should().Be(user.FullName);
        items[0].Username.Should().Be("jperez");
        items[0].BranchName.Should().Be("Matriz");
        items[1].UserFullName.Should().BeNull();
        items[1].Username.Should().BeNull();
        items[1].BranchName.Should().BeNull();
    }

    private static Mock<ICurrentCompany> CurrentCompany()
    {
        var company = new Mock<ICurrentCompany>();
        company.Setup(c => c.HasCompanyContext).Returns(true);
        company.Setup(c => c.CompanyId).Returns(CompanyId);
        return company;
    }

    [Fact]
    public async Task IAM_V1_F1_CompanyId_de_otra_empresa_se_ignora_y_usa_la_empresa_operativa()
    {
        var (repo, tenant) = BuildMocks();
        repo.Setup(r =>
                r.GetPagedAsync(
                    TenantId,
                    null,
                    CompanyId,
                    null,
                    null,
                    null,
                    1,
                    25,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((Array.Empty<UserSession>(), 0));

        var handler = NewHandler(repo, tenant);
        var result = await handler.Handle(
            new GetUserSessionsPagedQuery(CompanyId: Guid.NewGuid()),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        repo.VerifyAll();
    }
}
