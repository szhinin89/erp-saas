using ERP.Application.Auth.UseCases.PasswordReset;
using ERP.Application.Common.Config;
using ERP.Application.Common.Interfaces;
using ERP.Application.Tests.TestSupport;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Auth.Entities;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Tenants.Entities;
using ERP.Domain.Tenants.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using CompanyEntity = ERP.Domain.Modules.Company.Entities.Company;

namespace ERP.Application.Tests.Auth;

/// <summary>
/// ZH-AUTH-PASSWORD-RESET-SECURITY-HOTFIX-01 — respuesta neutral en todas las ramas, sin token
/// cuando no hay una cuenta inequívoca, cupo por identidad antes de buscar la cuenta, y ningún
/// secreto (token, enlace, email) en los logs.
/// </summary>
public sealed class ForgotPasswordHandlerTests
{
    private const string Email = "ana@test.com";
    private static readonly Guid Actor = Guid.NewGuid();

    private sealed class Fixture
    {
        public Mock<IAccessRepository> Access { get; } = new();
        public Mock<ITenantRepository> Tenants { get; } = new();
        public Mock<ICompanyRepository> Companies { get; } = new();
        public Mock<IPasswordResetTokenRepository> Tokens { get; } = new();
        public Mock<IPasswordResetLinkSender> Sender { get; } = new();
        public Mock<IPasswordResetRequestThrottle> Throttle { get; } = new();
        public CapturingLogger<ForgotPasswordHandler> Logger { get; } = new();
        public List<PasswordResetToken> AddedTokens { get; } = new();
        public List<string> SentLinks { get; } = new();

        public Fixture()
        {
            Throttle
                .Setup(t => t.TryAcquireAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            Tokens
                .Setup(r => r.AddAsync(It.IsAny<PasswordResetToken>(), It.IsAny<CancellationToken>()))
                .Callback<PasswordResetToken, CancellationToken>((t, _) => AddedTokens.Add(t))
                .Returns(Task.CompletedTask);
            Sender
                .Setup(s => s.SendPasswordResetLinkAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, CancellationToken>((_, link, _) => SentLinks.Add(link))
                .Returns(Task.CompletedTask);
        }

        public ForgotPasswordHandler Build() =>
            new(
                Access.Object,
                Tenants.Object,
                Companies.Object,
                Tokens.Object,
                Sender.Object,
                Throttle.Object,
                Options.Create(new PasswordResetOptions { PublicBaseUrl = "https://app.test" }),
                new ForgotPasswordCommandValidator(),
                Logger
            );

        /// <summary>Usuario activo con membresías en las empresas indicadas.</summary>
        public IdentityUser GivenUser(params CompanyEntity[] companies)
        {
            var user = IdentityUser.Create("ana.perez", "Ana", "Perez", Email, "hash", Actor);
            Access.Setup(r => r.GetUserByEmailAsync(Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
            var memberships = companies
                .Select(c => CompanyUserMembership.Create(c.Id, user.Id, "Admin", null, Actor))
                .ToList();
            Access
                .Setup(r => r.GetActiveCompanyUserMembershipsForUserSystemAsync(user.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(memberships);
            Companies
                .Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyCollection<Guid> ids, CancellationToken _) =>
                    companies.Where(c => ids.Contains(c.Id)).ToList()
                );
            return user;
        }

        public Tenant GivenTenant(bool active = true)
        {
            var tenant = Tenant.Create("Tenant", $"t-{Guid.NewGuid():N}", Actor);
            if (!active)
                tenant.Deactivate(Actor);
            Tenants.Setup(r => r.GetByIdAsync(tenant.Id, It.IsAny<CancellationToken>())).ReturnsAsync(tenant);
            return tenant;
        }
    }

    private static CompanyEntity CompanyOf(Guid tenantId) =>
        CompanyEntity.CreateManaged(tenantId, "1790000000001", "Empresa", createdBy: Actor);

    /// <summary>Mismo algoritmo que PasswordResetTokenCrypto.Hash (internal): SHA-256 en Base64.</summary>
    private static string HashOf(string raw) =>
        Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw)));

    private static Task<ERP.Application.Common.Result<bool>> Send(Fixture f, string email = Email) =>
        f.Build().Handle(new ForgotPasswordCommand(email), CancellationToken.None);

    // C/D/E — la respuesta pública no distingue ninguna rama.
    [Fact]
    public async Task Cuenta_existente_inexistente_y_multiple_responden_exactamente_igual()
    {
        var existing = new Fixture();
        var tenant = existing.GivenTenant();
        existing.GivenUser(CompanyOf(tenant.Id));

        var nonexistent = new Fixture();

        var multiple = new Fixture();
        multiple.GivenUser(CompanyOf(Guid.NewGuid()), CompanyOf(Guid.NewGuid()));

        var results = new[] { await Send(existing), await Send(nonexistent), await Send(multiple) };

        results.Should().AllSatisfy(r =>
        {
            r.IsSuccess.Should().BeTrue();
            r.Value.Should().BeTrue();
            r.Error.Should().BeNullOrEmpty();
        });
        existing.AddedTokens.Should().ContainSingle();
    }

    // F — cuenta inexistente: sin token ni entrega.
    [Fact]
    public async Task Cuenta_inexistente_no_crea_token_ni_entrega()
    {
        var f = new Fixture();

        var result = await Send(f);

        result.IsSuccess.Should().BeTrue();
        f.AddedTokens.Should().BeEmpty();
        f.Tokens.Verify(r => r.InvalidateActiveForUserAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
        f.Sender.VerifyNoOtherCalls();
        f.Logger.Entries.Should().Contain(e => e.EventId.Name == "PasswordResetRequestSuppressed" && e.Message.Contains("NoAccount"));
    }

    // G — membresías en varios tenants: no se elige uno, no hay token ni entrega.
    [Fact]
    public async Task Multiples_tenants_no_resolubles_no_crean_token_ni_entrega()
    {
        var f = new Fixture();
        var user = f.GivenUser(CompanyOf(Guid.NewGuid()), CompanyOf(Guid.NewGuid()));

        var result = await Send(f);

        result.IsSuccess.Should().BeTrue();
        f.AddedTokens.Should().BeEmpty();
        f.Sender.VerifyNoOtherCalls();
        f.Logger.Entries.Should().Contain(e =>
            e.EventId.Name == "PasswordResetRequestSuppressed"
            && e.Message.Contains("AmbiguousTenant")
            && e.Message.Contains(user.Id.ToString())
        );
    }

    [Fact]
    public async Task Varias_empresas_del_mismo_tenant_si_emiten_token()
    {
        var f = new Fixture();
        var tenant = f.GivenTenant();
        f.GivenUser(CompanyOf(tenant.Id), CompanyOf(tenant.Id));

        await Send(f);

        f.AddedTokens.Should().ContainSingle().Which.TenantId.Should().Be(tenant.Id);
        f.SentLinks.Should().ContainSingle();
    }

    [Fact]
    public async Task Cuenta_inactiva_o_tenant_inactivo_responden_neutral_sin_token()
    {
        var inactiveUser = new Fixture();
        var tenantA = inactiveUser.GivenTenant();
        inactiveUser.GivenUser(CompanyOf(tenantA.Id)).Deactivate(Actor);

        var inactiveTenant = new Fixture();
        var tenantB = inactiveTenant.GivenTenant(active: false);
        inactiveTenant.GivenUser(CompanyOf(tenantB.Id));

        var noMembership = new Fixture();
        noMembership.GivenUser();

        foreach (var f in new[] { inactiveUser, inactiveTenant, noMembership })
        {
            var result = await Send(f);
            result.IsSuccess.Should().BeTrue();
            f.AddedTokens.Should().BeEmpty();
            f.Sender.VerifyNoOtherCalls();
        }
    }

    // A/B — ni el token raw ni el enlace ni el email aparecen en ningún log.
    [Fact]
    public async Task Cuenta_existente_entrega_enlace_pero_nunca_registra_token_enlace_ni_email()
    {
        var f = new Fixture();
        var tenant = f.GivenTenant();
        var user = f.GivenUser(CompanyOf(tenant.Id));

        await Send(f);

        var link = f.SentLinks.Should().ContainSingle().Subject;
        var rawToken = Uri.UnescapeDataString(link.Split("token=")[1].Split('&')[0]);
        rawToken.Should().NotBeNullOrWhiteSpace();
        f.AddedTokens.Single().TokenHash.Should().Be(HashOf(rawToken));

        var logs = f.Logger.AllText();
        logs.Should().NotContain(rawToken);
        logs.Should().NotContain(Uri.EscapeDataString(rawToken));
        logs.Should().NotContain(link);
        logs.Should().NotContain("token=");
        logs.Should().NotContain(f.AddedTokens.Single().TokenHash);
        logs.Should().NotContain(Email);
        f.Logger.Entries.Select(e => e.EventId.Name)
            .Should().Equal("PasswordResetRequested", "PasswordResetDeliveryRequested");
        f.Logger.Entries.Last().Message.Should().Contain(user.Id.ToString());
    }

    // K — emitir un token nuevo invalida los anteriores antes de persistirlo.
    [Fact]
    public async Task Nueva_solicitud_invalida_tokens_activos_previos_antes_de_emitir()
    {
        var f = new Fixture();
        var tenant = f.GivenTenant();
        var user = f.GivenUser(CompanyOf(tenant.Id));
        var order = new List<string>();
        f.Tokens
            .Setup(r => r.InvalidateActiveForUserAsync(user.Id, PasswordResetToken.KindIdentity, tenant.Id, It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("invalidate"))
            .Returns(Task.CompletedTask);
        f.Tokens
            .Setup(r => r.AddAsync(It.IsAny<PasswordResetToken>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("add"))
            .Returns(Task.CompletedTask);

        await Send(f);

        order.Should().Equal("invalidate", "add");
    }

    // M/N — el cupo por identidad se consume antes de buscar la cuenta y responde neutral.
    [Fact]
    public async Task Cupo_por_identidad_agotado_responde_neutral_sin_consultar_la_cuenta()
    {
        var f = new Fixture();
        var tenant = f.GivenTenant();
        f.GivenUser(CompanyOf(tenant.Id));
        f.Throttle
            .Setup(t => t.TryAcquireAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await Send(f);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
        f.AddedTokens.Should().BeEmpty();
        f.Access.Verify(r => r.GetUserByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        f.Logger.Entries.Should().Contain(e => e.Message.Contains("RateLimited"));
    }

    [Fact]
    public async Task El_cupo_se_aplica_igual_a_cuentas_inexistentes_con_el_email_normalizado()
    {
        var f = new Fixture();

        await Send(f, "  Nadie@Test.COM ");

        f.Throttle.Verify(t => t.TryAcquireAsync("nadie@test.com", It.IsAny<CancellationToken>()), Times.Once);
    }

    // Formato inválido: error de validación (no depende de la cuenta) y no consume cupo.
    [Fact]
    public async Task Email_con_formato_invalido_devuelve_error_de_validacion_sin_consumir_cupo()
    {
        var f = new Fixture();

        var result = await Send(f, "no-es-email");

        result.IsSuccess.Should().BeFalse();
        f.Throttle.VerifyNoOtherCalls();
    }

    // Fallo técnico real: no se disfraza de respuesta neutral.
    [Fact]
    public async Task Fallo_tecnico_al_buscar_la_cuenta_se_propaga()
    {
        var f = new Fixture();
        f.Access
            .Setup(r => r.GetUserByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));

        var act = () => Send(f);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
