using ERP.Application.Common;
using ERP.Application.Modules.Finance.UseCases.Payments;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Finance.Interfaces;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Payables.Interfaces;
using ERP.Domain.Modules.Purchases.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Finance;

/// <summary>
/// P0-02 Fase 3 — cobertura de orquestación de <see cref="ReversePaymentCommandHandler"/> tras el
/// endurecimiento con transacción explícita + Lock A por cada <c>PurchaseInvoiceId</c> distinto
/// (§15.1/§15.4 del diseño). Las reglas de negocio puras (balance, reversa) ya viven en
/// <c>PaymentTests</c>/<c>PurchasePayableTests</c> (Domain).
///
/// P0-02 Fase 3 (Remediación transaccional 02): el <c>Payment</c> se carga por su propio Id (dado
/// directamente por el comando) antes de la transacción — mismo criterio aceptado que la carga
/// inicial del agregado propio en <c>AuthorizeSalesReturnUseCases</c>, no señalado como defecto en
/// la auditoría. Lo que sí cambia: el <c>PurchaseInvoiceId</c> de cada <c>PurchasePayable</c>
/// afectado se descubre ANTES del lock mediante <c>IPurchasePayableRepository.GetOriginIdAsync</c>
/// (proyección SIN TRACKING), y cada <c>PurchasePayable</c> completo solo se recarga (vía
/// <c>GetByIdAsync</c>, tracking) DESPUÉS de adquirir todos los Lock A.
/// </summary>
public sealed class ReversePaymentCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid PurchaseId = Guid.NewGuid();

    private static AccountsPayable CreatePayable(decimal amount = 100m, Guid? purchaseId = null)
    {
        var payable = AccountsPayable.CreateFromOrigin(
            TenantId,
            CompanyId,
            Guid.NewGuid(),
            SupplierId,
            AccountsPayableOriginType.PurchaseInvoice,
            purchaseId ?? PurchaseId,
            "01",
            "001-001-000000001",
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 1),
            UserId
        );
        payable.AddInstallment(1, new DateOnly(2026, 7, 31), amount);
        return payable;
    }

    private static Payment CreateAppliedPayment(AccountsPayable payable, decimal amount)
    {
        var payment = Payment.Create(
            TenantId,
            CompanyId,
            PaymentDirection.Payment,
            SupplierId,
            amount,
            new DateOnly(2026, 7, 30),
            null,
            "REF-1",
            UserId
        );
        payment.AddApplicationLine(payable.Id, null, amount);
        payment.Apply(UserId);
        payable.RegisterPayment(amount, UserId);
        return payment;
    }

    private static (
        Mock<IPaymentRepository> payments,
        Mock<IAccountsPayableRepository> payables,
        Mock<IPurchaseReturnRepository> purchaseReturnRepo,
        Mock<IUnitOfWork> uow
    ) BuildMocks()
    {
        var payments = new Mock<IPaymentRepository>();
        var payables = new Mock<IAccountsPayableRepository>();
        var purchaseReturnRepo = new Mock<IPurchaseReturnRepository>();
        var uow = new Mock<IUnitOfWork>();
        return (payments, payables, purchaseReturnRepo, uow);
    }

    private static ReversePaymentCommandHandler BuildHandler(
        Mock<IPaymentRepository> payments,
        Mock<IAccountsPayableRepository> payables,
        Mock<IPurchaseReturnRepository> purchaseReturnRepo,
        Mock<IUnitOfWork> uow
    )
    {
        var tenant = new Mock<ICurrentTenant>();
        tenant.Setup(t => t.TenantId).Returns(TenantId);
        var company = new Mock<ICurrentCompany>();
        company.Setup(c => c.CompanyId).Returns(CompanyId);
        var user = new Mock<ICurrentUser>();
        user.Setup(u => u.UserId).Returns(UserId);

        return new ReversePaymentCommandHandler(
            payments.Object,
            payables.Object,
            purchaseReturnRepo.Object,
            uow.Object,
            tenant.Object,
            company.Object,
            user.Object
        );
    }

    private static void SetupPayable(
        Mock<IAccountsPayableRepository> payables,
        AccountsPayable payable
    )
    {
        payables
            .Setup(r =>
                r.GetOriginIdAsync(TenantId, payable.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(payable.OriginId);
        payables
            .Setup(r => r.GetByIdAsync(TenantId, payable.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payable);
    }

    [Fact]
    public async Task Reverso_valido_reversa_el_pago_y_decrementa_el_saldo_de_la_CxP()
    {
        var (payments, payables, purchaseReturnRepo, uow) = BuildMocks();
        var payable = CreatePayable(100m);
        var payment = CreateAppliedPayment(payable, 60m);

        payments
            .Setup(p =>
                p.GetByIdAsync(TenantId, CompanyId, payment.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(payment);
        SetupPayable(payables, payable);

        var handler = BuildHandler(payments, payables, purchaseReturnRepo, uow);
        var result = await handler.Handle(
            new ReversePaymentCommand(payment.Id, "Error de digitación"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        payable.PaidAmount.Should().Be(0m);
        payments.Verify(p => p.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Reverso_valido_adquiere_Lock_A_por_el_PurchaseInvoiceId_de_la_CxP_afectada()
    {
        var (payments, payables, purchaseReturnRepo, uow) = BuildMocks();
        var payable = CreatePayable(100m);
        var payment = CreateAppliedPayment(payable, 60m);

        payments
            .Setup(p =>
                p.GetByIdAsync(TenantId, CompanyId, payment.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(payment);
        SetupPayable(payables, payable);

        var handler = BuildHandler(payments, payables, purchaseReturnRepo, uow);
        await handler.Handle(
            new ReversePaymentCommand(payment.Id, "Error de digitación"),
            CancellationToken.None
        );

        uow.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        purchaseReturnRepo.Verify(
            r => r.AcquireFinancialLockAsync(TenantId, PurchaseId, It.IsAny<CancellationToken>()),
            Times.Once
        );
        uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        uow.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Reverso_sobre_pago_inexistente_retorna_NotFound_sin_abrir_transaccion()
    {
        var (payments, payables, purchaseReturnRepo, uow) = BuildMocks();
        var missingId = Guid.NewGuid();
        payments
            .Setup(p =>
                p.GetByIdAsync(TenantId, CompanyId, missingId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((Payment?)null);

        var handler = BuildHandler(payments, payables, purchaseReturnRepo, uow);
        var result = await handler.Handle(
            new ReversePaymentCommand(missingId, "Motivo"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        uow.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Reverso_de_un_cobro_AR_retorna_ValidationFailure()
    {
        var (payments, payables, purchaseReturnRepo, uow) = BuildMocks();
        var collection = Payment.Create(
            TenantId,
            CompanyId,
            PaymentDirection.Collection,
            Guid.NewGuid(),
            50m,
            new DateOnly(2026, 7, 30),
            null,
            null,
            UserId
        );
        payments
            .Setup(p =>
                p.GetByIdAsync(TenantId, CompanyId, collection.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(collection);

        var handler = BuildHandler(payments, payables, purchaseReturnRepo, uow);
        var result = await handler.Handle(
            new ReversePaymentCommand(collection.Id, "Motivo"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("no es un pago a proveedor");
    }

    [Fact]
    public async Task Reverso_ya_reversado_retorna_ValidationFailure_y_hace_rollback()
    {
        var (payments, payables, purchaseReturnRepo, uow) = BuildMocks();
        var payable = CreatePayable(100m);
        var payment = CreateAppliedPayment(payable, 60m);
        payment.Reverse(UserId, "Primera reversa");
        payable.ReversePayment(60m, UserId);

        payments
            .Setup(p =>
                p.GetByIdAsync(TenantId, CompanyId, payment.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(payment);
        SetupPayable(payables, payable);

        var handler = BuildHandler(payments, payables, purchaseReturnRepo, uow);
        var result = await handler.Handle(
            new ReversePaymentCommand(payment.Id, "Segunda reversa"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        uow.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Fase 3, Remediación transaccional 02 ───────────────────────────────

    /// <summary>
    /// Orden transaccional estricto: BeginTransaction → descubrimiento sin tracking → Lock A →
    /// recarga autoritativa (tracking) → mutación → SaveChanges → Commit.
    /// </summary>
    [Fact]
    public async Task Orden_transaccional_BeginTx_descubrimiento_LockA_recarga_mutacion_SaveChanges_Commit()
    {
        var (payments, payables, purchaseReturnRepo, uow) = BuildMocks();
        var payable = CreatePayable(100m);
        var payment = CreateAppliedPayment(payable, 60m);
        payments
            .Setup(p =>
                p.GetByIdAsync(TenantId, CompanyId, payment.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(payment);

        var sequence = new MockSequence();
        uow.InSequence(sequence)
            .Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        payables
            .InSequence(sequence)
            .Setup(r =>
                r.GetOriginIdAsync(TenantId, payable.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(payable.OriginId);
        purchaseReturnRepo
            .InSequence(sequence)
            .Setup(r =>
                r.AcquireFinancialLockAsync(
                    TenantId,
                    payable.OriginId,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask);
        payables
            .InSequence(sequence)
            .Setup(r => r.GetByIdAsync(TenantId, payable.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payable);
        payments
            .InSequence(sequence)
            .Setup(p => p.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        uow.InSequence(sequence)
            .Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = BuildHandler(payments, payables, purchaseReturnRepo, uow);
        var result = await handler.Handle(
            new ReversePaymentCommand(payment.Id, "Error de digitación"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// Un pago aplicado a CxP de facturas distintas: locks deduplicados y en orden ascendente
    /// determinista por texto de Guid (§15.4), nunca dependiente del orden de las líneas.
    /// </summary>
    [Fact]
    public async Task Multiples_CxP_de_facturas_distintas_adquieren_locks_deduplicados_en_orden_ascendente_antes_de_mutar()
    {
        var (payments, payables, purchaseReturnRepo, uow) = BuildMocks();

        var invoiceHigh = Guid.Parse("ffffffff-0000-0000-0000-000000000002");
        var invoiceLow = Guid.Parse("11111111-0000-0000-0000-000000000002");

        var payableA = CreatePayable(100m, invoiceHigh);
        var payableB = CreatePayable(100m, invoiceLow);

        var payment = Payment.Create(
            TenantId,
            CompanyId,
            PaymentDirection.Payment,
            SupplierId,
            90m,
            new DateOnly(2026, 7, 30),
            null,
            null,
            UserId
        );
        payment.AddApplicationLine(payableA.Id, null, 40m);
        payment.AddApplicationLine(payableB.Id, null, 50m);
        payment.Apply(UserId);
        payableA.RegisterPayment(40m, UserId);
        payableB.RegisterPayment(50m, UserId);

        payments
            .Setup(p =>
                p.GetByIdAsync(TenantId, CompanyId, payment.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(payment);
        SetupPayable(payables, payableA);
        SetupPayable(payables, payableB);

        var lockedOrder = new List<Guid>();
        purchaseReturnRepo
            .Setup(r =>
                r.AcquireFinancialLockAsync(
                    TenantId,
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<Guid, Guid, CancellationToken>(
                (_, invoiceId, _) => lockedOrder.Add(invoiceId)
            )
            .Returns(Task.CompletedTask);

        var mutatedBeforeAllLocksAcquired = false;
        payables
            .Setup(r => r.GetByIdAsync(TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns<Guid, Guid, CancellationToken>(
                (_, id, _) =>
                {
                    if (lockedOrder.Count < 2)
                        mutatedBeforeAllLocksAcquired = true;
                    var match = new[] { payableA, payableB }.First(p => p.Id == id);
                    return Task.FromResult<AccountsPayable?>(match);
                }
            );

        var handler = BuildHandler(payments, payables, purchaseReturnRepo, uow);
        var result = await handler.Handle(
            new ReversePaymentCommand(payment.Id, "Motivo"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        lockedOrder.Should().Equal(invoiceLow, invoiceHigh);
        mutatedBeforeAllLocksAcquired
            .Should()
            .BeFalse("ninguna recarga/mutación debe ocurrir antes de adquirir todos los locks");
    }

    /// <summary>
    /// Estado concurrente simulado: entre el descubrimiento (sin tracking) y la adquisición del
    /// lock, otra transacción ya revirtió/redujo el pago aplicado de la CxP. La recarga
    /// autoritativa posterior al lock devuelve ese estado, y el guard de dominio existente
    /// ("El monto a reversar excede el monto pagado registrado") rechaza sobre esa instancia.
    /// </summary>
    [Fact]
    public async Task Estado_ya_reversado_detectado_solo_en_la_recarga_post_lock_rechaza_y_hace_rollback()
    {
        var (payments, payables, purchaseReturnRepo, uow) = BuildMocks();
        var payable = CreatePayable(100m);
        var payment = CreateAppliedPayment(payable, 60m);

        payments
            .Setup(p =>
                p.GetByIdAsync(TenantId, CompanyId, payment.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(payment);
        payables
            .Setup(r =>
                r.GetOriginIdAsync(TenantId, payable.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(payable.OriginId);
        // La recarga posterior al lock refleja que, mientras tanto, otra transacción ya reversó
        // el monto aplicado — PaidAmount ya está en 0 antes de que este handler mute nada.
        payable.ReversePayment(60m, UserId);
        payables
            .Setup(r => r.GetByIdAsync(TenantId, payable.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payable);

        var handler = BuildHandler(payments, payables, purchaseReturnRepo, uow);
        var result = await handler.Handle(
            new ReversePaymentCommand(payment.Id, "Segunda reversa"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("excede el monto pagado");
        payments.Verify(p => p.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        uow.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
