using ERP.Application.Access.DTOs;
using ERP.Application.Access.UseCases.GetUserSessionsPaged;
using ERP.Application.Common;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Enums;
using ERP.Domain.Access.Interfaces;
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
        result.Errors.Should().Contain(e => e.PropertyName == nameof(GetUserSessionsPagedQuery.PageNumber));
    }

    [Fact]
    public void PageSize_mayor_a_200_es_invalido()
    {
        var result = Validator.Validate(new GetUserSessionsPagedQuery(PageSize: 500));
        result.Errors.Should().Contain(e => e.PropertyName == nameof(GetUserSessionsPagedQuery.PageSize));
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
        var result = Validator.Validate(new GetUserSessionsPagedQuery(
            FromUtc: DateTime.UtcNow, ToUtc: DateTime.UtcNow.AddDays(-1)));
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
        repo.Setup(r => r.GetPagedAsync(
                TenantId, IdentityUserId, CompanyId, UserSessionStatus.Active,
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), 2, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<UserSession>(), 0));

        var handler = new GetUserSessionsPagedHandler(repo.Object, tenant.Object);
        var query = new GetUserSessionsPagedQuery(IdentityUserId, CompanyId, "Active", PageNumber: 2, PageSize: 10);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        repo.VerifyAll();
    }

    [Fact]
    public async Task Mapea_correctamente_a_UserSessionAdminDto()
    {
        var (repo, tenant) = BuildMocks();
        var session = UserSession.Create(TenantId, CompanyId, IdentityUserId, BranchId, "device-1");
        repo.Setup(r => r.GetPagedAsync(
                TenantId, null, null, null, null, null, 1, 25, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[] { session }, 1));

        var handler = new GetUserSessionsPagedHandler(repo.Object, tenant.Object);
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
        properties.Should().NotContain(p =>
            p.Name.Contains("RefreshToken", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("Hash", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("Token", StringComparison.OrdinalIgnoreCase));
    }
}
