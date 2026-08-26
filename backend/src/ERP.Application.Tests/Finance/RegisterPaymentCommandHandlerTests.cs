using ERP.Application.Common;
using ERP.Application.Modules.Finance.DTOs;
using ERP.Application.Modules.Finance.UseCases.Payments;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Finance.Events;
using ERP.Domain.Modules.Finance.Interfaces;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Finance;

/// <summary>
/// P0-03 (ERP_CORE_SUMAK_READINESS_AUDIT.md) — cobertura de orquestación de
/// <see cref="RegisterPaymentCommandHandler"/>, simétrica a
/// <c>RegisterCollectionCommandHandlerTests</c> (AR). Las reglas de negocio puras (balance,
/// límites, retención) ya viven en <c>PurchasePayableTests</c>/<c>PaymentTests</c> (Domain).
///
/// P0-02 Fase 3 (Remediación transaccional 02): el handler descubre el <c>PurchaseInvoiceId</c> de
/// cada <c>PurchasePayable</c> ANTES del lock mediante <c>IPurchasePayableRepository.GetPurchaseInvoiceIdAsync</c>
/// — una proyección SIN TRACKING (nunca rastrea la entidad) — y solo recarga cada
/// <c>PurchasePayable</c> completo (vía <c>GetByIdAsync</c>, tracking) DESPUÉS de adquirir todos los
/// Lock A. Por eso las pruebas de esta clase configuran ambos métodos por separado y, cuando
/// corresponde, verifican que las mutaciones y guards solo puedan provenir de la instancia
/// devuelta por <c>GetByIdAsync</c> (la única llamada posterior al lock).
/// </summary>
public sealed class RegisterPaymentCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid PurchaseId = Guid.NewGuid();

    private static PurchasePayable CreatePayable(decimal amount = 100m, Guid? purchaseId = null) =>
        PurchasePayable.Create(
            TenantId,
            CompanyId,
            purchaseId ?? PurchaseId,
            SupplierId,
            amount,
            UserId
        );

    private static (
        Mock<IPaymentRepository> payments,
        Mock<IPurchasePayableRepository> payables,
        Mock<IPurchaseReturnRepository> purchaseReturnRepo,
        Mock<ICompanyFinancialDestinationRepository> financialDestinations,
        Mock<IUnitOfWork> uow,
        Mock<ICurrentTenant> tenant,
        Mock<ICurrentCompany> company,
        Mock<ICurrentUser> user
    ) BuildMocks()
    {
        var payments = new Mock<IPaymentRepository>();
        var payables = new Mock<IPurchasePayableRepository>();
        var purchaseReturnRepo = new Mock<IPurchaseReturnRepository>();
        var financialDestinations = new Mock<ICompanyFinancialDestinationRepository>();
        var uow = new Mock<IUnitOfWork>();
        var tenant = new Mock<ICurrentTenant>();
        var company = new Mock<ICurrentCompany>();
        var user = new Mock<ICurrentUser>();

        tenant.Setup(t => t.TenantId).Returns(TenantId);
        company.Setup(c => c.CompanyId).Returns(CompanyId);
        user.Setup(u => u.UserId).Returns(UserId);

        return (payments, payables, purchaseReturnRepo, financialDestinations, uow, tenant, company, user);
    }

    private static RegisterPaymentCommandHandler BuildHandler(
        Mock<IPaymentRepository> payments,
        Mock<IPurchasePayableRepository> payables,
        Mock<IPurchaseReturnRepository> purchaseReturnRepo,
        Mock<ICompanyFinancialDestinationRepository> financialDestinations,
        Mock<IUnitOfWork> uow,
        Mock<ICurrentTenant> tenant,
        Mock<ICurrentCompany> company,
        Mock<ICurrentUser> user
    ) =>
        new(
            payments.Object,
            payables.Object,
            purchaseReturnRepo.Object,
            financialDestinations.Object,
            uow.Object,
            tenant.Object,
            company.Object,
            user.Object
        );

    /// <summary>Configura el par completo de mocks (descubrimiento sin tracking + recarga bajo lock) para un único payable.</summary>
    private static void SetupPayable(
        Mock<IPurchasePayableRepository> payables,
        PurchasePayable payable
    )
    {
        payables
            .Setup(r =>
                r.GetPurchaseInvoiceIdAsync(TenantId, payable.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(payable.PurchaseId);
        payables
            .Setup(r => r.GetByIdAsync(TenantId, payable.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payable);
    }

    [Fact]
    public async Task Pago_valido_aplica_el_pago_y_actualiza_el_saldo_de_la_CxP()
    {
        var (payments, payables, purchaseReturnRepo, financialDestinations, uow, tenant, company, user) = BuildMocks();
        var payable = CreatePayable(100m);
        SetupPayable(payables, payable);

        var handler = BuildHandler(
            payments,
            payables,
            purchaseReturnRepo,
            financialDestinations,
            uow,
            tenant,
            company,
            user
        );
        var cmd = new RegisterPaymentCommand(
            SupplierId,
            60m,
            new DateOnly(2026, 7, 30),
            null,
            "REF-1",
            new[] { new PaymentApplicationLineInput(payable.Id, null, 60m) }
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        payable.PaidAmount.Should().Be(60m);
        payable.BalanceDue.Should().Be(40m);
        payments.Verify(
            p =>
                p.AddAsync(
                    It.IsAny<Domain.Modules.Finance.Entities.Payment>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        payments.Verify(p => p.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Pago_valido_publica_SupplierPaymentAppliedEvent_en_el_Payment_persistido()
    {
        var (payments, payables, purchaseReturnRepo, financialDestinations, uow, tenant, company, user) = BuildMocks();
        var payable = CreatePayable(100m);
        SetupPayable(payables, payable);

        Domain.Modules.Finance.Entities.Payment? captured = null;
        payments
            .Setup(p =>
                p.AddAsync(
                    It.IsAny<Domain.Modules.Finance.Entities.Payment>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<Domain.Modules.Finance.Entities.Payment, CancellationToken>(
                (p, _) => captured = p
            )
            .Returns(Task.CompletedTask);

        var handler = BuildHandler(
            payments,
            payables,
            purchaseReturnRepo,
            financialDestinations,
            uow,
            tenant,
            company,
            user
        );
        var cmd = new RegisterPaymentCommand(
            SupplierId,
            100m,
            new DateOnly(2026, 7, 30),
            null,
            null,
            new[] { new PaymentApplicationLineInput(payable.Id, null, 100m) }
        );

        await handler.Handle(cmd, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.DomainEvents.Should().ContainSingle(e => e is SupplierPaymentAppliedEvent);
    }

    [Fact]
    public async Task Pago_con_InstallmentId_lo_propaga_a_la_linea_de_aplicacion_del_pago()
    {
        var (payments, payables, purchaseReturnRepo, financialDestinations, uow, tenant, company, user) = BuildMocks();
        var payable = CreatePayable(300m);
        var schedule = new List<Domain.Modules.Purchases.Entities.PurchasePaymentSchedule>
        {
            Domain.Modules.Purchases.Entities.PurchasePaymentSchedule.Create(
                PurchaseId,
                TenantId,
                1,
                new DateOnly(2026, 8, 30),
                150m
            ),
            Domain.Modules.Purchases.Entities.PurchasePaymentSchedule.Create(
                PurchaseId,
                TenantId,
                2,
                new DateOnly(2026, 9, 30),
                150m
            ),
        };
        payable.GenerateInstallments(schedule);
        var installmentId = payable.Installments[0].Id;
        SetupPayable(payables, payable);

        Domain.Modules.Finance.Entities.Payment? captured = null;
        payments
            .Setup(p =>
                p.AddAsync(
                    It.IsAny<Domain.Modules.Finance.Entities.Payment>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<Domain.Modules.Finance.Entities.Payment, CancellationToken>(
                (p, _) => captured = p
            )
            .Returns(Task.CompletedTask);

        var handler = BuildHandler(
            payments,
            payables,
            purchaseReturnRepo,
            financialDestinations,
            uow,
            tenant,
            company,
            user
        );
        var cmd = new RegisterPaymentCommand(
            SupplierId,
            150m,
            new DateOnly(2026, 7, 30),
            null,
            null,
            new[] { new PaymentApplicationLineInput(payable.Id, installmentId, 150m) }
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        captured!.Lines.Single().InstallmentId.Should().Be(installmentId);
    }

    [Fact]
    public async Task Pago_que_excede_el_saldo_pendiente_retorna_ValidationFailure_sin_lanzar()
    {
        var (payments, payables, purchaseReturnRepo, financialDestinations, uow, tenant, company, user) = BuildMocks();
        var payable = CreatePayable(100m);
        payable.RegisterPayment(70m, UserId); // saldo restante: 30
        SetupPayable(payables, payable);

        var handler = BuildHandler(
            payments,
            payables,
            purchaseReturnRepo,
            financialDestinations,
            uow,
            tenant,
            company,
            user
        );
        var cmd = new RegisterPaymentCommand(
            SupplierId,
            50m,
            new DateOnly(2026, 7, 30),
            null,
            null,
            new[] { new PaymentApplicationLineInput(payable.Id, null, 50m) }
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("excede el saldo pendiente");
        payable
            .PaidAmount.Should()
            .Be(70m, "el pago rechazado no debe mutar el saldo ya registrado");
        payments.Verify(p => p.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        uow.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Pago_sobre_CxP_inexistente_retorna_NotFound_sin_abrir_locks_ni_confirmar()
    {
        var (payments, payables, purchaseReturnRepo, financialDestinations, uow, tenant, company, user) = BuildMocks();
        var missingId = Guid.NewGuid();
        payables
            .Setup(r =>
                r.GetPurchaseInvoiceIdAsync(TenantId, missingId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((Guid?)null);

        var handler = BuildHandler(
            payments,
            payables,
            purchaseReturnRepo,
            financialDestinations,
            uow,
            tenant,
            company,
            user
        );
        var cmd = new RegisterPaymentCommand(
            SupplierId,
            50m,
            new DateOnly(2026, 7, 30),
            null,
            null,
            new[] { new PaymentApplicationLineInput(missingId, null, 50m) }
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        purchaseReturnRepo.Verify(
            r =>
                r.AcquireFinancialLockAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never,
            "el descubrimiento no encontró la CxP — no debe adquirirse ningún lock"
        );
        uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Pago_sobre_CxP_anulada_retorna_ValidationFailure()
    {
        var (payments, payables, purchaseReturnRepo, financialDestinations, uow, tenant, company, user) = BuildMocks();
        var payable = CreatePayable(100m);
        payable.CancelPayable();
        SetupPayable(payables, payable);

        var handler = BuildHandler(
            payments,
            payables,
            purchaseReturnRepo,
            financialDestinations,
            uow,
            tenant,
            company,
            user
        );
        var cmd = new RegisterPaymentCommand(
            SupplierId,
            10m,
            new DateOnly(2026, 7, 30),
            null,
            null,
            new[] { new PaymentApplicationLineInput(payable.Id, null, 10m) }
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("anulada");
        uow.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Fase 3, Remediación transaccional 02 ───────────────────────────────

    /// <summary>
    /// Orden transaccional estricto: BeginTransaction → descubrimiento sin tracking → Lock A →
    /// recarga autoritativa (tracking) → mutación → SaveChanges → Commit. Verificado con
    /// <see cref="MockSequence"/> — una prueba que solo cuente invocaciones (<c>Times.Once</c> por
    /// separado) no demuestra el orden relativo entre ellas.
    /// </summary>
    [Fact]
    public async Task Orden_transaccional_BeginTx_descubrimiento_LockA_recarga_mutacion_SaveChanges_Commit()
    {
        var (payments, payables, purchaseReturnRepo, financialDestinations, uow, tenant, company, user) = BuildMocks();
        var payable = CreatePayable(100m);

        var sequence = new MockSequence();
        uow.InSequence(sequence)
            .Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        payables
            .InSequence(sequence)
            .Setup(r =>
                r.GetPurchaseInvoiceIdAsync(TenantId, payable.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(payable.PurchaseId);
        purchaseReturnRepo
            .InSequence(sequence)
            .Setup(r =>
                r.AcquireFinancialLockAsync(
                    TenantId,
                    payable.PurchaseId,
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

        var handler = BuildHandler(
            payments,
            payables,
            purchaseReturnRepo,
            financialDestinations,
            uow,
            tenant,
            company,
            user
        );
        var cmd = new RegisterPaymentCommand(
            SupplierId,
            25m,
            new DateOnly(2026, 7, 30),
            null,
            null,
            new[] { new PaymentApplicationLineInput(payable.Id, null, 25m) }
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// Múltiples CxP de facturas distintas: locks deduplicados y en orden ascendente
    /// determinista por representación textual de Guid (§15.4) — nunca dependiente del orden de
    /// entrada. Se usan GUIDs deliberadamente desordenados para que la prueba solo pase si el
    /// handler realmente ordena.
    /// </summary>
    [Fact]
    public async Task Multiples_CxP_de_facturas_distintas_adquieren_locks_deduplicados_en_orden_ascendente_antes_de_mutar()
    {
        var (payments, payables, purchaseReturnRepo, financialDestinations, uow, tenant, company, user) = BuildMocks();

        // GUIDs deliberadamente NO ordenados como texto en el orden en que se declaran.
        var invoiceHigh = Guid.Parse("ffffffff-0000-0000-0000-000000000001");
        var invoiceLow = Guid.Parse("11111111-0000-0000-0000-000000000001");
        var invoiceMid = Guid.Parse("77777777-0000-0000-0000-000000000001");

        var payableA = CreatePayable(100m, invoiceHigh);
        var payableB = CreatePayable(100m, invoiceLow);
        // Dos líneas distintas de la MISMA factura (invoiceMid) — el lock de esa factura debe
        // adquirirse una sola vez (deduplicado), no dos.
        var payableC1 = CreatePayable(50m, invoiceMid);
        var payableC2 = CreatePayable(50m, invoiceMid);

        SetupPayable(payables, payableA);
        SetupPayable(payables, payableB);
        SetupPayable(payables, payableC1);
        SetupPayable(payables, payableC2);

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
                    if (lockedOrder.Count < 3)
                        mutatedBeforeAllLocksAcquired = true;
                    var match = new[] { payableA, payableB, payableC1, payableC2 }.First(p =>
                        p.Id == id
                    );
                    return Task.FromResult<PurchasePayable?>(match);
                }
            );

        var handler = BuildHandler(
            payments,
            payables,
            purchaseReturnRepo,
            financialDestinations,
            uow,
            tenant,
            company,
            user
        );
        var cmd = new RegisterPaymentCommand(
            SupplierId,
            120m,
            new DateOnly(2026, 7, 30),
            null,
            null,
            new[]
            {
                new PaymentApplicationLineInput(payableA.Id, null, 20m),
                new PaymentApplicationLineInput(payableB.Id, null, 30m),
                new PaymentApplicationLineInput(payableC1.Id, null, 40m),
                new PaymentApplicationLineInput(payableC2.Id, null, 30m),
            }
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        lockedOrder.Should().Equal(invoiceLow, invoiceMid, invoiceHigh);
        mutatedBeforeAllLocksAcquired
            .Should()
            .BeFalse("ninguna recarga/mutación debe ocurrir antes de adquirir todos los locks");
    }

    /// <summary>
    /// Estado concurrente simulado: el descubrimiento (sin tracking) solo confirma que la CxP
    /// existe; entre ese momento y la adquisición del lock, otra transacción la anuló. La recarga
    /// autoritativa posterior al lock devuelve el estado YA anulado, y el guard de dominio existente
    /// ("No se puede registrar un pago sobre una cuenta por pagar anulada") debe rechazar sobre esa
    /// instancia — nunca sobre un estado optimista previo al lock.
    /// </summary>
    [Fact]
    public async Task Estado_anulado_detectado_solo_en_la_recarga_post_lock_rechaza_y_hace_rollback()
    {
        var (payments, payables, purchaseReturnRepo, financialDestinations, uow, tenant, company, user) = BuildMocks();
        var payable = CreatePayable(100m);

        payables
            .Setup(r =>
                r.GetPurchaseInvoiceIdAsync(TenantId, payable.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(payable.PurchaseId);
        // La recarga posterior al lock refleja que, mientras tanto, otra transacción canceló la CxP.
        payable.CancelPayable();
        payables
            .Setup(r => r.GetByIdAsync(TenantId, payable.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payable);

        var handler = BuildHandler(
            payments,
            payables,
            purchaseReturnRepo,
            financialDestinations,
            uow,
            tenant,
            company,
            user
        );
        var cmd = new RegisterPaymentCommand(
            SupplierId,
            10m,
            new DateOnly(2026, 7, 30),
            null,
            null,
            new[] { new PaymentApplicationLineInput(payable.Id, null, 10m) }
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("anulada");
        payments.Verify(p => p.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        uow.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
