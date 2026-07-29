using ERP.Application.Common;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Infrastructure.Services;
using FluentAssertions;
using Moq;

namespace ERP.Infrastructure.Tests.Services;

/// <summary>
/// Fase 3 (ADR — Rediseño del módulo de Caja) — CurrentCashSessionService expone el contexto
/// operativo POS: solo la sesión Open del usuario autenticado, en la sucursal activa; nunca crea
/// ni modifica sesiones, nunca lanza excepción por ausencia de sesión.
/// </summary>
public sealed class CurrentCashSessionServiceTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static CashSession OpenSession(
        Guid branchId,
        Guid userId,
        Guid cashRegisterId,
        Guid emissionPointId
    ) =>
        CashSession.Open(
            TenantId,
            CompanyId,
            branchId,
            userId,
            cashRegisterId,
            "CAJA-01",
            "Caja Principal",
            emissionPointId,
            "001",
            50m,
            userId
        );

    private sealed class Fixture
    {
        public Mock<ICashSessionRepository> Repo { get; } = new();
        public Mock<ICurrentTenant> Tenant { get; } = new();
        public Mock<ICurrentUser> User { get; } = new();
        public Mock<ICurrentBranch> Branch { get; } = new();

        public Fixture()
        {
            Tenant.Setup(t => t.TenantId).Returns(TenantId);
            User.Setup(u => u.IsAuthenticated).Returns(true);
            User.Setup(u => u.UserId).Returns(UserId);
        }

        public CurrentCashSessionService BuildService(Guid branchId) =>
            new(Repo.Object, Tenant.Object, User.Object, BuildBranch(branchId));

        private ICurrentBranch BuildBranch(Guid branchId)
        {
            Branch.Setup(b => b.BranchId).Returns(branchId);
            return Branch.Object;
        }
    }

    [Fact]
    public void Usuario_con_sesion_abierta_en_sucursal_activa_devuelve_contexto_correcto()
    {
        var f = new Fixture();
        var branchId = Guid.NewGuid();
        var cashRegisterId = Guid.NewGuid();
        var emissionPointId = Guid.NewGuid();
        var session = OpenSession(branchId, UserId, cashRegisterId, emissionPointId);

        f.Repo.Setup(r => r.GetOpenByUserAsync(TenantId, UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var service = f.BuildService(branchId);

        service.HasOpenSession.Should().BeTrue();
        service.CashSessionId.Should().Be(session.Id);
        service.CashRegisterId.Should().Be(cashRegisterId);
        service.EmissionPointId.Should().Be(emissionPointId);
        service.BranchId.Should().Be(branchId);
        service.CashRegisterCodeSnapshot.Should().Be("CAJA-01");
        service.CashRegisterNameSnapshot.Should().Be("Caja Principal");
    }

    [Fact]
    public void Usuario_sin_sesion_abierta_devuelve_HasOpenSession_false_y_campos_nulos()
    {
        var f = new Fixture();
        var branchId = Guid.NewGuid();
        f.Repo.Setup(r => r.GetOpenByUserAsync(TenantId, UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CashSession?)null);

        var service = f.BuildService(branchId);

        service.HasOpenSession.Should().BeFalse();
        service.CashSessionId.Should().BeNull();
        service.CashRegisterId.Should().BeNull();
        service.EmissionPointId.Should().BeNull();
        service.BranchId.Should().BeNull();
    }

    [Fact]
    public void Sesion_de_otro_usuario_no_es_devuelta()
    {
        var f = new Fixture();
        var branchId = Guid.NewGuid();

        // El repositorio ya filtra por UserId — simulamos que no encuentra nada para este usuario.
        f.Repo.Setup(r => r.GetOpenByUserAsync(TenantId, UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CashSession?)null);

        var service = f.BuildService(branchId);

        service.HasOpenSession.Should().BeFalse();
        f.Repo.Verify(
            r => r.GetOpenByUserAsync(TenantId, UserId, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public void Sesion_cerrada_no_es_devuelta()
    {
        var f = new Fixture();
        var branchId = Guid.NewGuid();
        var session = OpenSession(branchId, UserId, Guid.NewGuid(), Guid.NewGuid());
        session.Close(UserId, new List<CashClosingCount>());

        // GetOpenByUserAsync filtra por Status==Open en el repositorio real; una sesión cerrada
        // nunca sería devuelta por él — simulamos ese contrato retornando null.
        f.Repo.Setup(r => r.GetOpenByUserAsync(TenantId, UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CashSession?)null);

        var service = f.BuildService(branchId);

        service.HasOpenSession.Should().BeFalse();
    }

    [Fact]
    public void Sesion_de_otra_sucursal_no_es_devuelta_en_el_contexto_actual()
    {
        var f = new Fixture();
        var sessionBranchId = Guid.NewGuid();
        var activeBranchId = Guid.NewGuid();
        var session = OpenSession(sessionBranchId, UserId, Guid.NewGuid(), Guid.NewGuid());

        f.Repo.Setup(r => r.GetOpenByUserAsync(TenantId, UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var service = f.BuildService(activeBranchId);

        service
            .HasOpenSession.Should()
            .BeFalse(
                "la sesión pertenece a otra sucursal — no debe exponerse en el contexto operativo activo"
            );
    }

    [Fact]
    public void CashRegisterId_y_EmissionPointId_provienen_de_CashSession()
    {
        var f = new Fixture();
        var branchId = Guid.NewGuid();
        var cashRegisterId = Guid.NewGuid();
        var emissionPointId = Guid.NewGuid();
        var session = OpenSession(branchId, UserId, cashRegisterId, emissionPointId);

        f.Repo.Setup(r => r.GetOpenByUserAsync(TenantId, UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var service = f.BuildService(branchId);

        service.CashRegisterId.Should().Be(session.CashRegisterId);
        service.EmissionPointId.Should().Be(session.EmissionPointId);
    }

    [Fact]
    public void No_consulta_el_repositorio_mas_de_una_vez_por_instancia()
    {
        var f = new Fixture();
        var branchId = Guid.NewGuid();
        var session = OpenSession(branchId, UserId, Guid.NewGuid(), Guid.NewGuid());
        f.Repo.Setup(r => r.GetOpenByUserAsync(TenantId, UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var service = f.BuildService(branchId);

        _ = service.HasOpenSession;
        _ = service.CashSessionId;
        _ = service.CashRegisterId;
        _ = service.EmissionPointId;
        _ = service.BranchId;
        _ = service.CashRegisterCodeSnapshot;
        _ = service.CashRegisterNameSnapshot;

        f.Repo.Verify(
            r => r.GetOpenByUserAsync(TenantId, UserId, It.IsAny<CancellationToken>()),
            Times.Once,
            "el servicio es Scoped (una instancia por request) — debe memoizar y no repetir la consulta"
        );
    }

    [Fact]
    public void Usuario_no_autenticado_devuelve_HasOpenSession_false_sin_consultar_el_repositorio()
    {
        var f = new Fixture();
        f.User.Setup(u => u.IsAuthenticated).Returns(false);
        var service = f.BuildService(Guid.NewGuid());

        service.HasOpenSession.Should().BeFalse();
        f.Repo.Verify(
            r =>
                r.GetOpenByUserAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }
}
