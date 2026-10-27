using ERP.Application.Access;
using ERP.Application.Access.Authorization;
using ERP.Application.Access.Caching;
using ERP.Application.Access.UseCases.Permissions;
using ERP.Application.Common;
using ERP.Application.Common.Security;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Exceptions;
using ERP.Domain.Kernel.Security;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Tenants.Entities;
using ERP.Domain.Tenants.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Access;

/// <summary>
/// SEC-04 — el bypass Admin se evalúa contra la membership de la empresa operativa
/// (X-Company-Id), nunca contra el rol del JWT; un header sin membership activa se rechaza sin
/// fallback; el administrador global solo conserva acceso transversal vía la política de operador.
/// Cubre <see cref="CompanyContextProvider"/>, <see cref="RuntimePermissionAuthorizer"/> y
/// <see cref="GetMyPermissionsHandler"/> con el provider real (repositorios simulados).
/// </summary>
public sealed class CompanyScopedAdminAuthorizationTests
{
    private const string LimitedKey = "access.sessions.view";
    private const string OtherKey = "access.profiles.view";

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid OtherTenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid LimitedProfileId = Guid.NewGuid();

    private readonly Company _companyA = NewCompany(TenantId);
    private readonly Company _companyB = NewCompany(TenantId);
    private readonly Company _companyC = NewCompany(TenantId);
    private readonly Company _foreignCompany = NewCompany(OtherTenantId);

    private readonly Mock<IAccessRepository> _access = new();
    private readonly Mock<ICompanyRepository> _companies = new();
    private readonly Mock<ICurrentTenant> _tenant = new();
    private readonly Mock<ICurrentCompany> _company = new();
    private readonly Mock<ICurrentUser> _user = new();
    private readonly Mock<IOperatorCompanyAccessPolicy> _operator = new();
    private readonly Mock<ITenantRepository> _tenants = new();
    private readonly Mock<IEffectivePermissionKeysProvider> _keys = new();
    private readonly List<CompanyUserMembership> _memberships = [];

    public CompanyScopedAdminAuthorizationTests()
    {
        _tenant.SetupGet(x => x.TenantId).Returns(TenantId);
        _user.SetupGet(x => x.IsAuthenticated).Returns(true);
        _user.SetupGet(x => x.UserId).Returns(UserId);
        _user.SetupGet(x => x.Role).Returns(SecurityRoles.Admin); // JWT emitido como Admin
        _tenants
            .Setup(x => x.GetByIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Tenant.Create("T", $"t-{Guid.NewGuid():N}", Guid.NewGuid()));

        var all = new[] { _companyA, _companyB, _companyC, _foreignCompany };
        _companies
            .Setup(x =>
                x.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                (IReadOnlyCollection<Guid> ids, CancellationToken _) =>
                    all.Where(c => ids.Contains(c.Id)).ToList()
            );
        _access
            .Setup(x =>
                x.GetCompanyUserMembershipAsync(
                    It.IsAny<Guid>(),
                    UserId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                (Guid companyId, Guid _, CancellationToken _) =>
                    _memberships.FirstOrDefault(m => m.CompanyId == companyId)
            );
        _access
            .Setup(x =>
                x.GetActiveCompanyUserMembershipsForUserSystemAsync(
                    UserId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(() => _memberships.Where(m => m.IsActive).ToList());
        _keys
            .Setup(x =>
                x.GetAllowedKeysAsync(
                    TenantId,
                    It.IsAny<Guid>(),
                    UserId,
                    LimitedProfileId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([LimitedKey]);
    }

    private static Company NewCompany(Guid tenantId) =>
        Company.CreateManaged(
            tenantId,
            taxIdentificationNumber: $"179{Guid.NewGuid():N}"[..13],
            legalName: "Empresa",
            createdBy: Guid.NewGuid()
        );

    private void AdminInAUserInB()
    {
        _memberships.Add(CompanyUserMembership.Create(_companyA.Id, UserId, "Admin", null, UserId));
        _memberships.Add(
            CompanyUserMembership.Create(_companyB.Id, UserId, "User", LimitedProfileId, UserId)
        );
    }

    private void Header(Guid? companyId)
    {
        _company.SetupGet(x => x.CompanyId).Returns(companyId ?? Guid.Empty);
        _company.SetupGet(x => x.HasCompanyContext).Returns(companyId is not null);
    }

    private CompanyContextProvider Provider() =>
        new(
            _access.Object,
            _companies.Object,
            _tenant.Object,
            _company.Object,
            _user.Object,
            _operator.Object
        );

    private RuntimePermissionAuthorizer Authorizer() =>
        new(_tenant.Object, _tenants.Object, Provider(), _keys.Object, _company.Object);

    private Task<bool> IsAuthorized(string key) =>
        Authorizer().IsAuthorizedAsync(key, UserId, SecurityRoles.Admin);

    // Header presente sin membership activa: rechazo de contexto (403 COMPANY_SCOPE_FORBIDDEN vía
    // ExceptionMiddleware), igual que CompanyScopeBehavior — nunca un fallback a otra empresa.
    private async Task AssertCompanyScopeRejected(string key)
    {
        var act = () => IsAuthorized(key);
        await act.Should().ThrowAsync<CompanyScopeException>();
    }

    private async Task<IReadOnlyList<string>> MyPermissions()
    {
        var handler = new GetMyPermissionsHandler(
            _user.Object,
            _tenant.Object,
            _keys.Object,
            Provider()
        );
        var result = await handler.Handle(new GetMyPermissionsQuery(), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        return result.Value!.Permissions;
    }

    [Fact]
    public async Task Admin_en_A_con_JWT_Admin_operando_B_recibe_permisos_de_B_nunca_Admin()
    {
        AdminInAUserInB();
        Header(_companyB.Id);

        (await IsAuthorized(LimitedKey)).Should().BeTrue();
        (await IsAuthorized(OtherKey)).Should().BeFalse();
        (await MyPermissions()).Should().BeEquivalentTo([LimitedKey]);

        var ctx = await Provider().ResolveOperationalForCurrentUserAsync();
        ctx!.HasAdminBypass.Should().BeFalse();
        OperationalCompanyContext
            .ResolveEffectiveRole(ctx, SecurityRoles.Admin)
            .Should()
            .Be("User");
    }

    [Fact]
    public async Task Admin_en_A_operando_A_conserva_bypass_Admin()
    {
        AdminInAUserInB();
        Header(_companyA.Id);

        (await IsAuthorized(OtherKey)).Should().BeTrue();
        (await MyPermissions()).Should().BeEquivalentTo(["*"]);
    }

    [Fact]
    public async Task Admin_en_A_sin_membership_en_C_es_rechazado_sin_fallback()
    {
        _memberships.Add(CompanyUserMembership.Create(_companyA.Id, UserId, "Admin", null, UserId));
        Header(_companyC.Id);

        await AssertCompanyScopeRejected(OtherKey);
        (await MyPermissions()).Should().BeEmpty();
        (await Provider().ResolveOperationalForCurrentUserAsync()).Should().BeNull();
    }

    [Fact]
    public async Task Header_inexistente_no_cae_en_la_unica_membership()
    {
        _memberships.Add(
            CompanyUserMembership.Create(_companyA.Id, UserId, "User", LimitedProfileId, UserId)
        );
        Header(Guid.NewGuid());

        (await Provider().ResolveOperationalForCurrentUserAsync()).Should().BeNull();
        await AssertCompanyScopeRejected(LimitedKey);
    }

    [Fact]
    public async Task Membership_inactiva_en_el_header_se_rechaza_aunque_exista_otra_activa()
    {
        AdminInAUserInB();
        _memberships[1].Deactivate(UserId);
        Header(_companyB.Id);

        (await Provider().ResolveOperationalForCurrentUserAsync()).Should().BeNull();
        await AssertCompanyScopeRejected(OtherKey);
    }

    [Fact]
    public async Task Membership_Admin_en_empresa_de_otro_tenant_nunca_autoriza()
    {
        _memberships.Add(
            CompanyUserMembership.Create(_foreignCompany.Id, UserId, "Admin", null, UserId)
        );
        Header(_foreignCompany.Id);

        (await Provider().ResolveOperationalForCurrentUserAsync()).Should().BeNull();
        await AssertCompanyScopeRejected(OtherKey);
    }

    [Fact]
    public async Task Una_membership_sin_header_conserva_el_comportamiento_existente()
    {
        _memberships.Add(
            CompanyUserMembership.Create(_companyA.Id, UserId, "User", LimitedProfileId, UserId)
        );
        Header(null);

        var ctx = await Provider().ResolveOperationalForCurrentUserAsync();
        ctx!.CompanyId.Should().Be(_companyA.Id);
        (await IsAuthorized(LimitedKey)).Should().BeTrue();
        (await IsAuthorized(OtherKey)).Should().BeFalse();
    }

    [Fact]
    public async Task Multiempresa_sin_header_no_resuelve_empresa()
    {
        AdminInAUserInB();
        Header(null);

        (await Provider().ResolveOperationalForCurrentUserAsync()).Should().BeNull();
        (await IsAuthorized(LimitedKey)).Should().BeFalse();
        OperationalCompanyContext
            .ResolveEffectiveRole(null, SecurityRoles.Admin)
            .Should()
            .Be(SecurityRoles.User);
    }

    [Fact]
    public async Task Operador_global_autorizado_tiene_acceso_transversal_sin_membership()
    {
        _operator
            .Setup(x => x.IsAuthorizedOperatorAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        foreach (var companyId in new Guid?[] { _companyA.Id, _companyC.Id, null })
        {
            Header(companyId);
            (await IsAuthorized(OtherKey)).Should().BeTrue();
            (await MyPermissions()).Should().BeEquivalentTo(["*"]);
        }

        var ctx = await Provider().ResolveOperationalForCurrentUserAsync();
        OperationalCompanyContext
            .ResolveEffectiveRole(ctx, SecurityRoles.Admin)
            .Should()
            .Be(SecurityRoles.Admin);
    }

    [Fact]
    public async Task Operador_no_aplica_a_otro_usuario_distinto_del_autenticado()
    {
        _operator
            .Setup(x => x.IsAuthorizedOperatorAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        Header(_companyC.Id);

        var act = () => Authorizer().IsAuthorizedAsync(OtherKey, Guid.NewGuid(), SecurityRoles.Admin);

        await act.Should().ThrowAsync<CompanyScopeException>();
    }
}
