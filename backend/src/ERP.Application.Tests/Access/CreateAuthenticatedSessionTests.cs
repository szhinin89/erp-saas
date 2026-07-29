using ERP.Application.Access.UseCases.CreateAuthenticatedSession;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Persistence;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Enums;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Auth.Entities;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Access;

public sealed class CreateAuthenticatedSessionValidatorTests
{
    private static readonly CreateAuthenticatedSessionValidator Validator = new();

    private static CreateAuthenticatedSessionCommand ValidCommand() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "device-1");

    [Fact]
    public void Command_valido_no_tiene_errores()
    {
        Validator.Validate(ValidCommand()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void TenantId_vacio_es_invalido()
    {
        var result = Validator.Validate(ValidCommand() with { TenantId = Guid.Empty });
        result
            .Errors.Should()
            .Contain(e => e.PropertyName == nameof(CreateAuthenticatedSessionCommand.TenantId));
    }

    [Fact]
    public void BranchId_vacio_es_invalido()
    {
        var result = Validator.Validate(ValidCommand() with { BranchId = Guid.Empty });
        result
            .Errors.Should()
            .Contain(e => e.PropertyName == nameof(CreateAuthenticatedSessionCommand.BranchId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void TerminalId_vacio_es_invalido(string? terminalId)
    {
        var result = Validator.Validate(ValidCommand() with { TerminalId = terminalId! });
        result
            .Errors.Should()
            .Contain(e => e.PropertyName == nameof(CreateAuthenticatedSessionCommand.TerminalId));
    }
}

/// <summary>
/// Invariante estructural exigida por el Contrato de Implementación: RefreshToken nunca conoce
/// UserSession. Se prueba por reflexión para que un cambio futuro que agregue una propiedad de
/// navegación inversa haga fallar el test inmediatamente, sin depender de que alguien recuerde
/// revisar el schema EF a mano.
/// </summary>
public sealed class RefreshTokenNeverReferencesUserSessionTests
{
    [Fact]
    public void RefreshToken_no_tiene_ninguna_propiedad_de_tipo_o_relacionada_con_UserSession()
    {
        var properties = typeof(RefreshToken).GetProperties();

        properties
            .Should()
            .NotContain(p =>
                p.PropertyType == typeof(UserSession)
                || p.Name.Contains("UserSession", StringComparison.OrdinalIgnoreCase)
            );
    }
}

public sealed class CreateAuthenticatedSessionHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid OtherCompanyId = Guid.NewGuid();
    private static readonly Guid IdentityUserId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();

    private static CreateAuthenticatedSessionCommand Command(Guid? companyId = null) =>
        new(TenantId, companyId ?? CompanyId, IdentityUserId, BranchId, "device-1");

    private static RefreshToken NewRefreshTokenEntity() =>
        RefreshToken.Create(
            IdentityUserId,
            TenantId,
            CompanyId,
            RefreshUserType.Identity,
            "hash-" + Guid.NewGuid()
        );

    private static (
        Mock<IUserSessionRepository> sessions,
        Mock<IRefreshTokenService> refresh,
        Mock<IDatabaseExceptionTranslator> dbEx
    ) BuildMocks()
    {
        var sessions = new Mock<IUserSessionRepository>();
        sessions
            .Setup(r =>
                r.GetActiveSessionsAsync(IdentityUserId, TenantId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(Array.Empty<UserSession>());

        var refresh = new Mock<IRefreshTokenService>();
        refresh
            .Setup(r =>
                r.CreateWithoutSaveAsync(
                    IdentityUserId,
                    TenantId,
                    It.IsAny<Guid?>(),
                    RefreshUserType.Identity,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(() => (NewRefreshTokenEntity(), "raw-token-value"));

        var dbEx = new Mock<IDatabaseExceptionTranslator>();
        DatabaseUniqueViolationInfo? none = null;
        dbEx.Setup(d => d.TryGetUniqueViolation(It.IsAny<Exception>(), out none)).Returns(false);

        return (sessions, refresh, dbEx);
    }

    [Fact]
    public async Task Command_valido_crea_UserSession_referenciando_el_RefreshTokenId_correcto()
    {
        var (sessions, refresh, dbEx) = BuildMocks();
        var tokenEntity = NewRefreshTokenEntity();
        refresh
            .Setup(r =>
                r.CreateWithoutSaveAsync(
                    IdentityUserId,
                    TenantId,
                    CompanyId,
                    RefreshUserType.Identity,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((tokenEntity, "raw-token-value"));

        var handler = new CreateAuthenticatedSessionHandler(
            sessions.Object,
            refresh.Object,
            dbEx.Object
        );

        var result = await handler.Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RefreshToken.Should().Be("raw-token-value");
        result.Value.RefreshTokenExpiry.Should().Be(tokenEntity.ExpiresAt);

        sessions.Verify(
            r =>
                r.AddAsync(
                    It.Is<UserSession>(s =>
                        s.RefreshTokenId == tokenEntity.Id && s.Status == UserSessionStatus.Active
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );

        // Única unidad de trabajo: un solo SaveChangesAsync para cierre(s) + RefreshToken + nueva sesión.
        sessions.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Sesion_activa_previa_de_la_misma_empresa_se_cierra_por_nuevo_login()
    {
        var (sessions, refresh, dbEx) = BuildMocks();
        var previous = UserSession.Create(
            TenantId,
            CompanyId,
            IdentityUserId,
            BranchId,
            "old-device"
        );
        sessions
            .Setup(r =>
                r.GetActiveSessionsAsync(IdentityUserId, TenantId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new[] { previous });

        var handler = new CreateAuthenticatedSessionHandler(
            sessions.Object,
            refresh.Object,
            dbEx.Object
        );

        await handler.Handle(Command(), CancellationToken.None);

        previous.Status.Should().Be(UserSessionStatus.ClosedByNewLogin);
        sessions.Verify(r => r.UpdateAsync(previous, It.IsAny<CancellationToken>()), Times.Once);
        sessions.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Sesion_activa_de_otra_empresa_del_mismo_tenant_no_se_toca()
    {
        var (sessions, refresh, dbEx) = BuildMocks();
        var otherCompanySession = UserSession.Create(
            TenantId,
            OtherCompanyId,
            IdentityUserId,
            BranchId,
            "other-device"
        );
        sessions
            .Setup(r =>
                r.GetActiveSessionsAsync(IdentityUserId, TenantId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new[] { otherCompanySession });

        var handler = new CreateAuthenticatedSessionHandler(
            sessions.Object,
            refresh.Object,
            dbEx.Object
        );

        await handler.Handle(Command(companyId: CompanyId), CancellationToken.None);

        otherCompanySession.Status.Should().Be(UserSessionStatus.Active);
        sessions.Verify(
            r => r.UpdateAsync(otherCompanySession, It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Si_SaveChangesAsync_falla_por_una_causa_no_relacionada_a_unicidad_la_excepcion_se_propaga()
    {
        var (sessions, refresh, dbEx) = BuildMocks();
        sessions
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Fallo no relacionado a unicidad."));

        var handler = new CreateAuthenticatedSessionHandler(
            sessions.Object,
            refresh.Object,
            dbEx.Object
        );

        var act = () => handler.Handle(Command(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>
    /// Fase 7: dos logins simultáneos compiten por el índice único parcial
    /// (ux_user_sessions_active_per_company) — el "perdedor" no debe recibir un 500 crudo, sino
    /// un Result.Conflict consistente con el resto del sistema (mismo patrón que
    /// CreateItemCommandHandler vía IDatabaseExceptionTranslator).
    /// </summary>
    [Fact]
    public async Task Si_SaveChangesAsync_falla_por_violacion_de_unicidad_devuelve_Conflict()
    {
        var (sessions, refresh, dbEx) = BuildMocks();
        sessions
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Violación de unicidad simulada."));

        var info = new DatabaseUniqueViolationInfo(
            "23505",
            "ux_user_sessions_active_per_company",
            "user_sessions",
            null
        );
        dbEx.Setup(d => d.TryGetUniqueViolation(It.IsAny<Exception>(), out info)).Returns(true);

        var handler = new CreateAuthenticatedSessionHandler(
            sessions.Object,
            refresh.Object,
            dbEx.Object
        );

        var result = await handler.Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.Conflict);
    }
}
