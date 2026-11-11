using System.Text.RegularExpressions;
using ERP.Application.Auth;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.Companies.UseCases.CreateCompany;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Domain.Tenants.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Companies;

/// <summary>
/// IAM-V1-F3 — alta de empresa con administrador inicial: contraseña temporal generada por el
/// backend (CSPRNG), solo el hash llega al aprovisionamiento, el texto plano viaja únicamente en la
/// respuesta de creación y los duplicados responden 409 genérico antes y durante el aprovisionamiento.
/// </summary>
public sealed class CreateCompanyInitialAdministratorTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();

    private static readonly InitialAdministratorRequest Request =
        new("Acme.Admin", " Ana ", " Pérez ", "Ana@Example.test");

    private sealed class Fixture
    {
        public Mock<ICompanyProvisioningService> Provisioning { get; } = new();
        public Mock<IAccessRepository> Access { get; } = new();
        public InitialCompanyAdministrator? Captured { get; private set; }

        public CreateCompanyHandler Handler()
        {
            var user = new Mock<ICurrentUser>();
            user.SetupGet(u => u.IsAuthenticated).Returns(true);
            user.SetupGet(u => u.UserId).Returns(ActorId);
            var tenants = new Mock<ITenantRepository>();
            tenants
                .Setup(t => t.GetByIdAsync(TenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Tenant.Create("Grupo", "grupo", ActorId));
            var hasher = new Mock<IPasswordHasher>();
            hasher.Setup(h => h.HashPassword(It.IsAny<string>())).Returns<string>(p => "hash::" + p.Length);

            Provisioning
                .Setup(p => p.CreateManagedCompanyAsync(
                    It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
                    It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string?>(), It.IsAny<CancellationToken>(), It.IsAny<InitialCompanyAdministrator?>()))
                .Callback<Guid, string, string, string, Guid, string, string?, string?, string?, string, string, string, string?, CancellationToken, InitialCompanyAdministrator?>(
                    (_, _, _, _, _, _, _, _, _, _, _, _, _, _, admin) => Captured = admin)
                .ReturnsAsync(Company.CreateManaged(TenantId, "1790016919001", "Acme S.A."));

            Access
                .Setup(a => a.GetUserByUsernameAsync("acme.admin", It.IsAny<CancellationToken>()))
                .ReturnsAsync(IdentityUser.Create("acme.admin", "Ana", "Pérez", "ana@example.test", "h", ActorId));

            return new CreateCompanyHandler(Provisioning.Object, user.Object, tenants.Object, Access.Object, hasher.Object);
        }
    }

    [Fact]
    public async Task With_administrator_generates_a_secure_password_hashes_it_and_returns_it_once()
    {
        var fx = new Fixture();
        var result = await fx.Handler().Handle(
            new CreateCompanyCommand(TenantId, "1790016919001", "Acme S.A.", null, Request), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        var credentials = result.Value!.InitialAdministrator;
        credentials.Should().NotBeNull();
        credentials!.Username.Should().Be("acme.admin");
        credentials.TemporaryPassword.Should().HaveLength(TemporaryPasswordGenerator.Length);
        credentials.TemporaryPassword.Should().MatchRegex("[A-Z]").And.MatchRegex("[0-9]");

        // Al aprovisionamiento solo llega el hash y los datos normalizados; jamás el texto plano.
        fx.Captured.Should().NotBeNull();
        fx.Captured!.Username.Should().Be("acme.admin");
        fx.Captured.FirstName.Should().Be("Ana");
        fx.Captured.LastName.Should().Be("Pérez");
        fx.Captured.Email.Should().Be("ana@example.test");
        fx.Captured.PasswordHash.Should().NotContain(credentials.TemporaryPassword);
        fx.Captured.PasswordHash.Should().Be("hash::" + credentials.TemporaryPassword.Length);
    }

    [Fact]
    public async Task Without_administrator_nothing_is_generated_and_the_contract_is_unchanged()
    {
        var fx = new Fixture();
        var result = await fx.Handler().Handle(
            new CreateCompanyCommand(TenantId, "1790016919001", "Acme S.A.", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.InitialAdministrator.Should().BeNull();
        fx.Captured.Should().BeNull();
        fx.Access.Verify(a => a.AnyUserWithUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Duplicate_username_or_email_returns_the_generic_conflict_before_creating_anything(bool byUsername)
    {
        var fx = new Fixture();
        fx.Access.Setup(a => a.AnyUserWithUsernameAsync("acme.admin", It.IsAny<CancellationToken>())).ReturnsAsync(byUsername);
        fx.Access.Setup(a => a.AnyUserWithEmailAsync("ana@example.test", It.IsAny<CancellationToken>())).ReturnsAsync(!byUsername);

        var result = await fx.Handler().Handle(
            new CreateCompanyCommand(TenantId, "1790016919001", "Acme S.A.", null, Request), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(InitialAdministratorIdentityUnavailableException.GenericMessage);
        fx.Provisioning.Invocations.Should().BeEmpty("no se debe crear la empresa si la identidad ya existe");
    }

    [Fact]
    public async Task Identity_conflict_detected_during_provisioning_is_also_a_generic_conflict()
    {
        var fx = new Fixture();
        var handler = fx.Handler();
        fx.Provisioning.Reset();
        fx.Provisioning
            .Setup(p => p.CreateManagedCompanyAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>(), It.IsAny<InitialCompanyAdministrator?>()))
            .ThrowsAsync(new InitialAdministratorIdentityUnavailableException());

        var result = await handler.Handle(
            new CreateCompanyCommand(TenantId, "1790016919001", "Acme S.A.", null, Request), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(InitialAdministratorIdentityUnavailableException.GenericMessage);
    }

    [Fact]
    public void Validator_requires_all_administrator_fields_and_a_valid_email_only_when_present()
    {
        var validator = new CreateCompanyCommandValidator();
        var ok = new CreateCompanyCommand(TenantId, "1790016919001", "Acme", null, Request);
        validator.Validate(ok).IsValid.Should().BeTrue();
        validator.Validate(ok with { InitialAdministrator = null }).IsValid.Should().BeTrue();

        foreach (var bad in new[]
                 {
                     Request with { Username = "a b" },
                     Request with { FirstName = "" },
                     Request with { LastName = "" },
                     Request with { Email = "" },
                     Request with { Email = "no-es-correo" },
                 })
            validator.Validate(ok with { InitialAdministrator = bad }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Generator_produces_unique_policy_compliant_passwords_from_an_unambiguous_alphabet()
    {
        var passwords = Enumerable.Range(0, 500).Select(_ => TemporaryPasswordGenerator.Generate()).ToList();

        passwords.Distinct().Should().HaveCount(passwords.Count);
        passwords.Should().OnlyContain(p =>
            p.Length == TemporaryPasswordGenerator.Length
            && Regex.IsMatch(p, "[A-Z]") && Regex.IsMatch(p, "[a-z]") && Regex.IsMatch(p, "[0-9]")
            && !Regex.IsMatch(p, "[0O1lI]") && Regex.IsMatch(p, "^[A-Za-z0-9]+$"));
    }
}
