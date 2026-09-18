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
            TenantId,
            CompanyId,
            BranchId,
            UserId,
            Guid.NewGuid(),
            "CAJA-01",
            "Caja Principal",
            Guid.NewGuid(),
            "001",
            0m,
            UserId
        );

    private static SalesInvoiceAuthorizedHandler BuildHandler(
        Mock<ICashSessionRepository> cashRepo
    ) => new(cashRepo.Object, MockTenant(), Mock.Of<ILogger<SalesInvoiceAuthorizedHandler>>());

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
        cashRepo
            .Setup(r => r.GetByIdAsync(TenantId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var handler = BuildHandler(cashRepo);
        var evt = new SalesInvoiceAuthorizedEvent(
            Guid.NewGuid(),
            "001-001-000000001",
            115m,
            UserId,
            session.Id,
            TenantId,
            CompanyId,
            new DateOnly(2026, 7, 25),
            100m,
            15m,
            0m,
            0m
        );

        await handler.Handle(evt, CancellationToken.None);

        cashRepo.Verify(
            r => r.GetByIdAsync(TenantId, session.Id, It.IsAny<CancellationToken>()),
            Times.Once
        );
        cashRepo.Verify(
            r =>
                r.GetOpenByUserAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
        session
            .Movements.Should()
            .Contain(m => m.ReferenceNumber == "001-001-000000001" && m.Amount == 115m);
    }

    [Fact]
    public async Task Sesion_inexistente_no_lanza_y_no_registra_movimiento()
    {
        var cashRepo = new Mock<ICashSessionRepository>();
        var missingSessionId = Guid.NewGuid();
        cashRepo
            .Setup(r => r.GetByIdAsync(TenantId, missingSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CashSession?)null);

        var handler = BuildHandler(cashRepo);
        var evt = new SalesInvoiceAuthorizedEvent(
            Guid.NewGuid(),
            "001-001-000000002",
            50m,
            UserId,
            missingSessionId,
            TenantId,
            CompanyId,
            new DateOnly(2026, 7, 25),
            50m,
            0m,
            0m,
            0m
        );

        var act = async () => await handler.Handle(evt, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    // ── SALES-CASH-REAL-MONEY-01 — Caja registra dinero real (CashApplied), no GrandTotal ────

    [Fact]
    public async Task Contado_completo_registra_el_monto_realmente_pagado_no_GrandTotal()
    {
        // $4.00 pagado $3.99 (tolerancia) — caja debe registrar $3.99, nunca $4.00.
        var session = OpenSession(Guid.NewGuid());
        var cashRepo = new Mock<ICashSessionRepository>();
        cashRepo
            .Setup(r => r.GetByIdAsync(TenantId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var handler = BuildHandler(cashRepo);
        var evt = new SalesInvoiceAuthorizedEvent(
            Guid.NewGuid(),
            "001-001-000000003",
            4.00m,
            UserId,
            session.Id,
            TenantId,
            CompanyId,
            new DateOnly(2026, 7, 25),
            3.48m,
            0.52m,
            0m,
            0m,
            0m,
            cashApplied: 3.99m,
            physicalCashApplied: 3.99m
        );

        await handler.Handle(evt, CancellationToken.None);

        session.Movements.Should().ContainSingle(m => m.Amount == 3.99m);
    }

    [Fact]
    public async Task Venta_parcial_registra_solo_el_abono_real_physicalCashApplied()
    {
        // $10.00 total, abono real en efectivo $4.00 — caja registra $4.00, no el total ni el saldo pendiente.
        var session = OpenSession(Guid.NewGuid());
        var cashRepo = new Mock<ICashSessionRepository>();
        cashRepo
            .Setup(r => r.GetByIdAsync(TenantId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var handler = BuildHandler(cashRepo);
        var evt = new SalesInvoiceAuthorizedEvent(
            Guid.NewGuid(),
            "001-001-000000004",
            10.00m,
            UserId,
            session.Id,
            TenantId,
            CompanyId,
            new DateOnly(2026, 7, 25),
            8.70m,
            1.30m,
            0m,
            0m,
            0m,
            cashApplied: 4.00m,
            physicalCashApplied: 4.00m
        );

        await handler.Handle(evt, CancellationToken.None);

        session.Movements.Should().ContainSingle(m => m.Amount == 4.00m);
    }

    [Fact]
    public async Task Credito_puro_con_cashApplied_cero_no_crea_movimiento_de_caja()
    {
        // Venta 100% a crédito (CashApplied == 0, PhysicalCashApplied == 0) — ningún movimiento
        // debe crearse, ni siquiera $0.
        var session = OpenSession(Guid.NewGuid());
        var cashRepo = new Mock<ICashSessionRepository>();
        cashRepo
            .Setup(r => r.GetByIdAsync(TenantId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var handler = BuildHandler(cashRepo);
        var evt = new SalesInvoiceAuthorizedEvent(
            Guid.NewGuid(),
            "001-001-000000005",
            10.00m,
            UserId,
            session.Id,
            TenantId,
            CompanyId,
            new DateOnly(2026, 7, 25),
            8.70m,
            1.30m,
            0m,
            0m,
            0m,
            cashApplied: 0m,
            physicalCashApplied: 0m
        );

        await handler.Handle(evt, CancellationToken.None);

        session.Movements.Should()
            .NotContain(m => m.ReferenceNumber == "001-001-000000005");
    }

    [Fact]
    public async Task Contado_completo_pagado_exacto_registra_el_mismo_monto()
    {
        // $4.00 pagado $4.00 — caso base, sigue funcionando igual que antes del fix.
        var session = OpenSession(Guid.NewGuid());
        var cashRepo = new Mock<ICashSessionRepository>();
        cashRepo
            .Setup(r => r.GetByIdAsync(TenantId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var handler = BuildHandler(cashRepo);
        var evt = new SalesInvoiceAuthorizedEvent(
            Guid.NewGuid(),
            "001-001-000000006",
            4.00m,
            UserId,
            session.Id,
            TenantId,
            CompanyId,
            new DateOnly(2026, 7, 25),
            3.48m,
            0.52m,
            0m,
            0m,
            0m,
            cashApplied: 4.00m,
            physicalCashApplied: 4.00m
        );

        await handler.Handle(evt, CancellationToken.None);

        session.Movements.Should().ContainSingle(m => m.Amount == 4.00m);
    }

    // ── CASH-SESSION-PHYSICAL-CASH-SSOT-01 — Caja registra PhysicalCashApplied, no CashApplied ──

    [Fact]
    public async Task Venta_100_por_ciento_Transferencia_no_crea_movimiento_de_caja()
    {
        // CashApplied = 0.63 (dinero real, no crédito) pero PhysicalCashApplied = 0 (Transferencia
        // no mueve el cajón físico) — caso real: factura 001-001-000000013.
        var session = OpenSession(Guid.NewGuid());
        var cashRepo = new Mock<ICashSessionRepository>();
        cashRepo
            .Setup(r => r.GetByIdAsync(TenantId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var handler = BuildHandler(cashRepo);
        var evt = new SalesInvoiceAuthorizedEvent(
            Guid.NewGuid(),
            "001-001-000000013",
            0.63m,
            UserId,
            session.Id,
            TenantId,
            CompanyId,
            new DateOnly(2026, 9, 18),
            0.56m,
            0.07m,
            0m,
            0m,
            0m,
            cashApplied: 0.63m,
            physicalCashApplied: 0m
        );

        await handler.Handle(evt, CancellationToken.None);

        session.Movements.Should().NotContain(m => m.ReferenceNumber == "001-001-000000013");
    }

    [Fact]
    public async Task Venta_100_por_ciento_Tarjeta_no_crea_movimiento_de_caja()
    {
        var session = OpenSession(Guid.NewGuid());
        var cashRepo = new Mock<ICashSessionRepository>();
        cashRepo
            .Setup(r => r.GetByIdAsync(TenantId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var handler = BuildHandler(cashRepo);
        var evt = new SalesInvoiceAuthorizedEvent(
            Guid.NewGuid(),
            "001-001-000000014",
            50.00m,
            UserId,
            session.Id,
            TenantId,
            CompanyId,
            new DateOnly(2026, 9, 18),
            44.64m,
            5.36m,
            0m,
            0m,
            0m,
            cashApplied: 50.00m,
            physicalCashApplied: 0m
        );

        await handler.Handle(evt, CancellationToken.None);

        session.Movements.Should().NotContain(m => m.ReferenceNumber == "001-001-000000014");
    }

    [Fact]
    public async Task Venta_100_por_ciento_Cheque_no_crea_movimiento_de_caja()
    {
        var session = OpenSession(Guid.NewGuid());
        var cashRepo = new Mock<ICashSessionRepository>();
        cashRepo
            .Setup(r => r.GetByIdAsync(TenantId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var handler = BuildHandler(cashRepo);
        var evt = new SalesInvoiceAuthorizedEvent(
            Guid.NewGuid(),
            "001-001-000000015",
            20.00m,
            UserId,
            session.Id,
            TenantId,
            CompanyId,
            new DateOnly(2026, 9, 18),
            17.86m,
            2.14m,
            0m,
            0m,
            0m,
            cashApplied: 20.00m,
            physicalCashApplied: 0m
        );

        await handler.Handle(evt, CancellationToken.None);

        session.Movements.Should().NotContain(m => m.ReferenceNumber == "001-001-000000015");
    }

    [Fact]
    public async Task Venta_mixta_Efectivo_mas_Transferencia_registra_solo_la_porcion_en_efectivo()
    {
        // $30 total: $10 Efectivo + $20 Transferencia — CashApplied = 30, PhysicalCashApplied = 10.
        var session = OpenSession(Guid.NewGuid());
        var cashRepo = new Mock<ICashSessionRepository>();
        cashRepo
            .Setup(r => r.GetByIdAsync(TenantId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var handler = BuildHandler(cashRepo);
        var evt = new SalesInvoiceAuthorizedEvent(
            Guid.NewGuid(),
            "001-001-000000016",
            30.00m,
            UserId,
            session.Id,
            TenantId,
            CompanyId,
            new DateOnly(2026, 9, 18),
            26.79m,
            3.21m,
            0m,
            0m,
            0m,
            cashApplied: 30.00m,
            physicalCashApplied: 10.00m
        );

        await handler.Handle(evt, CancellationToken.None);

        session.Movements.Should().ContainSingle(m => m.Amount == 10.00m);
    }
}
