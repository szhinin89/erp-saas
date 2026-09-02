using ERP.Application.Access.DTOs;
using ERP.Application.Access.UseCases.CreateSystemUser;
using ERP.Application.Access.UseCases.CreateSystemUserAdmin;
using ERP.Application.Common;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Tenants.Entities;
using ERP.Domain.Tenants.Interfaces;
using FluentAssertions;
using MediatR;
using Moq;

namespace ERP.Application.Tests.Access;

/// <summary>
/// Envoltorio delgado sobre CreateSystemUserHandler (probado en CreateSystemUserHandlerTests) —
/// estos tests cubren únicamente el agregado propio de esta fase: resolución de TenantId/CompanyId
/// desde el contexto autenticado (nunca del request) y el aislamiento cuando la empresa activa no
/// existe dentro del tenant, mismo criterio que
/// UpsertCompanyUserMembershipAdminHandlerTests.
/// </summary>
public sealed class CreateSystemUserAdminHandlerTests
{
    private static readonly Guid CreatedBy = Guid.NewGuid();
    private const string Username = "nuevo.usuario";
    private const string Email = "nuevo@test.com";

    private static Tenant NewTenant() =>
        Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], CreatedBy);

    private static Company NewCompany(Guid tenantId) =>
        Company.CreateManaged(tenantId, "1790012345001", "Test S.A.", createdBy: CreatedBy);

    private sealed class CurrentTenantStub : ICurrentTenant
    {
        public CurrentTenantStub(Guid tenantId) => TenantId = tenantId;

        public Guid TenantId { get; }
        public string? Slug => null;
    }

    private sealed class CurrentCompanyStub : ICurrentCompany
    {
        public CurrentCompanyStub(Guid companyId) => CompanyId = companyId;

        public Guid CompanyId { get; }
        public bool IsAuthenticated => true;
        public bool HasCompanyContext => true;
    }

    private sealed class Fixture
    {
        public Mock<ITenantRepository> TenantRepo { get; } = new();
        public Mock<ICompanyRepository> CompanyRepo { get; } = new();
        public Mock<IMediator> Mediator { get; } = new();

        public CreateSystemUserAdminHandler BuildHandler(Guid tenantId, Guid companyId) =>
            new(
                new CurrentTenantStub(tenantId),
                new CurrentCompanyStub(companyId),
                TenantRepo.Object,
                CompanyRepo.Object,
                Mediator.Object
            );
    }

    [Fact]
    public async Task Empresa_activa_coincide_con_la_del_tenant_delega_en_CreateSystemUserHandler_y_devuelve_su_resultado()
    {
        var tenant = NewTenant();
        var company = NewCompany(tenant.Id);
        var f = new Fixture();
        f.TenantRepo.Setup(r => r.GetByIdAsync(tenant.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        f.CompanyRepo.Setup(r =>
                r.GetByIdForTenantAsync(company.Id, tenant.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(company);

        CreateSystemUserCommand? sentCommand = null;
        f.Mediator.Setup(m =>
                m.Send(It.IsAny<CreateSystemUserCommand>(), It.IsAny<CancellationToken>())
            )
            .Callback<IRequest<Result<CreateSystemUserResultDto>>, CancellationToken>(
                (cmd, _) => sentCommand = (CreateSystemUserCommand)cmd
            )
            .ReturnsAsync(
                Result<CreateSystemUserResultDto>.Success(
                    new CreateSystemUserResultDto(Guid.NewGuid(), Username)
                )
            );

        var handler = f.BuildHandler(tenant.Id, company.Id);
        var result = await handler.Handle(
            new CreateSystemUserAdminCommand(
                Username,
                "Ana",
                "Perez",
                Email,
                "S3curePass!",
                "User"
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        sentCommand.Should().NotBeNull();
        sentCommand!.TenantId.Should().Be(tenant.Id);
        sentCommand.CompanyId.Should().Be(company.Id);
        sentCommand.Email.Should().Be(Email);
        sentCommand.Role.Should().Be("User");
    }

    [Fact]
    public async Task Empresa_activa_inexistente_en_el_tenant_devuelve_NotFound_y_no_delega()
    {
        var tenant = NewTenant();
        var otherActiveCompanyId = Guid.NewGuid();
        var f = new Fixture();
        f.TenantRepo.Setup(r => r.GetByIdAsync(tenant.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        f.CompanyRepo.Setup(r =>
                r.GetByIdForTenantAsync(
                    otherActiveCompanyId,
                    tenant.Id,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((Company?)null);

        var handler = f.BuildHandler(tenant.Id, otherActiveCompanyId);
        var result = await handler.Handle(
            new CreateSystemUserAdminCommand(
                Username,
                "Ana",
                "Perez",
                Email,
                "S3curePass!",
                "User"
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        f.Mediator.Verify(
            m => m.Send(It.IsAny<CreateSystemUserCommand>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Tenant_inexistente_devuelve_NotFound_y_no_delega()
    {
        var tenantId = Guid.NewGuid();
        var f = new Fixture();
        f.TenantRepo.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant?)null);

        var handler = f.BuildHandler(tenantId, Guid.NewGuid());
        var result = await handler.Handle(
            new CreateSystemUserAdminCommand(
                Username,
                "Ana",
                "Perez",
                Email,
                "S3curePass!",
                "User"
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        f.Mediator.Verify(
            m => m.Send(It.IsAny<CreateSystemUserCommand>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Email_duplicado_propaga_el_Conflict_del_handler_delegado()
    {
        var tenant = NewTenant();
        var company = NewCompany(tenant.Id);
        var f = new Fixture();
        f.TenantRepo.Setup(r => r.GetByIdAsync(tenant.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        f.CompanyRepo.Setup(r =>
                r.GetByIdForTenantAsync(company.Id, tenant.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(company);
        f.Mediator.Setup(m =>
                m.Send(It.IsAny<CreateSystemUserCommand>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                Result<CreateSystemUserResultDto>.Conflict("Ya existe un usuario con ese email.")
            );

        var handler = f.BuildHandler(tenant.Id, company.Id);
        var result = await handler.Handle(
            new CreateSystemUserAdminCommand(
                Username,
                "Ana",
                "Perez",
                Email,
                "S3curePass!",
                "User"
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.Conflict);
    }
}
