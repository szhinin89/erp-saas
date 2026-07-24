using ERP.Application.Common;
using ERP.Application.Modules.Caja.UseCases;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Sales.Events;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace ERP.Application.Tests.Caja;

/// <summary>
/// Fase 4 (ADR — Rediseño del módulo de Caja) — SalesInvoiceAuthorizedHandler registra el
/// movimiento de caja usando <see cref="SalesInvoiceAuthorizedEvent.CashSessionId"/> (fijado en
/// la factura al crear el borrador), no una búsqueda ad-hoc de la sesión abierta del usuario.
/// </summary>
public sealed class SalesInvoiceAuthorizedHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static CashSession OpenSession(Guid cashSessionSeed) =>
        CashSession.Open(
            TenantId, CompanyId, BranchId, UserId,
            Guid.NewGuid(), "CAJA-01", "Caja Principal",
            Guid.NewGuid(), "001",
            0m, UserId);

    private static SalesInvoiceAuthorizedHandler BuildHandler(Mock<ICashSessionRepository> cashRepo) =>
        new(cashRepo.Object, MockTenant(), Mock.Of<ILogger<SalesInvoiceAuthorizedHandler>>());

    private static ICurrentTenant MockTenant()
    {
        var t = new Mock<ICurrentTenant>();
        t.Setup(x => x.TenantId).Returns(TenantId);
        return t.Object;
    }

    [Fact]
    public async Task Usa_GetByIdAsync_con_el_CashSessionId_de_la_factura_y_nunca_busca_por_usuario()
    {
        var session = OpenSession(Guid.NewGuid());
        var cashRepo = new Mock<ICashSessionRepository>();
        cashRepo.Setup(r => r.GetByIdAsync(TenantId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var handler = BuildHandler(cashRepo);
        var evt = new SalesInvoiceAuthorizedEvent(Guid.NewGuid(), "001-001-000000001", 115m, UserId, session.Id);

        await handler.Handle(evt, CancellationToken.None);

        cashRepo.Verify(r => r.GetByIdAsync(TenantId, session.Id, It.IsAny<CancellationToken>()), Times.Once);
        cashRepo.Verify(r => r.GetOpenByUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        session.Movements.Should().Contain(m => m.ReferenceNumber == "001-001-000000001" && m.Amount == 115m);
    }

    [Fact]
    public async Task Sesion_inexistente_no_lanza_y_no_registra_movimiento()
    {
        var cashRepo = new Mock<ICashSessionRepository>();
        var missingSessionId = Guid.NewGuid();
        cashRepo.Setup(r => r.GetByIdAsync(TenantId, missingSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CashSession?)null);

        var handler = BuildHandler(cashRepo);
        var evt = new SalesInvoiceAuthorizedEvent(Guid.NewGuid(), "001-001-000000002", 50m, UserId, missingSessionId);

        var act = async () => await handler.Handle(evt, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}
