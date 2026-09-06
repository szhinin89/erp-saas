using ERP.Application.Access;
using ERP.Application.Access.Caching;
using ERP.Application.Common;
using ERP.Application.Common.Security;
using ERP.Application.Modules.Media;
using ERP.Application.Modules.Session.UseCases.GetSessionContext;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Branches.Interfaces;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Tenants.Entities;
using ERP.Domain.Tenants.Interfaces;
using FluentAssertions;
using MediatR;
using Moq;

namespace ERP.Application.Tests.Modules.Session.UseCases.GetSessionContext;

/// <summary>
/// FASE 4B (ZH-AUTH-BACKEND-COMPANY-BRANCH-ISOLATION-04B) — caso 5: GetSessionContextHandler debe
/// devolver un Branch consistente con la empresa operativa activa, y nunca "adoptar" el header
/// X-Branch-Id cuando ese branch pertenece a otra empresa. La regla vive en
/// ResolveActiveBranchAsync (precedencia: header ICurrentBranch → UserSession activa → resolver de
/// preferencias) — este test cubre el primer nivel de esa precedencia, que es también el único
/// alimentado directamente por un valor client-supplied (el header, no un claim firmado).
/// </summary>
public sealed class GetSessionContextHandlerTests
{
    private static readonly Guid CreatedBy = Guid.NewGuid();

    private sealed class Fixture
    {
        public Mock<ICurrentUser> CurrentUser { get; } = new();
        public Mock<ICurrentTenant> CurrentTenant { get; } = new();
        public Mock<ICurrentBranch> CurrentBranch { get; } = new();
        public Mock<IAccessRepository> AccessRepo { get; } = new();
        public Mock<ITenantRepository> TenantRepo { get; } = new();
        public Mock<ICompanyRepository> CompanyRepo { get; } = new();
        public Mock<ICompanyContextProvider> CompanyContext { get; } = new();
        public Mock<IEffectivePermissionKeysProvider> PermissionKeys { get; } = new();
        public Mock<IMediaService> Media { get; } = new();
        public Mock<IUserSessionRepository> UserSessionRepo { get; } = new();
        public Mock<IBranchRepository> BranchRepo { get; } = new();
        public Mock<ICompanyUserBranchRepository> CompanyUserBranchRepo { get; } = new();
        public Mock<IMediator> Mediator { get; } = new();
        public Mock<IOperatorCompanyAccessPolicy> OperatorAccessPolicy { get; } = new();

        public Fixture()
        {
            // Por defecto ningún test de esta clase es un admin global operando — evita que un
            // Mock sin Setup devuelva Task<bool> nulo (NullReferenceException al await).
            OperatorAccessPolicy
                .Setup(o => o.IsAuthorizedOperatorAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
        }

        public GetSessionContextHandler BuildHandler() =>
            new(
                CurrentUser.Object,
                CurrentTenant.Object,
                CurrentBranch.Object,
                AccessRepo.Object,
                TenantRepo.Object,
                CompanyRepo.Object,
                CompanyContext.Object,
                PermissionKeys.Object,
                Media.Object,
                UserSessionRepo.Object,
                BranchRepo.Object,
                CompanyUserBranchRepo.Object,
                Mediator.Object,
                OperatorAccessPolicy.Object
            );
    }

    private static Branch NewBranch(Guid tenantId, Guid companyId, string name) =>
        Branch.Create(
            tenantId,
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
            isMainBranch: true,
            CreatedBy,
            companyId: companyId
        );

    private static (
        Fixture f,
        Guid userId,
        Tenant tenant,
        Company companyA
    ) BuildBaseContext()
    {
        var f = new Fixture();
        var userId = Guid.NewGuid();
        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], CreatedBy);
        var companyA = Company.CreateManaged(
            tenant.Id,
            "1790012345001",
            "Empresa A S.A.",
            createdBy: CreatedBy
        );
        var user = IdentityUser.Create(
            "ana.perez",
            "Ana",
            "Perez",
            "ana@test.com",
            "hash",
            CreatedBy
        );

        f.CurrentUser.Setup(u => u.IsAuthenticated).Returns(true);
        f.CurrentUser.Setup(u => u.UserId).Returns(userId);
        f.CurrentUser.Setup(u => u.Role).Returns("User");
        f.CurrentTenant.Setup(t => t.TenantId).Returns(tenant.Id);
        f.AccessRepo.Setup(a => a.GetUserByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        f.TenantRepo.Setup(t => t.GetByIdAsync(tenant.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        f.CompanyContext.Setup(c =>
                c.ResolveOperationalForCurrentUserAsync(It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new OperationalCompanyContext(companyA.Id, userId, null, true));
        f.CompanyRepo.Setup(c => c.GetByIdAsync(companyA.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(companyA);
        f.Media.Setup(m =>
                m.GetActivePrimaryAsync(
                    tenant.Id,
                    companyA.Id,
                    It.IsAny<ERP.Domain.Modules.Media.Enums.MediaOwnerType>(),
                    companyA.Id,
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((ERP.Domain.Modules.Media.Entities.MediaFile?)null);

        return (f, userId, tenant, companyA);
    }

    [Fact]
    public async Task Branch_del_header_que_pertenece_a_la_empresa_activa_se_usa_directamente_sin_consultar_UserSession()
    {
        var (f, userId, tenant, companyA) = BuildBaseContext();
        var branchDeEmpresaA = NewBranch(tenant.Id, companyA.Id, "Matriz");
        var membership = CompanyUserMembership.Create(companyA.Id, userId, "User", null, CreatedBy);

        f.CurrentBranch.Setup(b => b.HasBranchContext).Returns(true);
        f.CurrentBranch.Setup(b => b.BranchId).Returns(branchDeEmpresaA.Id);
        f.BranchRepo.Setup(r =>
                r.GetByIdForCompanyAsync(
                    tenant.Id,
                    companyA.Id,
                    branchDeEmpresaA.Id,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(branchDeEmpresaA);
        f.AccessRepo.Setup(a =>
                a.GetCompanyUserMembershipAsync(companyA.Id, userId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(membership);
        f.CompanyUserBranchRepo.Setup(r =>
                r.ExistsAsync(membership.Id, branchDeEmpresaA.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(true);

        var handler = f.BuildHandler();
        var result = await handler.Handle(new GetSessionContextQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var sessionContext = result.Value!;
        sessionContext.Branch.Should().NotBeNull();
        sessionContext.Branch!.Id.Should().Be(branchDeEmpresaA.Id);
        sessionContext.Branch.Name.Should().Be("Matriz");
        f.UserSessionRepo.Verify(
            r =>
                r.GetActiveSessionsAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never,
            "el header válido es la fuente de mayor precedencia; no debe consultar niveles inferiores"
        );
    }

    /// <summary>
    /// ZH-AUTH-BRANCH-CONTEXT-EXPENSES-AUDIT-12: regresión del bug real — una sucursal que
    /// pertenece a la empresa activa y está habilitada, pero cuya autorización CompanyUserBranch
    /// fue revocada para esta membership, ya NO debe "confirmarse" como sucursal activa solo
    /// porque venga en el header. Antes de este fix, session/context la devolvía igual (solo
    /// validaba pertenencia a la empresa) mientras que los endpoints branch-scoped reales
    /// (IBranchAccessGuard, p. ej. GET /expenses/documents) sí la rechazaban con
    /// BRANCH_SCOPE_FORBIDDEN — dejando al cliente en un loop sin salida porque su "fuente de
    /// verdad" nunca se autocorregía. Ahora debe caer al siguiente nivel de precedencia.
    /// </summary>
    [Fact]
    public async Task Branch_del_header_sin_autorizacion_CompanyUserBranch_se_descarta_y_cae_al_siguiente_nivel()
    {
        var (f, userId, tenant, companyA) = BuildBaseContext();
        var branchRevocada = NewBranch(tenant.Id, companyA.Id, "Sucursal revocada");
        var membership = CompanyUserMembership.Create(companyA.Id, userId, "User", null, CreatedBy);

        f.CurrentBranch.Setup(b => b.HasBranchContext).Returns(true);
        f.CurrentBranch.Setup(b => b.BranchId).Returns(branchRevocada.Id);
        f.BranchRepo.Setup(r =>
                r.GetByIdForCompanyAsync(
                    tenant.Id,
                    companyA.Id,
                    branchRevocada.Id,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(branchRevocada);
        f.AccessRepo.Setup(a =>
                a.GetCompanyUserMembershipAsync(companyA.Id, userId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(membership);
        // La membership existe y está activa, pero ya no tiene fila CompanyUserBranch vigente
        // para esta sucursal (revocada) — exactamente lo que IBranchAccessGuard también verifica.
        f.CompanyUserBranchRepo.Setup(r =>
                r.ExistsAsync(membership.Id, branchRevocada.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(false);
        f.UserSessionRepo.Setup(r =>
                r.GetActiveSessionsAsync(userId, tenant.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(Array.Empty<UserSession>());
        // Sin preferencias configuradas y sin una única sucursal IsMainBranch resoluble por el
        // heurístico de respaldo — así el fallback tampoco "adivina" ninguna sucursal.
        f.Mediator.Setup(m =>
                m.Send(
                    It.IsAny<ERP.Application.Access.UseCases.GetCompanyUserPreferences.GetCompanyUserPreferencesQuery>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result<ERP.Application.Access.DTOs.CompanyUserPreferencesDto?>.Success(null));
        f.BranchRepo.Setup(r =>
                r.GetByCompanyAsync(
                    tenant.Id,
                    companyA.Id,
                    true,
                    null,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Array.Empty<Branch>());

        var handler = f.BuildHandler();
        var result = await handler.Handle(new GetSessionContextQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        // Sin UserSession reutilizable y sin preferencia/heurístico resoluble en este fixture,
        // el resultado correcto es "sin sucursal confirmada" — nunca la sucursal revocada.
        result.Value!.Branch.Should().BeNull();
    }

    /// <summary>
    /// ZH-AUTH-BRANCH-FORBIDDEN-F5-NO-LOGOUT-13: mismo bug que el nivel 1 (header), ahora en el
    /// nivel 2 (UserSession.BranchId histórico). Sin header X-Branch-Id, si la sucursal con la que
    /// se abrió la sesión ya no tiene fila CompanyUserBranch vigente para la membership (revocada
    /// después del login), session/context NO debe seguir reportándola como sucursal activa — el
    /// cliente la reenviaría a endpoints branch-scoped reales, que la rechazan con
    /// BRANCH_SCOPE_FORBIDDEN, y cada F5 volvería a servir la misma sucursal revocada sin
    /// autocorregirse nunca. Debe caer al nivel 3 (resolver de preferencias/heurístico).
    /// </summary>
    [Fact]
    public async Task Branch_de_UserSession_sin_autorizacion_CompanyUserBranch_se_descarta_y_cae_al_siguiente_nivel()
    {
        var (f, userId, tenant, companyA) = BuildBaseContext();
        var branchRevocada = NewBranch(tenant.Id, companyA.Id, "Sucursal revocada");
        var membership = CompanyUserMembership.Create(companyA.Id, userId, "User", null, CreatedBy);
        var existingSession = UserSession.Create(
            tenant.Id,
            companyA.Id,
            userId,
            branchRevocada.Id,
            "terminal-1"
        );

        f.CurrentBranch.Setup(b => b.HasBranchContext).Returns(false);
        f.AccessRepo.Setup(a =>
                a.GetCompanyUserMembershipAsync(companyA.Id, userId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(membership);
        f.UserSessionRepo.Setup(r =>
                r.GetActiveSessionsAsync(userId, tenant.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync([existingSession]);
        // La membership existe y está activa, pero ya no tiene fila CompanyUserBranch vigente
        // para la sucursal con la que se abrió la sesión (revocada después del login).
        f.CompanyUserBranchRepo.Setup(r =>
                r.ExistsAsync(membership.Id, branchRevocada.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(false);
        // Sin preferencias configuradas y sin una única sucursal IsMainBranch resoluble por el
        // heurístico de respaldo — así el fallback tampoco "adivina" ninguna sucursal.
        f.Mediator.Setup(m =>
                m.Send(
                    It.IsAny<ERP.Application.Access.UseCases.GetCompanyUserPreferences.GetCompanyUserPreferencesQuery>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result<ERP.Application.Access.DTOs.CompanyUserPreferencesDto?>.Success(null));
        f.BranchRepo.Setup(r =>
                r.GetByCompanyAsync(
                    tenant.Id,
                    companyA.Id,
                    true,
                    null,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Array.Empty<Branch>());

        var handler = f.BuildHandler();
        var result = await handler.Handle(new GetSessionContextQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        // Sin preferencia/heurístico resoluble en este fixture, el resultado correcto es
        // "sin sucursal confirmada" — nunca la sucursal revocada de UserSession.
        result.Value!.Branch.Should().BeNull();
    }

    /// <summary>
    /// ERP-CORE-GLOBAL-ADMIN-BRANCH-ACCESS-01: un admin global operando esta empresa (sin
    /// CompanyUserMembership, autorizado por IOperatorCompanyAccessPolicy) sí debe ver confirmada
    /// la sucursal del header, igual que la política ya deja pasar a BranchAccessGuard para
    /// requests branch-scoped reales — session/context no puede quedar más estricto que el guard.
    /// </summary>
    [Fact]
    public async Task Branch_del_header_sin_membership_pero_autorizado_como_operador_global_se_confirma()
    {
        var (f, userId, tenant, companyA) = BuildBaseContext();
        var branch = NewBranch(tenant.Id, companyA.Id, "Sucursal operada por admin global");

        f.CurrentBranch.Setup(b => b.HasBranchContext).Returns(true);
        f.CurrentBranch.Setup(b => b.BranchId).Returns(branch.Id);
        f.BranchRepo.Setup(r =>
                r.GetByIdForCompanyAsync(
                    tenant.Id,
                    companyA.Id,
                    branch.Id,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(branch);
        f.AccessRepo.Setup(a =>
                a.GetCompanyUserMembershipAsync(companyA.Id, userId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((CompanyUserMembership?)null);
        f.OperatorAccessPolicy
            .Setup(o => o.IsAuthorizedOperatorAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = f.BuildHandler();
        var result = await handler.Handle(new GetSessionContextQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Branch.Should().NotBeNull();
        result.Value.Branch!.Id.Should().Be(branch.Id);
    }

    /// <summary>
    /// Mismo caso que el anterior, ahora en el nivel 2 (UserSession.BranchId histórico) sin
    /// header X-Branch-Id — la política de operador debe aplicar igual en ambos niveles.
    /// </summary>
    [Fact]
    public async Task Branch_de_UserSession_sin_membership_pero_autorizado_como_operador_global_se_confirma()
    {
        var (f, userId, tenant, companyA) = BuildBaseContext();
        var branch = NewBranch(tenant.Id, companyA.Id, "Sucursal operada por admin global");
        var existingSession = UserSession.Create(
            tenant.Id,
            companyA.Id,
            userId,
            branch.Id,
            "terminal-1"
        );

        f.CurrentBranch.Setup(b => b.HasBranchContext).Returns(false);
        f.AccessRepo.Setup(a =>
                a.GetCompanyUserMembershipAsync(companyA.Id, userId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((CompanyUserMembership?)null);
        f.UserSessionRepo.Setup(r =>
                r.GetActiveSessionsAsync(userId, tenant.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync([existingSession]);
        f.BranchRepo.Setup(r =>
                r.GetByIdForCompanyAsync(
                    tenant.Id,
                    companyA.Id,
                    branch.Id,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(branch);
        f.OperatorAccessPolicy
            .Setup(o => o.IsAuthorizedOperatorAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = f.BuildHandler();
        var result = await handler.Handle(new GetSessionContextQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Branch.Should().NotBeNull();
        result.Value.Branch!.Id.Should().Be(branch.Id);
    }

    [Fact]
    public async Task Branch_del_header_que_pertenece_a_otra_empresa_se_descarta_y_nunca_se_devuelve_como_contexto_operativo()
    {
        var (f, userId, tenant, companyA) = BuildBaseContext();
        var companyBId = Guid.NewGuid();
        var branchDeEmpresaB = NewBranch(tenant.Id, companyBId, "Sucursal de otra empresa");

        f.CurrentBranch.Setup(b => b.HasBranchContext).Returns(true);
        f.CurrentBranch.Setup(b => b.BranchId).Returns(branchDeEmpresaB.Id);
        f.BranchRepo.Setup(r =>
                r.GetByIdForCompanyAsync(
                    tenant.Id,
                    companyA.Id,
                    branchDeEmpresaB.Id,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((Branch?)null);

        // Sin sesión activa reutilizable y sin membership resoluble para el heurístico —
        // fuerza el escenario "no hay ninguna sucursal segura que ofrecer", que es el
        // resultado fail-closed correcto (nunca se debe filtrar branchDeEmpresaB).
        f.UserSessionRepo.Setup(r =>
                r.GetActiveSessionsAsync(userId, tenant.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(Array.Empty<UserSession>());
        f.AccessRepo.Setup(a =>
                a.GetCompanyUserMembershipAsync(companyA.Id, userId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((CompanyUserMembership?)null);

        var handler = f.BuildHandler();
        var result = await handler.Handle(new GetSessionContextQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Branch.Should().BeNull();
        f.BranchRepo.Verify(
            r =>
                r.GetByIdForCompanyAsync(
                    tenant.Id,
                    companyA.Id,
                    branchDeEmpresaB.Id,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once,
            "solo se consulta una vez, para descartar el header — nunca se vuelve a resolver como branch válido"
        );
    }
}
