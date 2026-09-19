using ERP.Application.Common;
using ERP.Application.Modules.Caja.UseCases;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Finance.Interfaces;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Domain.Modules.Sales.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Caja;

/// <summary>
/// CASH-SESSION-COLLECTION-SUMMARY-01/UX-02 — <see cref="GetCashSessionCollectionSummaryHandler"/>
/// arma el resumen de ventas/cobros del turno leyendo SalesInvoice+SalesInvoicePayment (vía
/// <see cref="ISalesInvoiceRepository.GetCollectionSummaryByCashSessionAsync"/>), el catálogo de
/// PaymentMethod, y — solo si hubo Transferencia — bancos/cuentas bancarias. Nunca toca
/// CashSession/CashMovement (efectivo físico) ni recalcula CashApplied/PhysicalCashApplied.
/// </summary>
public sealed class CashSessionCollectionSummaryHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid OtherBranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static CashSession OpenSession(Guid branchId) =>
        CashSession.Open(
            TenantId,
            CompanyId,
            branchId,
            UserId,
            Guid.NewGuid(),
            "CAJA-01",
            "Caja Principal",
            Guid.NewGuid(),
            "001",
            0m,
            UserId
        );

    /// <summary>Catálogo de las 5 formas de pago típicas — Ids reales generados por PaymentMethod.Create,
    /// nunca constantes hardcodeadas, para usar exactamente el mismo Id al armar filas de prueba.</summary>
    public sealed class Methods
    {
        public PaymentMethod Efectivo { get; } =
            PaymentMethod.Create(TenantId, "EFECTIVO", "Efectivo", false, false, 1, UserId, affectsPhysicalCash: true);
        public PaymentMethod Transferencia { get; } =
            PaymentMethod.Create(
                TenantId,
                "TRANSFERENCIA",
                "Transferencia Bancaria",
                true,
                false,
                2,
                UserId,
                detailType: PaymentMethodDetailType.Transfer
            );
        public PaymentMethod Tarjeta { get; } =
            PaymentMethod.Create(
                TenantId,
                "TARJETA",
                "Tarjeta de Crédito",
                true,
                false,
                3,
                UserId,
                detailType: PaymentMethodDetailType.Card
            );
        public PaymentMethod Cheque { get; } =
            PaymentMethod.Create(
                TenantId,
                "CHEQUE",
                "Cheque",
                true,
                false,
                4,
                UserId,
                detailType: PaymentMethodDetailType.Check
            );
        public PaymentMethod Credito { get; } =
            PaymentMethod.Create(TenantId, "CREDITO", "Crédito", false, true, 5, UserId);

        public IReadOnlyList<PaymentMethod> All => [Efectivo, Transferencia, Tarjeta, Cheque, Credito];
    }

    private sealed class Fixture
    {
        public Mock<ICashSessionRepository> CashRepo { get; } = new();
        public Mock<ISalesInvoiceRepository> InvoiceRepo { get; } = new();
        public Mock<IPaymentMethodRepository> PaymentMethodRepo { get; } = new();
        public Mock<ICompanyBankAccountRepository> BankAccountRepo { get; } = new();
        public Mock<IBankRepository> BankRepo { get; } = new();
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
            BankAccountRepo
                .Setup(r => r.GetListAsync(TenantId, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<CompanyBankAccount>());
            BankRepo
                .Setup(r => r.ListAsync(TenantId, false, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<Bank>());
        }

        public GetCashSessionCollectionSummaryHandler BuildHandler() =>
            new(
                CashRepo.Object,
                InvoiceRepo.Object,
                PaymentMethodRepo.Object,
                BankAccountRepo.Object,
                BankRepo.Object,
                Tenant.Object,
                Branch.Object
            );
    }

    private static SalesInvoiceCashSessionPaymentRow Row(
        Guid invoiceId,
        string invoiceNumber,
        decimal grandTotal,
        PaymentMethod method,
        decimal amount,
        DateTime? authorizedAt = null,
        string customerName = "Cliente Test",
        string? reference = null,
        Guid? transferCompanyBankAccountId = null,
        string? transferLegacyBankName = null,
        string? transferReceiptNumber = null,
        DateOnly? transferDate = null,
        Guid? cashSessionId = null
    ) =>
        new(
            cashSessionId ?? Guid.NewGuid(),
            invoiceId,
            invoiceNumber,
            authorizedAt ?? new DateTime(2026, 9, 18, 10, 0, 0, DateTimeKind.Utc),
            customerName,
            grandTotal,
            method.Id,
            method.Code,
            method.Name,
            amount,
            reference,
            transferCompanyBankAccountId,
            transferLegacyBankName,
            transferReceiptNumber,
            transferDate
        );

    [Fact]
    public async Task Sesion_de_otra_sucursal_devuelve_NotFound()
    {
        var session = OpenSession(OtherBranchId);
        var f = new Fixture(activeBranchId: BranchId);
        f.CashRepo
            .Setup(r => r.GetByIdAsync(TenantId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var result = await f.BuildHandler()
            .Handle(new GetCashSessionCollectionSummaryQuery(session.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        f.InvoiceRepo.Verify(
            r => r.GetCollectionSummaryByCashSessionAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Sin_ventas_devuelve_resumen_en_cero()
    {
        var session = OpenSession(BranchId);
        var f = new Fixture(activeBranchId: BranchId);
        f.CashRepo
            .Setup(r => r.GetByIdAsync(TenantId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        f.InvoiceRepo
            .Setup(r => r.GetCollectionSummaryByCashSessionAsync(TenantId, BranchId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SalesInvoiceCashSessionPaymentRow>());

        var result = await f.BuildHandler()
            .Handle(new GetCashSessionCollectionSummaryQuery(session.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.InvoiceCount.Should().Be(0);
        result.Value.TotalInvoiced.Should().Be(0m);
        result.Value.TotalCollected.Should().Be(0m);
        result.Value.TotalCredit.Should().Be(0m);
        result.Value.ByPaymentMethod.Should().BeEmpty();
    }

    public static IEnumerable<object[]> NonCreditMethodSelectors()
    {
        yield return new object[] { (Func<Methods, PaymentMethod>)(m => m.Efectivo) };
        yield return new object[] { (Func<Methods, PaymentMethod>)(m => m.Transferencia) };
        yield return new object[] { (Func<Methods, PaymentMethod>)(m => m.Tarjeta) };
        yield return new object[] { (Func<Methods, PaymentMethod>)(m => m.Cheque) };
    }

    // ── "forma simple" ────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(NonCreditMethodSelectors))]
    public async Task Venta_100_por_ciento_no_credito_suma_a_TotalCollected_y_no_a_TotalCredit(
        Func<Methods, PaymentMethod> select
    )
    {
        var session = OpenSession(BranchId);
        var invoiceId = Guid.NewGuid();
        var f = new Fixture(activeBranchId: BranchId);
        var method = select(f.Methods);
        f.CashRepo
            .Setup(r => r.GetByIdAsync(TenantId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        f.InvoiceRepo
            .Setup(r => r.GetCollectionSummaryByCashSessionAsync(TenantId, BranchId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Row(invoiceId, "001-001-000000001", 50m, method, 50m) });

        var result = await f.BuildHandler()
            .Handle(new GetCashSessionCollectionSummaryQuery(session.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.InvoiceCount.Should().Be(1);
        result.Value.TotalInvoiced.Should().Be(50m);
        result.Value.TotalCollected.Should().Be(50m);
        result.Value.TotalCredit.Should().Be(0m);
        var row = result.Value.ByPaymentMethod.Single(m => m.PaymentMethodId == method.Id);
        row.Amount.Should().Be(50m);
        row.InvoiceCount.Should().Be(1);
        row.OperationCount.Should().Be(1);
        row.IsCreditAllowed.Should().BeFalse();
        row.Details.Should().ContainSingle(d => d.InvoiceTotal == 50m && d.Amount == 50m && !d.IsMixedPayment);
    }

    [Fact]
    public async Task Venta_100_por_ciento_credito_suma_a_TotalCredit_y_no_a_TotalCollected()
    {
        var session = OpenSession(BranchId);
        var invoiceId = Guid.NewGuid();
        var f = new Fixture(activeBranchId: BranchId);
        f.CashRepo
            .Setup(r => r.GetByIdAsync(TenantId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        f.InvoiceRepo
            .Setup(r => r.GetCollectionSummaryByCashSessionAsync(TenantId, BranchId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Row(invoiceId, "001-001-000000002", 30m, f.Methods.Credito, 30m) });

        var result = await f.BuildHandler()
            .Handle(new GetCashSessionCollectionSummaryQuery(session.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.TotalCollected.Should().Be(0m);
        result.Value.TotalCredit.Should().Be(30m);
        var row = result.Value.ByPaymentMethod.Single(m => m.PaymentMethodId == f.Methods.Credito.Id);
        row.IsCreditAllowed.Should().BeTrue();
        row.Destination.Should().Be("Cuentas por Cobrar");
    }

    // ── "código+nombre" ──────────────────────────────────────────────────

    [Fact]
    public async Task Expone_Code_y_Name_del_metodo_para_la_columna_forma_codigo()
    {
        var session = OpenSession(BranchId);
        var invoiceId = Guid.NewGuid();
        var f = new Fixture(activeBranchId: BranchId);
        f.CashRepo
            .Setup(r => r.GetByIdAsync(TenantId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        f.InvoiceRepo
            .Setup(r => r.GetCollectionSummaryByCashSessionAsync(TenantId, BranchId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Row(invoiceId, "001-001-000000009", 12m, f.Methods.Efectivo, 12m) });

        var result = await f.BuildHandler()
            .Handle(new GetCashSessionCollectionSummaryQuery(session.Id), CancellationToken.None);

        var row = result.Value!.ByPaymentMethod.Single();
        row.PaymentMethodCode.Should().Be("EFECTIVO");
        row.PaymentMethodName.Should().Be("Efectivo");
        row.Destination.Should().Be("Caja física");
    }

    /// <summary>AUDIT-CASH-SESSION-COLLECTION-SUMMARY-MISMATCH-01 — un PaymentMethodId huérfano
    /// (ya no existe en el catálogo) no debe romper el resumen: cae en IsCreditAllowed=false,
    /// Destination="Sin clasificar", y usa el Code/Name del snapshot del pago, no del catálogo.</summary>
    [Fact]
    public async Task PaymentMethodId_huerfano_no_falla_y_usa_el_snapshot_del_pago()
    {
        var session = OpenSession(BranchId);
        var invoiceId = Guid.NewGuid();
        var orphanMethodId = Guid.NewGuid();
        var f = new Fixture(activeBranchId: BranchId);
        f.CashRepo
            .Setup(r => r.GetByIdAsync(TenantId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        f.InvoiceRepo
            .Setup(r => r.GetCollectionSummaryByCashSessionAsync(TenantId, BranchId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new SalesInvoiceCashSessionPaymentRow(
                    session.Id,
                    invoiceId,
                    "999-999-orphan",
                    new DateTime(2026, 9, 13, 18, 14, 0, DateTimeKind.Utc),
                    "Cliente Test",
                    2.00m,
                    orphanMethodId,
                    "01",
                    "Efectivo",
                    2.00m,
                    null,
                    null,
                    null,
                    null,
                    null
                ),
            });

        var result = await f.BuildHandler()
            .Handle(new GetCashSessionCollectionSummaryQuery(session.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        var row = result.Value!.ByPaymentMethod.Single();
        row.PaymentMethodId.Should().Be(orphanMethodId);
        row.PaymentMethodCode.Should().Be("01");
        row.IsCreditAllowed.Should().BeFalse();
        row.Destination.Should().Be("Sin clasificar");
        result.Value.TotalCollected.Should().Be(2.00m);
    }

    // ── "venta mixta" ────────────────────────────────────────────────────

    [Fact]
    public async Task Venta_mixta_Efectivo_mas_Transferencia_reparte_el_monto_por_forma_y_cuenta_una_factura()
    {
        // $2.59 = Efectivo $1.59 + Transferencia $1.00 — mismo ejemplo del ticket.
        var session = OpenSession(BranchId);
        var invoiceId = Guid.NewGuid();
        var f = new Fixture(activeBranchId: BranchId);
        f.CashRepo
            .Setup(r => r.GetByIdAsync(TenantId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        f.InvoiceRepo
            .Setup(r => r.GetCollectionSummaryByCashSessionAsync(TenantId, BranchId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                Row(invoiceId, "001-001-000000003", 2.59m, f.Methods.Efectivo, 1.59m),
                Row(invoiceId, "001-001-000000003", 2.59m, f.Methods.Transferencia, 1.00m),
            });

        var result = await f.BuildHandler()
            .Handle(new GetCashSessionCollectionSummaryQuery(session.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.InvoiceCount.Should().Be(1, "la factura mixta cuenta una sola vez en el total de facturas");
        result.Value.TotalInvoiced.Should().Be(2.59m, "el total facturado no debe duplicarse por tener 2 formas de pago");
        result.Value.TotalCollected.Should().Be(2.59m);

        var efectivo = result.Value.ByPaymentMethod.Single(m => m.PaymentMethodId == f.Methods.Efectivo.Id);
        efectivo.Amount.Should().Be(1.59m);
        efectivo.InvoiceCount.Should().Be(1);
        var efectivoDetail = efectivo.Details.Single();
        efectivoDetail.InvoiceTotal.Should().Be(2.59m, "el total factura debe verse completo aunque el monto de esta forma sea parcial");
        efectivoDetail.Amount.Should().Be(1.59m);
        efectivoDetail.IsMixedPayment.Should().BeTrue();

        var transferencia = result.Value.ByPaymentMethod.Single(m => m.PaymentMethodId == f.Methods.Transferencia.Id);
        transferencia.Amount.Should().Be(1.00m);
        transferencia.InvoiceCount.Should().Be(1);
        transferencia.Details.Single().IsMixedPayment.Should().BeTrue();
    }

    // ── "varias operaciones misma forma" ────────────────────────────────

    [Fact]
    public async Task Factura_con_varios_pagos_de_la_misma_forma_cuenta_una_factura_pero_dos_operaciones()
    {
        // Simula 2 pagos separados con el mismo PaymentMethodId dentro de la misma factura
        // (p.ej. dos abonos en efectivo) — 1 factura distinta, pero 2 operaciones (líneas de pago).
        var session = OpenSession(BranchId);
        var invoiceId = Guid.NewGuid();
        var f = new Fixture(activeBranchId: BranchId);
        f.CashRepo
            .Setup(r => r.GetByIdAsync(TenantId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        f.InvoiceRepo
            .Setup(r => r.GetCollectionSummaryByCashSessionAsync(TenantId, BranchId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                Row(invoiceId, "001-001-000000004", 10m, f.Methods.Efectivo, 6m),
                Row(invoiceId, "001-001-000000004", 10m, f.Methods.Efectivo, 4m),
            });

        var result = await f.BuildHandler()
            .Handle(new GetCashSessionCollectionSummaryQuery(session.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.InvoiceCount.Should().Be(1);
        result.Value.TotalInvoiced.Should().Be(10m);
        var efectivo = result.Value.ByPaymentMethod.Single(m => m.PaymentMethodId == f.Methods.Efectivo.Id);
        efectivo.InvoiceCount.Should().Be(1);
        efectivo.OperationCount.Should().Be(2, "2 líneas de pago separadas son 2 operaciones aunque sea 1 sola factura");
        efectivo.Amount.Should().Be(10m);
        efectivo.Details.Should().HaveCount(2);
        efectivo.Details.Should().OnlyContain(d => d.IsMixedPayment, "2 pagos en la misma factura ya es una factura mixta a nivel de pagos");
    }

    [Fact]
    public async Task Factura_anulada_no_aparece_porque_el_repositorio_ya_la_excluye()
    {
        // El repositorio (GetCollectionSummaryByCashSessionAsync) filtra Status == Authorized —
        // el handler nunca recibe filas de facturas Draft/Cancelled. Este test documenta el
        // contrato: si el repo no devuelve la fila, el handler no la suma.
        var session = OpenSession(BranchId);
        var f = new Fixture(activeBranchId: BranchId);
        f.CashRepo
            .Setup(r => r.GetByIdAsync(TenantId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        f.InvoiceRepo
            .Setup(r => r.GetCollectionSummaryByCashSessionAsync(TenantId, BranchId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SalesInvoiceCashSessionPaymentRow>());

        var result = await f.BuildHandler()
            .Handle(new GetCashSessionCollectionSummaryQuery(session.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.InvoiceCount.Should().Be(0);
        result.Value.TotalInvoiced.Should().Be(0m);
    }

    [Fact]
    public async Task Query_se_ejecuta_con_tenant_y_branch_de_la_sesion_no_valores_arbitrarios()
    {
        var session = OpenSession(BranchId);
        var f = new Fixture(activeBranchId: BranchId);
        f.CashRepo
            .Setup(r => r.GetByIdAsync(TenantId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        f.InvoiceRepo
            .Setup(r => r.GetCollectionSummaryByCashSessionAsync(TenantId, BranchId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SalesInvoiceCashSessionPaymentRow>());

        await f.BuildHandler()
            .Handle(new GetCashSessionCollectionSummaryQuery(session.Id), CancellationToken.None);

        f.InvoiceRepo.Verify(
            r => r.GetCollectionSummaryByCashSessionAsync(TenantId, BranchId, session.Id, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Totales_generales_cuadran_con_la_suma_de_los_desgloses_por_forma()
    {
        var session = OpenSession(BranchId);
        var inv1 = Guid.NewGuid();
        var inv2 = Guid.NewGuid();
        var f = new Fixture(activeBranchId: BranchId);
        f.CashRepo
            .Setup(r => r.GetByIdAsync(TenantId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        f.InvoiceRepo
            .Setup(r => r.GetCollectionSummaryByCashSessionAsync(TenantId, BranchId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                Row(inv1, "001-001-000000005", 2.59m, f.Methods.Efectivo, 1.59m),
                Row(inv1, "001-001-000000005", 2.59m, f.Methods.Transferencia, 1.00m),
                Row(inv2, "001-001-000000006", 20m, f.Methods.Credito, 20m),
            });

        var result = await f.BuildHandler()
            .Handle(new GetCashSessionCollectionSummaryQuery(session.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.InvoiceCount.Should().Be(2);
        result.Value.TotalInvoiced.Should().Be(22.59m);
        result.Value.TotalCollected.Should().Be(2.59m);
        result.Value.TotalCredit.Should().Be(20m);
        result.Value.ByPaymentMethod.Sum(m => m.Amount).Should().Be(result.Value.TotalCollected + result.Value.TotalCredit);
    }

    // ── "orden fecha DESC" ───────────────────────────────────────────────

    [Fact]
    public async Task Detalle_de_una_forma_esta_ordenado_por_fecha_hora_descendente()
    {
        var session = OpenSession(BranchId);
        var f = new Fixture(activeBranchId: BranchId);
        f.CashRepo
            .Setup(r => r.GetByIdAsync(TenantId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        var older = new DateTime(2026, 9, 18, 8, 0, 0, DateTimeKind.Utc);
        var newer = new DateTime(2026, 9, 18, 15, 30, 0, DateTimeKind.Utc);
        var middle = new DateTime(2026, 9, 18, 10, 0, 0, DateTimeKind.Utc);
        f.InvoiceRepo
            .Setup(r => r.GetCollectionSummaryByCashSessionAsync(TenantId, BranchId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                Row(Guid.NewGuid(), "001-001-000000001", 1m, f.Methods.Efectivo, 1m, authorizedAt: older),
                Row(Guid.NewGuid(), "001-001-000000002", 1m, f.Methods.Efectivo, 1m, authorizedAt: newer),
                Row(Guid.NewGuid(), "001-001-000000003", 1m, f.Methods.Efectivo, 1m, authorizedAt: middle),
            });

        var result = await f.BuildHandler()
            .Handle(new GetCashSessionCollectionSummaryQuery(session.Id), CancellationToken.None);

        var details = result.Value!.ByPaymentMethod.Single().Details;
        details.Select(d => d.AuthorizedAt).Should().BeInDescendingOrder();
        details[0].InvoiceNumber.Should().Be("001-001-000000002");
        details[^1].InvoiceNumber.Should().Be("001-001-000000001");
    }

    // ── Orden de formas: Efectivo, Transferencia, Tarjeta, Cheque, Crédito; otros después ──

    [Fact]
    public async Task ByPaymentMethod_respeta_el_orden_fijo_de_UX_no_el_SortOrder_del_catalogo()
    {
        var session = OpenSession(BranchId);
        var f = new Fixture(activeBranchId: BranchId);
        f.CashRepo
            .Setup(r => r.GetByIdAsync(TenantId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        // Se insertan en orden inverso a propósito — el resultado debe reordenarlos igual.
        f.InvoiceRepo
            .Setup(r => r.GetCollectionSummaryByCashSessionAsync(TenantId, BranchId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                Row(Guid.NewGuid(), "001-001-000000001", 1m, f.Methods.Credito, 1m),
                Row(Guid.NewGuid(), "001-001-000000002", 1m, f.Methods.Cheque, 1m),
                Row(Guid.NewGuid(), "001-001-000000003", 1m, f.Methods.Tarjeta, 1m),
                Row(Guid.NewGuid(), "001-001-000000004", 1m, f.Methods.Transferencia, 1m),
                Row(Guid.NewGuid(), "001-001-000000005", 1m, f.Methods.Efectivo, 1m),
            });

        var result = await f.BuildHandler()
            .Handle(new GetCashSessionCollectionSummaryQuery(session.Id), CancellationToken.None);

        result.Value!.ByPaymentMethod.Select(m => m.PaymentMethodName).Should().Equal(
            "Efectivo",
            "Transferencia Bancaria",
            "Tarjeta de Crédito",
            "Cheque",
            "Crédito"
        );
    }

    // ── "transferencia con cuenta/comprobante" ──────────────────────────

    [Fact]
    public async Task Transferencia_con_cuenta_bancaria_resuelve_banco_y_cuenta_enmascarada()
    {
        var session = OpenSession(BranchId);
        var invoiceId = Guid.NewGuid();
        var f = new Fixture(activeBranchId: BranchId);
        f.CashRepo
            .Setup(r => r.GetByIdAsync(TenantId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var bank = Bank.Create(TenantId, "PICHINCHA", "Banco Pichincha", null, UserId);
        var bankAccount = CompanyBankAccount.Create(
            TenantId,
            CompanyId,
            bank.Id,
            BankAccountType.Checking,
            "2200123456",
            "Cuenta corriente Pichincha",
            Guid.NewGuid(),
            UserId
        );
        f.BankAccountRepo
            .Setup(r => r.GetListAsync(TenantId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { bankAccount });
        f.BankRepo
            .Setup(r => r.ListAsync(TenantId, false, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { bank });

        f.InvoiceRepo
            .Setup(r => r.GetCollectionSummaryByCashSessionAsync(TenantId, BranchId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                Row(
                    invoiceId,
                    "001-001-000000007",
                    5.00m,
                    f.Methods.Transferencia,
                    5.00m,
                    transferCompanyBankAccountId: bankAccount.Id,
                    transferReceiptNumber: "TRX-000123",
                    transferDate: new DateOnly(2026, 9, 18)
                ),
            });

        var result = await f.BuildHandler()
            .Handle(new GetCashSessionCollectionSummaryQuery(session.Id), CancellationToken.None);

        var detail = result.Value!.ByPaymentMethod.Single().Details.Single();
        detail.DestinationBankName.Should().Be("Banco Pichincha");
        detail.DestinationAccountMasked.Should().Be("Cuenta corriente Pichincha (****3456)");
        detail.TransferReceiptNumber.Should().Be("TRX-000123");
        detail.TransferDate.Should().Be(new DateOnly(2026, 9, 18));
        detail.Reference.Should().Be("TRX-000123", "para Transferencia el comprobante real reemplaza la referencia genérica");
    }

    [Fact]
    public async Task Transferencia_legacy_sin_cuenta_bancaria_usa_el_texto_libre_historico()
    {
        var session = OpenSession(BranchId);
        var invoiceId = Guid.NewGuid();
        var f = new Fixture(activeBranchId: BranchId);
        f.CashRepo
            .Setup(r => r.GetByIdAsync(TenantId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        f.InvoiceRepo
            .Setup(r => r.GetCollectionSummaryByCashSessionAsync(TenantId, BranchId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                Row(
                    invoiceId,
                    "001-001-000000008",
                    3.00m,
                    f.Methods.Transferencia,
                    3.00m,
                    transferLegacyBankName: "Banco Guayaquil (texto libre histórico)",
                    transferReceiptNumber: "OLD-001"
                ),
            });

        var result = await f.BuildHandler()
            .Handle(new GetCashSessionCollectionSummaryQuery(session.Id), CancellationToken.None);

        var detail = result.Value!.ByPaymentMethod.Single().Details.Single();
        detail.DestinationBankName.Should().Be("Banco Guayaquil (texto libre histórico)");
        detail.DestinationAccountMasked.Should().BeNull("sin CompanyBankAccountId no hay número de cuenta real que enmascarar");
    }

    // ── "cero impacto sobre saldo físico" ───────────────────────────────

    [Fact]
    public async Task No_lee_ni_escribe_movimientos_de_CashSession_efectivo_fisico_intacto()
    {
        var session = OpenSession(BranchId);
        var f = new Fixture(activeBranchId: BranchId);
        f.CashRepo
            .Setup(r => r.GetByIdAsync(TenantId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        f.InvoiceRepo
            .Setup(r => r.GetCollectionSummaryByCashSessionAsync(TenantId, BranchId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                Row(Guid.NewGuid(), "001-001-000000001", 30m, f.Methods.Transferencia, 30m),
            });
        var balanceBefore = session.CurrentBalance;
        var movementsBefore = session.Movements.Count;

        await f.BuildHandler()
            .Handle(new GetCashSessionCollectionSummaryQuery(session.Id), CancellationToken.None);

        session.CurrentBalance.Should().Be(balanceBefore);
        session.Movements.Should().HaveCount(movementsBefore);
        f.CashRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        f.CashRepo.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Once,
            "el handler solo lee la sesión una vez para validar tenant+branch, nunca la muta"
        );
    }
}
