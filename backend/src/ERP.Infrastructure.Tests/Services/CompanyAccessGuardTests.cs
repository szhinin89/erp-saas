using ERP.Application.Common;
using ERP.Application.Common.Security;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Tenants.Entities;
using ERP.Domain.Tenants.Interfaces;
using ERP.Infrastructure.Services;
using FluentAssertions;
using Moq;

namespace ERP.Infrastructure.Tests.Services;

/// <summary>
/// FASE 4B (ZH-AUTH-BACKEND-COMPANY-BRANCH-ISOLATION-04B) — prueba directamente la implementación
/// real de <see cref="CompanyAccessGuard"/> (no un mock de la interfaz, como hacen
/// CompanyScopeBehaviorTests). El header X-Company-Id nunca es autoridad por sí solo: este guard
/// es el único punto que revalida, en cada request, que la empresa pertenece al tenant del JWT y
/// que el usuario tiene una CompanyUserMembership real y activa en ella.
/// </summary>
public sealed class CompanyAccessGuardTests
{
    private static readonly Guid CreatedBy = Guid.NewGuid();

    private sealed class Fixture
    {
        public Mock<IAccessRepository> Access { get; } = new();
        public Mock<ICompanyRepository> Companies { get; } = new();
        public Mock<ICurrentUser> CurrentUser { get; } = new();
        public Mock<ICurrentTenant> CurrentTenant { get; } = new();
        public Mock<ICurrentCompany> CurrentCompany { get; } = new();
        public Mock<ITenantRepository> Tenants { get; } = new();
        public Mock<ISecurityMetrics> Metrics { get; } = new();
        public Mock<IOperatorCompanyAccessPolicy> OperatorAccessPolicy { get; } = new();

        public Fixture()
        {
            // Por defecto ningún test de esta clase es un admin global operando — evita que un
            // Mock sin Setup devuelva Task<bool> nulo (NullReferenceException al await).
            OperatorAccessPolicy
                .Setup(o => o.IsAuthorizedOperatorAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
        }

        public CompanyAccessGuard BuildGuard() =>
            new(
                Access.Object,
                Companies.Object,
                CurrentUser.Object,
                CurrentTenant.Object,
                CurrentCompany.Object,
                Tenants.Object,
                Metrics.Object,
                OperatorAccessPolicy.Object
            );
    }

    private static (Fixture f, Tenant tenant, Company company, Guid userId) BuildAuthenticatedContext()
    {
        var f = new Fixture();
        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], CreatedBy);
        var company = Company.CreateManaged(
            tenant.Id,
            "1790012345001",
            "Empresa A S.A.",
            createdBy: CreatedBy
        );
        var userId = Guid.NewGuid();

        f.CurrentUser.Setup(u => u.IsAuthenticated).Returns(true);
        f.CurrentUser.Setup(u => u.UserId).Returns(userId);
        f.CurrentTenant.Setup(t => t.TenantId).Returns(tenant.Id);
        f.Tenants.Setup(t => t.GetByIdAsync(tenant.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        return (f, tenant, company, userId);
    }

    [Fact]
    public async Task RequireMembershipAsync_sin_membership_en_la_empresa_rechaza_el_acceso()
    {
        var (f, tenant, company, userId) = BuildAuthenticatedContext();
        f.Companies.Setup(c => c.GetByIdAsync(company.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);
        f.Access.Setup(a =>
                a.GetCompanyUserMembershipAsync(company.Id, userId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((CompanyUserMembership?)null);

        var guard = f.BuildGuard();
        var result = await guard.RequireMembershipAsync(company.Id);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("No tiene acceso a esta empresa.");
        f.Metrics.Verify(m => m.RecordMembershipValidationFailed(null), Times.Once);
    }

    [Fact]
    public async Task RequireMembershipAsync_con_membership_inactiva_rechaza_el_acceso()
    {
        var (f, tenant, company, userId) = BuildAuthenticatedContext();
        var membership = CompanyUserMembership.Create(company.Id, userId, "Admin", null, CreatedBy);
        membership.Deactivate(CreatedBy);

        f.Companies.Setup(c => c.GetByIdAsync(company.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);
        f.Access.Setup(a =>
                a.GetCompanyUserMembershipAsync(company.Id, userId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(membership);

        var guard = f.BuildGuard();
        var result = await guard.RequireMembershipAsync(company.Id);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("No tiene acceso a esta empresa.");
    }

    [Fact]
    public async Task RequireMembershipAsync_empresa_de_otro_tenant_rechaza_el_acceso_como_fuga_cross_company()
    {
        var (f, tenant, _, userId) = BuildAuthenticatedContext();
        var otherTenant = Tenant.Create("Otro Tenant", $"other-{Guid.NewGuid():N}"[..16], CreatedBy);
        var companyDeOtroTenant = Company.CreateManaged(
            otherTenant.Id,
            "1790012345002",
            "Empresa de otro tenant S.A.",
            createdBy: CreatedBy
        );

        f.Companies.Setup(c => c.GetByIdAsync(companyDeOtroTenant.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(companyDeOtroTenant);

        var guard = f.BuildGuard();
        var result = await guard.RequireMembershipAsync(companyDeOtroTenant.Id);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Empresa no encontrada o no pertenece al tenant activo.");
        f.Metrics.Verify(m => m.RecordCrossCompanyDenied(null), Times.Once);
        f.Access.Verify(
            a =>
                a.GetCompanyUserMembershipAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never,
            "nunca debe consultar membership de una empresa que ni siquiera pertenece al tenant del JWT"
        );
    }

    [Fact]
    public async Task RequireMembershipAsync_con_membership_activa_en_la_empresa_correcta_permite_el_acceso()
    {
        var (f, tenant, company, userId) = BuildAuthenticatedContext();
        var membership = CompanyUserMembership.Create(company.Id, userId, "Admin", null, CreatedBy);

        f.Companies.Setup(c => c.GetByIdAsync(company.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);
        f.Access.Setup(a =>
                a.GetCompanyUserMembershipAsync(company.Id, userId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(membership);

        var guard = f.BuildGuard();
        var result = await guard.RequireMembershipAsync(company.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value!.CompanyId.Should().Be(company.Id);
        result.Value.TenantId.Should().Be(tenant.Id);
        result.Value.UserId.Should().Be(userId);
    }

    /// <summary>
    /// ERP-CORE-GLOBAL-ADMIN-BRANCH-ACCESS-01: un admin global operando esta empresa
    /// (IOperatorCompanyAccessPolicy autorizado) no tiene CompanyUserMembership aquí — la
    /// política central es la única fuente que puede sustituir ese requisito, y solo para ese
    /// caso puntual. El contexto resultante usa SecurityRoles.Admin como rol operativo.
    /// </summary>
    [Fact]
    public async Task RequireMembershipAsync_sin_membership_pero_autorizado_como_operador_global_permite_el_acceso()
    {
        var (f, tenant, company, userId) = BuildAuthenticatedContext();
        f.Companies.Setup(c => c.GetByIdAsync(company.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);
        f.Access.Setup(a =>
                a.GetCompanyUserMembershipAsync(company.Id, userId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((CompanyUserMembership?)null);
        f.OperatorAccessPolicy
            .Setup(o => o.IsAuthorizedOperatorAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var guard = f.BuildGuard();
        var result = await guard.RequireMembershipAsync(company.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value!.CompanyId.Should().Be(company.Id);
        result.Value.TenantId.Should().Be(tenant.Id);
        result.Value.Role.Should().Be(ERP.Domain.Kernel.Security.SecurityRoles.Admin);
        f.Metrics.Verify(m => m.RecordMembershipValidationFailed(null), Times.Never);
    }

    /// <summary>
    /// Sin autorización de la política de operador (usuario normal sin membership, o admin
    /// global sin operator_mode/GlobalUserRole vigente), el rechazo sigue siendo el mismo de
    /// siempre — la política nunca se convierte en un bypass general.
    /// </summary>
    [Fact]
    public async Task RequireMembershipAsync_sin_membership_y_sin_autorizacion_de_operador_rechaza_el_acceso()
    {
        var (f, _, company, userId) = BuildAuthenticatedContext();
        f.Companies.Setup(c => c.GetByIdAsync(company.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);
        f.Access.Setup(a =>
                a.GetCompanyUserMembershipAsync(company.Id, userId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((CompanyUserMembership?)null);

        var guard = f.BuildGuard();
        var result = await guard.RequireMembershipAsync(company.Id);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("No tiene acceso a esta empresa.");
        f.Metrics.Verify(m => m.RecordMembershipValidationFailed(null), Times.Once);
    }

    [Fact]
    public async Task RequireMembershipAsync_empresa_suspendida_rechaza_acceso_operativo()
    {
        var (f, _, company, userId) = BuildAuthenticatedContext();
        company.SuspendOperations();
        var membership = CompanyUserMembership.Create(company.Id, userId, "Admin", null, CreatedBy);

        f.Companies.Setup(c => c.GetByIdAsync(company.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);
        f.Access.Setup(a =>
                a.GetCompanyUserMembershipAsync(company.Id, userId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(membership);

        var guard = f.BuildGuard();
        var result = await guard.RequireMembershipAsync(company.Id);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Empresa no disponible para operar.");
        company.OperationalStatus.Should().Be(CompanyOperationalStatus.Suspended);
        f.Access.Verify(
            a =>
                a.GetCompanyUserMembershipAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task RequireMembershipAsync_empresa_suspendida_permite_acceso_no_operativo_si_no_exige_empresa_activa()
    {
        var (f, tenant, company, userId) = BuildAuthenticatedContext();
        company.SuspendOperations();
        var membership = CompanyUserMembership.Create(company.Id, userId, "Admin", null, CreatedBy);

        f.Companies.Setup(c => c.GetByIdAsync(company.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);
        f.Access.Setup(a =>
                a.GetCompanyUserMembershipAsync(company.Id, userId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(membership);

        var guard = f.BuildGuard();
        var result = await guard.RequireMembershipAsync(
            company.Id,
            requireActiveCompany: false
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.CompanyId.Should().Be(company.Id);
        result.Value.TenantId.Should().Be(tenant.Id);
        result.Value.CompanyIsActive.Should().BeTrue();
        company.OperationalStatus.Should().Be(CompanyOperationalStatus.Suspended);
    }

    [Fact]
    public async Task RequireMembershipAsync_empresa_inactiva_rechaza_acceso_operativo()
    {
        var (f, _, company, userId) = BuildAuthenticatedContext();
        company.UpdateAdminIdentity(company.LegalName, company.TradeName, isActive: false, CreatedBy);
        var membership = CompanyUserMembership.Create(company.Id, userId, "Admin", null, CreatedBy);

        f.Companies.Setup(c => c.GetByIdAsync(company.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);
        f.Access.Setup(a =>
                a.GetCompanyUserMembershipAsync(company.Id, userId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(membership);

        var guard = f.BuildGuard();
        var result = await guard.RequireMembershipAsync(company.Id);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Empresa no disponible para operar.");
        f.Access.Verify(
            a =>
                a.GetCompanyUserMembershipAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task RequireCurrentCompanyAsync_sin_header_X_Company_Id_rechaza_antes_de_consultar_membership()
    {
        var (f, _, _, _) = BuildAuthenticatedContext();
        f.CurrentCompany.Setup(c => c.HasCompanyContext).Returns(false);

        var guard = f.BuildGuard();
        var result = await guard.RequireCurrentCompanyAsync();

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("No hay empresa operativa seleccionada.");
        f.Metrics.Verify(m => m.RecordInvalidCompanyContext(null), Times.Once);
        f.Companies.Verify(
            c => c.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task RequireCurrentCompanyAsync_empresa_suspendida_rechaza_acceso_operativo()
    {
        var (f, _, company, userId) = BuildAuthenticatedContext();
        company.SuspendOperations();
        var membership = CompanyUserMembership.Create(company.Id, userId, "Admin", null, CreatedBy);

        f.CurrentCompany.Setup(c => c.HasCompanyContext).Returns(true);
        f.CurrentCompany.Setup(c => c.CompanyId).Returns(company.Id);
        f.Companies.Setup(c => c.GetByIdAsync(company.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);
        f.Access.Setup(a =>
                a.GetCompanyUserMembershipAsync(company.Id, userId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(membership);

        var guard = f.BuildGuard();
        var result = await guard.RequireCurrentCompanyAsync();

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Empresa no disponible para operar.");
        f.Access.Verify(
            a =>
                a.GetCompanyUserMembershipAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }
}
