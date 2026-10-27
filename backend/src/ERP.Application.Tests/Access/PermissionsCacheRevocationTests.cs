using ERP.Application.Access;
using ERP.Application.Access.Authorization;
using ERP.Application.Access.Caching;
using ERP.Application.Common;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Exceptions;
using ERP.Domain.Kernel.Security;
using ERP.Domain.Tenants.Entities;
using ERP.Domain.Tenants.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Access;

/// <summary>
/// SEC-06 — revocación de permisos con caché: protocolo de invalidación fail-closed
/// (<see cref="PermissionsCacheInvalidation"/>), publicación con el sello observado antes de cargar
/// la BD (<see cref="EffectivePermissionKeysProvider"/>) y autorización que no depende de la caché
/// para membership revocada ni para el operador global.
/// </summary>
public sealed class PermissionsCacheRevocationTests
{
    private static readonly PermissionsCacheScope Scope = PermissionsCacheScope.Company(
        Guid.NewGuid()
    );

    // ── Protocolo Begin → mutación → Complete ───────────────────────────────────────────────

    [Fact]
    public async Task Begin_fallido_no_ejecuta_la_mutacion_y_lo_informa()
    {
        var invalidator = PermissionsCacheInvalidatorMocks.Healthy();
        invalidator
            .Setup(c => c.TryBeginAsync(Scope, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var executed = false;

        var result = await invalidator.Object.RunAsync(
            Scope,
            _ =>
            {
                executed = true;
                return Task.FromResult(Result<object>.Success(new { }));
            },
            CancellationToken.None
        );

        executed.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Access.PermissionsInvalidationUnavailable);
        invalidator.Verify(
            c => c.TryCompleteAsync(It.IsAny<PermissionsCacheScope>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Complete_fallido_tras_persistir_informa_invalidacion_incompleta()
    {
        var invalidator = PermissionsCacheInvalidatorMocks.Healthy();
        invalidator
            .Setup(c => c.TryCompleteAsync(Scope, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await invalidator.Object.RunAsync(
            Scope,
            _ => Task.FromResult(Result<object>.Success(new { })),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Access.PermissionsInvalidationIncomplete);
    }

    [Fact]
    public async Task Complete_se_ejecuta_aunque_la_mutacion_falle_y_conserva_el_fallo_original()
    {
        var invalidator = PermissionsCacheInvalidatorMocks.Healthy();

        var result = await invalidator.Object.RunAsync(
            Scope,
            _ => Task.FromResult(Result<object>.NotFound("No existe.")),
            CancellationToken.None
        );

        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        invalidator.Verify(c => c.TryCompleteAsync(Scope, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Complete_se_ejecuta_sin_cancelacion_aunque_la_mutacion_lance()
    {
        var invalidator = PermissionsCacheInvalidatorMocks.Healthy();
        using var cts = new CancellationTokenSource();

        var act = () =>
            invalidator.Object.RunAsync<object>(
                Scope,
                _ => throw new InvalidOperationException("BD caída"),
                cts.Token
            );

        await act.Should().ThrowAsync<InvalidOperationException>();
        invalidator.Verify(c => c.TryCompleteAsync(Scope, CancellationToken.None), Times.Once);
    }

    // ── Provider: publica solo con el sello observado antes de cargar la BD ─────────────────

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid ProfileId = Guid.NewGuid();

    private static Mock<IAccessRepository> RepoReturning(Guid companyId, params string[] keys)
    {
        var repo = new Mock<IAccessRepository>();
        repo.Setup(r =>
                r.GetActiveProfilePermissionsForCompanyAsync(
                    TenantId,
                    companyId,
                    ProfileId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                keys.Select(k => AccessProfilePermission.Create(TenantId, ProfileId, k, true, ProfileId))
                    .ToList()
            );
        return repo;
    }

    [Fact]
    public async Task Provider_publica_con_el_sello_de_la_lectura_previa_a_la_BD_y_el_perfil()
    {
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var steps = new List<string>();
        var stamps = new Queue<PermissionsCacheStamp>(
            [new PermissionsCacheStamp(1, 1, 1, 10), new PermissionsCacheStamp(1, 2, 1, 20)]
        );
        var cache = new Mock<IPermissionsCacheService>();
        cache
            .Setup(c => c.ReadAsync(TenantId, companyId, userId, ProfileId, It.IsAny<CancellationToken>()))
            .Callback(() => steps.Add("read"))
            .ReturnsAsync(() =>
                new PermissionsCacheReadResult(
                    null,
                    PermissionsCacheMissReason.NotFound,
                    stamps.Dequeue()
                )
            );
        PermissionsCacheStamp? written = null;
        cache
            .Setup(c =>
                c.WriteAsync(
                    TenantId,
                    companyId,
                    userId,
                    ProfileId,
                    It.IsAny<IReadOnlyList<string>>(),
                    It.IsAny<PermissionsCacheStamp>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (
                    Guid _,
                    Guid _,
                    Guid _,
                    Guid _,
                    IReadOnlyList<string> _,
                    PermissionsCacheStamp stamp,
                    CancellationToken _
                ) =>
                {
                    steps.Add("write");
                    written = stamp;
                }
            )
            .Returns(Task.CompletedTask);
        var repo = RepoReturning(companyId, "ventas.view");
        repo.Setup(r =>
                r.GetActiveProfilePermissionsForCompanyAsync(
                    TenantId,
                    companyId,
                    ProfileId,
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(() => steps.Add("db"))
            .ReturnsAsync([AccessProfilePermission.Create(TenantId, ProfileId, "ventas.view", true, ProfileId)]);
        var provider = new EffectivePermissionKeysProvider(
            cache.Object,
            Mock.Of<IPermissionsCacheDiagnostics>(),
            repo.Object
        );

        var keys = await provider.GetAllowedKeysAsync(TenantId, companyId, userId, ProfileId);

        keys.Should().Equal("ventas.view");
        steps.Should().Equal("read", "read", "db", "write");
        written.Should().Be(new PermissionsCacheStamp(1, 2, 1, 20));
    }

    [Theory]
    [InlineData(PermissionsCacheMissReason.InvalidationPending)]
    [InlineData(PermissionsCacheMissReason.Exception)]
    public async Task Provider_sin_sello_resuelve_desde_BD_sin_publicar_ni_conceder_de_mas(
        PermissionsCacheMissReason reason
    )
    {
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var cache = new Mock<IPermissionsCacheService>();
        cache
            .Setup(c => c.ReadAsync(TenantId, companyId, userId, ProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PermissionsCacheReadResult(null, reason));
        var provider = new EffectivePermissionKeysProvider(
            cache.Object,
            Mock.Of<IPermissionsCacheDiagnostics>(),
            RepoReturning(companyId, "ventas.view").Object
        );

        var keys = await provider.GetAllowedKeysAsync(TenantId, companyId, userId, ProfileId);

        keys.Should().Equal("ventas.view");
        cache.Verify(
            c =>
                c.WriteAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<IReadOnlyList<string>>(),
                    It.IsAny<PermissionsCacheStamp>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    // ── Autorización que no depende de la caché ─────────────────────────────────────────────

    private static (RuntimePermissionAuthorizer Authorizer, Mock<IEffectivePermissionKeysProvider> Keys) AuthorizerWith(
        OperationalCompanyContext? context,
        bool hasHeader
    )
    {
        var tenant = new Mock<ICurrentTenant>();
        tenant.SetupGet(t => t.TenantId).Returns(TenantId);
        var tenants = new Mock<ITenantRepository>();
        tenants
            .Setup(t => t.GetByIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Tenant.Create("T", $"t-{Guid.NewGuid():N}", Guid.NewGuid()));
        var companyContext = new Mock<ICompanyContextProvider>();
        companyContext
            .Setup(c => c.ResolveOperationalForUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(context);
        var currentCompany = new Mock<ICurrentCompany>();
        currentCompany.SetupGet(c => c.HasCompanyContext).Returns(hasHeader);
        // Caché "envenenada": si se consultara, concedería el permiso revocado.
        var keys = new Mock<IEffectivePermissionKeysProvider>();
        keys.Setup(k =>
                k.GetAllowedKeysAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(["ventas.view"]);
        return (
            new RuntimePermissionAuthorizer(
                tenant.Object,
                tenants.Object,
                companyContext.Object,
                keys.Object,
                currentCompany.Object
            ),
            keys
        );
    }

    [Fact]
    public async Task Membership_revocada_se_rechaza_sin_consultar_la_cache_de_permisos()
    {
        var (authorizer, keys) = AuthorizerWith(context: null, hasHeader: true);

        var act = () => authorizer.IsAuthorizedAsync("ventas.view", Guid.NewGuid(), SecurityRoles.User);

        await act.Should().ThrowAsync<CompanyScopeException>();
        keys.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Operador_global_conserva_su_bypass_sin_consultar_la_cache_de_permisos()
    {
        var userId = Guid.NewGuid();
        var (authorizer, keys) = AuthorizerWith(
            new OperationalCompanyContext(
                Guid.NewGuid(),
                userId,
                null,
                IsActiveMembership: false,
                MembershipRole: SecurityRoles.Admin,
                IsAuthorizedOperator: true
            ),
            hasHeader: true
        );

        (await authorizer.IsAuthorizedAsync("cualquier.permiso", userId, SecurityRoles.Admin))
            .Should()
            .BeTrue();
        keys.VerifyNoOtherCalls();
    }
}
