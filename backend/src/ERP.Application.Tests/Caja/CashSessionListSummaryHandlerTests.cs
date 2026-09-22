using ERP.Application.Common;
using ERP.Application.Modules.Caja.DTOs;
using ERP.Application.Modules.Caja.UseCases;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Enums;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Domain.Modules.Sales.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Caja;

/// <summary>
/// CASH-SESSION-LIST-SUMMARY-01 — <see cref="GetCashSessionListHandler"/> arma el listado de
/// turnos con información operativa (cajero, facturas del turno, cobros por forma) sin dejar de
/// ser la única fuente de efectivo físico vía CashSession/CashMovement. Un solo query extra por
/// página para SalesInvoice+SalesInvoicePayment (todas las sesiones a la vez), uno para
/// PaymentMethod y uno para nombres de usuario — nunca N+1 por fila.
/// </summary>
public sealed class CashSessionListSummaryHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid OtherBranchId = Guid.NewGuid();

    public sealed class Methods
    {
        public PaymentMethod Efectivo { get; } =
            PaymentMethod.Create(TenantId, "EFECTIVO", "Efectivo", false, false, 1, Guid.NewGuid(), affectsPhysicalCash: true);
        public PaymentMethod Transferencia { get; } =
            PaymentMethod.Create(
                TenantId, "TRANSFERENCIA", "Transferencia Bancaria", true, false, 2, Guid.NewGuid(),
                detailType: PaymentMethodDetailType.Transfer
            );
        public PaymentMethod Credito { get; } =
            PaymentMethod.Create(TenantId, "CREDITO", "Crédito", false, true, 5, Guid.NewGuid());

        public IReadOnlyList<PaymentMethod> All => [Efectivo, Transferencia, Credito];
    }

    private static CashSession OpenSession(Guid branchId, Guid userId, decimal openingAmount = 0m) =>
        CashSession.Open(
            TenantId, CompanyId, branchId, userId, Guid.NewGuid(),
            "CAJA-01", "Caja Principal", Guid.NewGuid(), "001", openingAmount, userId
        );

    private static CashClosingCount Count(Guid sessionId, decimal denomination, int quantity) =>
        CashClosingCount.Create(sessionId, TenantId, denomination, $"Billete ${denomination}", quantity);

    private static SalesInvoiceCashSessionPaymentRow Row(
        Guid cashSessionId, Guid invoiceId, string invoiceNumber, decimal grandTotal,
        PaymentMethod method, decimal amount, DateTime? authorizedAt = null
    ) =>
        new(
            cashSessionId, invoiceId, invoiceNumber,
            authorizedAt ?? new DateTime(2026, 9, 18, 10, 0, 0, DateTimeKind.Utc),
            "Cliente Test", grandTotal, method.Id, method.Code, method.Name, amount,
            null, null, null, null, null
        );

    private sealed class Fixture
    {
        public Mock<ICashSessionRepository> CashRepo { get; } = new();
        public Mock<ISalesInvoiceRepository> InvoiceRepo { get; } = new();
        public Mock<IPaymentMethodRepository> PaymentMethodRepo { get; } = new();
        public Mock<IAccessRepository> AccessRepo { get; } = new();
        public Mock<ICurrentTenant> Tenant { get; } = new();
        public Mock<ICurrentBranch> Branch { get; } = new();
        public Methods Methods { get; } = new();

        public Fixture(Guid activeBranchId)
        {
            Tenant.Setup(t => t.TenantId).Returns(TenantId);
            Branch.Setup(b => b.BranchId).Returns(activeBranchId);
            PaymentMethodRepo
                .Setup(r => r.ListAsync(TenantId, false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Methods.All);
            AccessRepo
                .Setup(r => r.GetUsersByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<IdentityUser>());
            InvoiceRepo
                .Setup(r => r.GetCollectionSummaryByCashSessionsAsync(
                    TenantId, activeBranchId, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<SalesInvoiceCashSessionPaymentRow>());
        }

        public void SetupPaged(IReadOnlyList<CashSession> items, int total = -1) =>
            CashRepo
                .Setup(r => r.GetPagedAsync(TenantId, Branch.Object.BranchId, null, 1, 25, It.IsAny<CancellationToken>()))
                .ReturnsAsync((items, total < 0 ? items.Count : total));

        public GetCashSessionListHandler BuildHandler() =>
            new(CashRepo.Object, InvoiceRepo.Object, PaymentMethodRepo.Object, AccessRepo.Object, Tenant.Object, Branch.Object);
    }

    private static Task<Result<CashSessionListResponse>> RunAsync(Fixture f) =>
        f.BuildHandler().Handle(new GetCashSessionListQuery(), CancellationToken.None);

    [Fact]
    public async Task Turno_abierto_ExpectedCash_es_el_saldo_actual_y_no_hay_contado_ni_diferencia()
    {
        var userId = Guid.NewGuid();
        var session = OpenSession(BranchId, userId, 100m);
        session.RecordMovement(CashMovementType.SaleIncome, 20m, "Venta 001", userId, CashReferenceType.SalesInvoice, Guid.NewGuid(), "001-001-000000001");
        var f = new Fixture(BranchId);
        f.SetupPaged([session]);

        var result = await RunAsync(f);

        result.IsSuccess.Should().BeTrue(result.Error);
        var dto = result.Value!.Items.Single();
        dto.Status.Should().Be("Open");
        dto.ExpectedCash.Should().Be(session.CurrentBalance);
        dto.CountedAmount.Should().BeNull();
        dto.Difference.Should().BeNull();
    }

    [Fact]
    public async Task Turno_cerrado_ExpectedCash_usa_ExpectedAmount_congelado_al_cierre()
    {
        var userId = Guid.NewGuid();
        var closedBy = Guid.NewGuid();
        var session = OpenSession(BranchId, userId, 100m);
        session.Close(closedBy, [Count(session.Id, 1m, 100)]);
        var f = new Fixture(BranchId);
        f.SetupPaged([session]);

        var result = await RunAsync(f);

        var dto = result.Value!.Items.Single();
        dto.Status.Should().Be("Closed");
        dto.ExpectedCash.Should().Be(session.ExpectedAmount);
        dto.CountedAmount.Should().Be(100m);
        dto.ClosedBy.Should().Be(closedBy);
    }

    [Theory]
    [InlineData(120, 100, 20)]   // sobrante — diferencia positiva
    [InlineData(80, 100, -20)]   // faltante — diferencia negativa
    public async Task Diferencia_positiva_o_negativa_se_expone_igual_que_en_el_dominio(
        decimal counted, decimal opening, decimal expectedDifference
    )
    {
        var userId = Guid.NewGuid();
        var session = OpenSession(BranchId, userId, opening);
        session.Close(userId, [Count(session.Id, 1m, (int)counted)]);
        var f = new Fixture(BranchId);
        f.SetupPaged([session]);

        var result = await RunAsync(f);

        result.Value!.Items.Single().Difference.Should().Be(expectedDifference);
    }

    [Fact]
    public async Task Efectivo_suma_a_SaleIncomeCash_y_a_facturas_del_turno()
    {
        var userId = Guid.NewGuid();
        var session = OpenSession(BranchId, userId);
        var invoiceId = Guid.NewGuid();
        session.RecordMovement(CashMovementType.SaleIncome, 30m, "Venta", userId, CashReferenceType.SalesInvoice, invoiceId, "001-001-000000001");
        var f = new Fixture(BranchId);
        f.SetupPaged([session]);
        f.InvoiceRepo
            .Setup(r => r.GetCollectionSummaryByCashSessionsAsync(TenantId, BranchId, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Row(session.Id, invoiceId, "001-001-000000001", 30m, f.Methods.Efectivo, 30m) });

        var result = await RunAsync(f);

        var dto = result.Value!.Items.Single();
        dto.InvoiceCount.Should().Be(1);
        dto.TotalInvoiced.Should().Be(30m);
        dto.SaleIncomeCash.Should().Be(30m);
        dto.ByPaymentMethod.Should().ContainSingle(m => m.PaymentMethodId == f.Methods.Efectivo.Id && m.Amount == 30m);
    }

    [Fact]
    public async Task Transferencia_suma_a_facturas_del_turno_pero_no_a_SaleIncomeCash_ni_a_ExpectedCash()
    {
        // Regla central del ticket: cobros no-efectivo nunca alimentan el saldo físico.
        var userId = Guid.NewGuid();
        var session = OpenSession(BranchId, userId, 50m);
        var invoiceId = Guid.NewGuid();
        // Sin RecordMovement — una Transferencia nunca crea CashMovement (PhysicalCashApplied == 0).
        var f = new Fixture(BranchId);
        f.SetupPaged([session]);
        f.InvoiceRepo
            .Setup(r => r.GetCollectionSummaryByCashSessionsAsync(TenantId, BranchId, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Row(session.Id, invoiceId, "001-001-000000002", 15m, f.Methods.Transferencia, 15m) });

        var result = await RunAsync(f);

        var dto = result.Value!.Items.Single();
        dto.TotalInvoiced.Should().Be(15m);
        dto.SaleIncomeCash.Should().Be(0m);
        dto.ExpectedCash.Should().Be(50m, "el efectivo físico no se mueve por una venta 100% Transferencia");
        dto.ByPaymentMethod.Should().ContainSingle(m => m.PaymentMethodId == f.Methods.Transferencia.Id && m.Amount == 15m);
    }

    [Fact]
    public async Task Venta_mixta_reparte_por_forma_y_la_factura_cuenta_una_sola_vez()
    {
        var userId = Guid.NewGuid();
        var session = OpenSession(BranchId, userId);
        var invoiceId = Guid.NewGuid();
        session.RecordMovement(CashMovementType.SaleIncome, 1.59m, "Venta", userId, CashReferenceType.SalesInvoice, invoiceId, "001-001-000000003");
        var f = new Fixture(BranchId);
        f.SetupPaged([session]);
        f.InvoiceRepo
            .Setup(r => r.GetCollectionSummaryByCashSessionsAsync(TenantId, BranchId, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                Row(session.Id, invoiceId, "001-001-000000003", 2.59m, f.Methods.Efectivo, 1.59m),
                Row(session.Id, invoiceId, "001-001-000000003", 2.59m, f.Methods.Transferencia, 1.00m),
            });

        var result = await RunAsync(f);

        var dto = result.Value!.Items.Single();
        dto.InvoiceCount.Should().Be(1, "la factura mixta cuenta una sola vez");
        dto.TotalInvoiced.Should().Be(2.59m);
        dto.ByPaymentMethod.Single(m => m.PaymentMethodId == f.Methods.Efectivo.Id).Amount.Should().Be(1.59m);
        dto.ByPaymentMethod.Single(m => m.PaymentMethodId == f.Methods.Transferencia.Id).Amount.Should().Be(1.00m);
        dto.SaleIncomeCash.Should().Be(1.59m, "solo la porción en efectivo mueve el cajón físico");
    }

    [Fact]
    public async Task Multiples_cajas_y_usuarios_no_mezclan_facturas_entre_sesiones()
    {
        var identityUserA = IdentityUser.Create("cajeroa", "Ana", "Perez", null, "hash", Guid.NewGuid());
        var identityUserB = IdentityUser.Create("cajerob", "Luis", "Gomez", null, "hash", Guid.NewGuid());
        var userA = identityUserA.Id;
        var userB = identityUserB.Id;
        var sessionA = OpenSession(BranchId, userA);
        var sessionB = OpenSession(BranchId, userB);
        var invoiceA = Guid.NewGuid();
        var invoiceB = Guid.NewGuid();
        sessionA.RecordMovement(CashMovementType.SaleIncome, 10m, "Venta A", userA, CashReferenceType.SalesInvoice, invoiceA, "001-001-000000004");
        sessionB.RecordMovement(CashMovementType.SaleIncome, 40m, "Venta B", userB, CashReferenceType.SalesInvoice, invoiceB, "001-001-000000005");

        var f = new Fixture(BranchId);
        f.SetupPaged([sessionA, sessionB], total: 2);
        f.InvoiceRepo
            .Setup(r => r.GetCollectionSummaryByCashSessionsAsync(TenantId, BranchId, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                Row(sessionA.Id, invoiceA, "001-001-000000004", 10m, f.Methods.Efectivo, 10m),
                Row(sessionB.Id, invoiceB, "001-001-000000005", 40m, f.Methods.Efectivo, 40m),
            });
        f.AccessRepo
            .Setup(r => r.GetUsersByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { identityUserA, identityUserB });

        var result = await RunAsync(f);

        result.Value!.Items.Should().HaveCount(2);
        var dtoA = result.Value.Items.Single(d => d.Id == sessionA.Id);
        var dtoB = result.Value.Items.Single(d => d.Id == sessionB.Id);
        dtoA.TotalInvoiced.Should().Be(10m);
        dtoB.TotalInvoiced.Should().Be(40m);
        dtoA.UserName.Should().Be("Ana Perez");
        dtoB.UserName.Should().Be("Luis Gomez");
    }

    [Fact]
    public async Task Scope_por_sucursal_consulta_el_resumen_de_facturas_con_la_sucursal_activa()
    {
        var userId = Guid.NewGuid();
        var session = OpenSession(BranchId, userId);
        var f = new Fixture(BranchId);
        f.SetupPaged([session]);

        await RunAsync(f);

        f.CashRepo.Verify(
            r => r.GetPagedAsync(TenantId, BranchId, null, 1, 25, It.IsAny<CancellationToken>()),
            Times.Once
        );
        f.InvoiceRepo.Verify(
            r => r.GetCollectionSummaryByCashSessionsAsync(
                TenantId, BranchId, It.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(session.Id)), It.IsAny<CancellationToken>()),
            Times.Once
        );
        f.InvoiceRepo.Verify(
            r => r.GetCollectionSummaryByCashSessionsAsync(
                It.IsAny<Guid>(), OtherBranchId, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Facturas_anuladas_no_suman_porque_el_repositorio_ya_las_excluye()
    {
        // GetCollectionSummaryByCashSessionsAsync ya filtra Status == Authorized (mismo contrato
        // que la versión singular) — el handler nunca ve filas de Draft/Cancelled.
        var userId = Guid.NewGuid();
        var session = OpenSession(BranchId, userId);
        var f = new Fixture(BranchId);
        f.SetupPaged([session]);
        // Fixture ya devuelve Array.Empty por defecto — simula que la única factura del turno
        // estaba anulada y el repo no la trae.

        var result = await RunAsync(f);

        var dto = result.Value!.Items.Single();
        dto.InvoiceCount.Should().Be(0);
        dto.TotalInvoiced.Should().Be(0m);
        dto.ByPaymentMethod.Should().BeEmpty();
    }
}
