using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Enums;
using FluentAssertions;

namespace ERP.Domain.Tests.Access;

public sealed class UserSessionTests
{
    private static UserSession CreateSession(string terminalId = "device-1") =>
        UserSession.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            terminalId
        );

    [Fact]
    public void Create_queda_Active_por_defecto()
    {
        var session = CreateSession();

        session.Status.Should().Be(UserSessionStatus.Active);
        session.IsActive.Should().BeTrue();
        session.ClosedAt.Should().BeNull();
        session.ClosedReason.Should().BeNull();
    }

    [Fact]
    public void Create_asigna_CreatedBy_al_IdentityUserId()
    {
        var session = CreateSession();

        session.CreatedBy.Should().Be(session.IdentityUserId);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void Create_con_Guid_obligatorio_vacio_lanza_ArgumentException(
        bool companyEmpty,
        bool userEmpty,
        bool branchEmpty
    )
    {
        var companyId = companyEmpty ? Guid.Empty : Guid.NewGuid();
        var identityUserId = userEmpty ? Guid.Empty : Guid.NewGuid();
        var branchId = branchEmpty ? Guid.Empty : Guid.NewGuid();

        var act = () =>
            UserSession.Create(Guid.NewGuid(), companyId, identityUserId, branchId, "device-1");

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_con_TerminalId_vacio_lanza_ArgumentException(string? terminalId)
    {
        var act = () =>
            UserSession.Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                terminalId!
            );

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_con_TerminalId_mayor_al_maximo_lanza_ArgumentException()
    {
        var terminalId = new string('x', UserSession.TerminalIdMaxLen + 1);

        var act = () => CreateSession(terminalId);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CloseManually_cierra_la_sesion_con_el_motivo_correspondiente()
    {
        var session = CreateSession();
        var closedBy = Guid.NewGuid();

        session.CloseManually(closedBy);

        session.Status.Should().Be(UserSessionStatus.ClosedManually);
        session.IsActive.Should().BeFalse();
        session.ClosedAt.Should().NotBeNull();
        session.UpdatedBy.Should().Be(closedBy);
    }

    [Fact]
    public void CloseByNewLogin_cierra_la_sesion_con_el_motivo_correspondiente()
    {
        var session = CreateSession();

        session.CloseByNewLogin(Guid.NewGuid());

        session.Status.Should().Be(UserSessionStatus.ClosedByNewLogin);
        session.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Close_es_idempotente_no_sobrescribe_una_sesion_ya_cerrada()
    {
        var session = CreateSession();
        session.CloseManually(Guid.NewGuid());
        var closedAtOriginal = session.ClosedAt;

        session.CloseByNewLogin(Guid.NewGuid());

        session.Status.Should().Be(UserSessionStatus.ClosedManually);
        session.ClosedAt.Should().Be(closedAtOriginal);
    }

    [Fact]
    public void MarkExpired_cierra_la_sesion_con_el_motivo_correspondiente()
    {
        var session = CreateSession();

        session.MarkExpired(Guid.Empty);

        session.Status.Should().Be(UserSessionStatus.Expired);
        session.IsActive.Should().BeFalse();
        session.ClosedAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkExpired_es_idempotente_no_sobrescribe_una_sesion_ya_cerrada()
    {
        var session = CreateSession();
        session.CloseManually(Guid.NewGuid());
        var closedAtOriginal = session.ClosedAt;

        session.MarkExpired(Guid.Empty);

        session.Status.Should().Be(UserSessionStatus.ClosedManually);
        session.ClosedAt.Should().Be(closedAtOriginal);
    }

    [Fact]
    public void MarkExpired_sobre_sesion_ya_expirada_no_falla_ni_reescribe()
    {
        var session = CreateSession();
        session.MarkExpired(Guid.Empty);
        var closedAtOriginal = session.ClosedAt;

        session.MarkExpired(Guid.Empty);

        session.Status.Should().Be(UserSessionStatus.Expired);
        session.ClosedAt.Should().Be(closedAtOriginal);
    }
}
