using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Payables.Events;
using FluentAssertions;

namespace ERP.Domain.Tests.Payables;

/// <summary>
/// SUPPLIER-PAYMENTS-FOUNDATION-15B — pruebas puras de dominio de <see cref="SupplierPayment.Create"/>:
/// cobertura del invariante completo (medios, aplicaciones, matriz de distribución, balance) y de la
/// regla "sin Draft visible" (SUPPLIER-PAYMENTS-AUDIT-15A) — <c>Create</c> siempre devuelve un pago
/// ya <see cref="SupplierPaymentStatus.Confirmed"/> o lanza, nunca un estado intermedio.
/// </summary>
public sealed class SupplierPaymentTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid CreatedBy = Guid.NewGuid();
    private static readonly DateOnly PaymentDate = new(2026, 8, 28);

    private static SupplierPayment CreatePayment(
        decimal totalAmount,
        IReadOnlyList<SupplierPaymentMethodLineInput> methods,
        IReadOnlyList<SupplierPaymentApplicationLineInput> applications,
        IReadOnlyList<SupplierPaymentAllocationInput> allocations,
        string systemNumber = "00000001",
        string? receiptNumber = null
    ) =>
        SupplierPayment.Create(
            TenantId,
            CompanyId,
            BranchId,
            SupplierId,
            PaymentDate,
            totalAmount,
            systemNumber,
            receiptNumber,
            methods,
            applications,
            allocations,
            CreatedBy
        );

    [Fact]
    public void Create_valido_1_medio_1_aplicacion_1_allocation()
    {
        var methods = new[] { new SupplierPaymentMethodLineInput(Guid.NewGuid(), Guid.NewGuid(), null, 300m, TransactionDate: PaymentDate) };
        var applications = new[] { new SupplierPaymentApplicationLineInput(Guid.NewGuid(), 300m) };
        var allocations = new[] { new SupplierPaymentAllocationInput(0, 0, 300m) };

        var payment = CreatePayment(300m, methods, applications, allocations);

        payment.Status.Should().Be(SupplierPaymentStatus.Confirmed);
        payment.MethodLines.Should().HaveCount(1);
        payment.ApplicationLines.Should().HaveCount(1);
        payment.AllocationLines.Should().HaveCount(1);
        payment.DomainEvents.Should().ContainSingle(e => e is SupplierPaymentConfirmedEvent);
    }

    /// <summary>
    /// SUPPLIER-PAYMENTS-POSTING-15D — el evento debe transportar un snapshot
    /// (CompanyBankAccountId, Amount) por cada medio de pago, para que
    /// <c>SupplierPaymentConfirmedPostingTranslator</c> pueda generar un crédito por medio sin
    /// recargar el agregado completo.
    /// </summary>
    [Fact]
    public void Create_publica_evento_con_un_snapshot_de_medio_por_cada_SupplierPaymentMethodLine()
    {
        var destinationA = Guid.NewGuid();
        var destinationB = Guid.NewGuid();
        var methods = new[]
        {
            new SupplierPaymentMethodLineInput(Guid.NewGuid(), destinationA, null, 100m, TransactionDate: PaymentDate),
            new SupplierPaymentMethodLineInput(Guid.NewGuid(), destinationB, null, 200m, TransactionDate: PaymentDate),
        };
        var applications = new[] { new SupplierPaymentApplicationLineInput(Guid.NewGuid(), 300m) };
        var allocations = new[]
        {
            new SupplierPaymentAllocationInput(0, 0, 100m),
            new SupplierPaymentAllocationInput(1, 0, 200m),
        };

        var payment = CreatePayment(300m, methods, applications, allocations);

        var evt = payment.DomainEvents.OfType<SupplierPaymentConfirmedEvent>().Single();
        evt.TotalAmount.Should().Be(300m);
        evt.MethodLines.Should()
            .BeEquivalentTo(
                new[]
                {
                    new SupplierPaymentConfirmedMethodLine(destinationA, null, 100m),
                    new SupplierPaymentConfirmedMethodLine(destinationB, null, 200m),
                }
            );
    }

    [Fact]
    public void Create_valido_2_medios_1_cuota()
    {
        var methods = new[]
        {
            new SupplierPaymentMethodLineInput(Guid.NewGuid(), Guid.NewGuid(), null, 100m, TransactionDate: PaymentDate),
            new SupplierPaymentMethodLineInput(Guid.NewGuid(), Guid.NewGuid(), null, 200m, TransactionDate: PaymentDate),
        };
        var applications = new[] { new SupplierPaymentApplicationLineInput(Guid.NewGuid(), 300m) };
        var allocations = new[]
        {
            new SupplierPaymentAllocationInput(0, 0, 100m),
            new SupplierPaymentAllocationInput(1, 0, 200m),
        };

        var payment = CreatePayment(300m, methods, applications, allocations);

        payment.Status.Should().Be(SupplierPaymentStatus.Confirmed);
        payment.MethodLines.Should().HaveCount(2);
        payment.ApplicationLines.Should().HaveCount(1);
        payment.AllocationLines.Should().HaveCount(2);
    }

    [Fact]
    public void Create_valido_1_medio_2_cuotas()
    {
        var methods = new[] { new SupplierPaymentMethodLineInput(Guid.NewGuid(), Guid.NewGuid(), null, 300m, TransactionDate: PaymentDate) };
        var applications = new[]
        {
            new SupplierPaymentApplicationLineInput(Guid.NewGuid(), 100m),
            new SupplierPaymentApplicationLineInput(Guid.NewGuid(), 200m),
        };
        var allocations = new[]
        {
            new SupplierPaymentAllocationInput(0, 0, 100m),
            new SupplierPaymentAllocationInput(0, 1, 200m),
        };

        var payment = CreatePayment(300m, methods, applications, allocations);

        payment.Status.Should().Be(SupplierPaymentStatus.Confirmed);
        payment.MethodLines.Should().HaveCount(1);
        payment.ApplicationLines.Should().HaveCount(2);
        payment.AllocationLines.Should().HaveCount(2);
    }

    [Fact]
    public void Create_valido_2_medios_2_cuotas_matriz_cruzada()
    {
        var methods = new[]
        {
            new SupplierPaymentMethodLineInput(Guid.NewGuid(), Guid.NewGuid(), null, 150m, TransactionDate: PaymentDate),
            new SupplierPaymentMethodLineInput(Guid.NewGuid(), Guid.NewGuid(), null, 150m, TransactionDate: PaymentDate),
        };
        var applications = new[]
        {
            new SupplierPaymentApplicationLineInput(Guid.NewGuid(), 150m),
            new SupplierPaymentApplicationLineInput(Guid.NewGuid(), 150m),
        };
        var allocations = new[]
        {
            new SupplierPaymentAllocationInput(0, 0, 100m),
            new SupplierPaymentAllocationInput(0, 1, 50m),
            new SupplierPaymentAllocationInput(1, 0, 50m),
            new SupplierPaymentAllocationInput(1, 1, 100m),
        };

        var payment = CreatePayment(300m, methods, applications, allocations);

        payment.Status.Should().Be(SupplierPaymentStatus.Confirmed);
        payment.AllocationLines.Should().HaveCount(4);
    }

    [Fact]
    public void Bloquea_si_suma_medios_no_coincide_con_suma_aplicaciones()
    {
        var methods = new[] { new SupplierPaymentMethodLineInput(Guid.NewGuid(), Guid.NewGuid(), null, 300m, TransactionDate: PaymentDate) };
        var applications = new[] { new SupplierPaymentApplicationLineInput(Guid.NewGuid(), 250m) };
        var allocations = new[] { new SupplierPaymentAllocationInput(0, 0, 250m) };

        var act = () => CreatePayment(300m, methods, applications, allocations);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Bloquea_si_suma_allocations_no_coincide_con_total()
    {
        var methods = new[] { new SupplierPaymentMethodLineInput(Guid.NewGuid(), Guid.NewGuid(), null, 300m, TransactionDate: PaymentDate) };
        var applications = new[] { new SupplierPaymentApplicationLineInput(Guid.NewGuid(), 300m) };
        var allocations = new[] { new SupplierPaymentAllocationInput(0, 0, 250m) };

        var act = () => CreatePayment(300m, methods, applications, allocations);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Bloquea_si_un_medio_no_esta_distribuido_al_100_por_ciento()
    {
        var methods = new[]
        {
            new SupplierPaymentMethodLineInput(Guid.NewGuid(), Guid.NewGuid(), null, 100m, TransactionDate: PaymentDate),
            new SupplierPaymentMethodLineInput(Guid.NewGuid(), Guid.NewGuid(), null, 200m, TransactionDate: PaymentDate),
        };
        var applications = new[] { new SupplierPaymentApplicationLineInput(Guid.NewGuid(), 300m) };
        // El medio 1 (200) solo se distribuye 150 — el otro medio compensa el total pero deja
        // ese medio puntual sin cubrir al 100%.
        var allocations = new[]
        {
            new SupplierPaymentAllocationInput(0, 0, 150m),
            new SupplierPaymentAllocationInput(1, 0, 150m),
        };

        var act = () => CreatePayment(300m, methods, applications, allocations);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Bloquea_si_una_aplicacion_no_esta_cubierta_al_100_por_ciento()
    {
        var methods = new[] { new SupplierPaymentMethodLineInput(Guid.NewGuid(), Guid.NewGuid(), null, 300m, TransactionDate: PaymentDate) };
        var applications = new[]
        {
            new SupplierPaymentApplicationLineInput(Guid.NewGuid(), 100m),
            new SupplierPaymentApplicationLineInput(Guid.NewGuid(), 200m),
        };
        // La aplicación 1 (200) solo recibe 150 — el resto del medio se desvía a la aplicación 0,
        // que queda sobre-cubierta y por tanto desbalanceada igual.
        var allocations = new[]
        {
            new SupplierPaymentAllocationInput(0, 0, 150m),
            new SupplierPaymentAllocationInput(0, 1, 150m),
        };

        var act = () => CreatePayment(300m, methods, applications, allocations);

        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Bloquea_monto_de_medio_menor_o_igual_a_cero(decimal amount)
    {
        var methods = new[] { new SupplierPaymentMethodLineInput(Guid.NewGuid(), Guid.NewGuid(), null, amount, TransactionDate: PaymentDate) };
        var applications = new[] { new SupplierPaymentApplicationLineInput(Guid.NewGuid(), 300m) };
        var allocations = new[] { new SupplierPaymentAllocationInput(0, 0, 300m) };

        var act = () => CreatePayment(300m, methods, applications, allocations);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Bloquea_monto_de_aplicacion_menor_o_igual_a_cero(decimal amount)
    {
        var methods = new[] { new SupplierPaymentMethodLineInput(Guid.NewGuid(), Guid.NewGuid(), null, 300m, TransactionDate: PaymentDate) };
        var applications = new[] { new SupplierPaymentApplicationLineInput(Guid.NewGuid(), amount) };
        var allocations = new[] { new SupplierPaymentAllocationInput(0, 0, 300m) };

        var act = () => CreatePayment(300m, methods, applications, allocations);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Bloquea_monto_total_menor_o_igual_a_cero(decimal amount)
    {
        var methods = new[] { new SupplierPaymentMethodLineInput(Guid.NewGuid(), Guid.NewGuid(), null, 300m, TransactionDate: PaymentDate) };
        var applications = new[] { new SupplierPaymentApplicationLineInput(Guid.NewGuid(), 300m) };
        var allocations = new[] { new SupplierPaymentAllocationInput(0, 0, 300m) };

        var act = () => CreatePayment(amount, methods, applications, allocations);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void DisplayNumber_usa_receipt_number_cuando_existe()
    {
        var methods = new[] { new SupplierPaymentMethodLineInput(Guid.NewGuid(), Guid.NewGuid(), null, 300m, TransactionDate: PaymentDate) };
        var applications = new[] { new SupplierPaymentApplicationLineInput(Guid.NewGuid(), 300m) };
        var allocations = new[] { new SupplierPaymentAllocationInput(0, 0, 300m) };

        var payment = CreatePayment(
            300m,
            methods,
            applications,
            allocations,
            systemNumber: "00000042",
            receiptNumber: "CHK-9911"
        );

        payment.DisplayNumber.Should().Be("CHK-9911");
    }

    [Fact]
    public void DisplayNumber_usa_system_number_cuando_no_hay_receipt_number()
    {
        var methods = new[] { new SupplierPaymentMethodLineInput(Guid.NewGuid(), Guid.NewGuid(), null, 300m, TransactionDate: PaymentDate) };
        var applications = new[] { new SupplierPaymentApplicationLineInput(Guid.NewGuid(), 300m) };
        var allocations = new[] { new SupplierPaymentAllocationInput(0, 0, 300m) };

        var payment = CreatePayment(300m, methods, applications, allocations, systemNumber: "00000042");

        payment.DisplayNumber.Should().Be("00000042");
    }

    [Fact]
    public void Create_no_depende_de_Payment_ni_de_PaymentApplicationLine()
    {
        // SUPPLIER-PAYMENTS-AUDIT-15A: SupplierPayment es un agregado independiente — nunca debe
        // referenciar los tipos de Finance.Payment (que sostienen Collections/CxC en vivo).
        var forbiddenTypeNames = new[] { "Payment", "PaymentApplicationLine" };

        var referencedTypes = typeof(SupplierPayment)
            .GetMethods()
            .SelectMany(m => m.GetParameters().Select(p => p.ParameterType))
            .Concat(typeof(SupplierPayment).GetProperties().Select(p => p.PropertyType))
            .Select(t => t.IsGenericType ? t.GetGenericArguments().FirstOrDefault() ?? t : t)
            .Where(t => t is not null)
            .Select(t => t!.Name)
            .ToHashSet();

        referencedTypes.Should().NotContain(forbiddenTypeNames);
    }

    [Fact]
    public void No_existe_RegisterPaymentCommand_ni_SupplierPaymentAppliedPostingTranslator_en_Domain()
    {
        var domainAssembly = typeof(SupplierPayment).Assembly;
        var forbiddenNames = new[]
        {
            "RegisterPaymentCommand",
            "ReversePaymentCommand",
            "SupplierPaymentAppliedPostingTranslator",
        };

        var offending = domainAssembly
            .GetTypes()
            .Where(t => forbiddenNames.Contains(t.Name))
            .Select(t => t.FullName)
            .ToList();

        offending.Should().BeEmpty();
    }

    // ══════════════════════════════════════════════════════════════════════
    // SUPPLIER-PAYMENTS-REVERSE-16 — SupplierPayment.Reverse
    // ══════════════════════════════════════════════════════════════════════

    private static SupplierPayment CreateSimplePayment(decimal amount = 300m)
    {
        var methods = new[] { new SupplierPaymentMethodLineInput(Guid.NewGuid(), Guid.NewGuid(), null, amount, TransactionDate: PaymentDate) };
        var applications = new[] { new SupplierPaymentApplicationLineInput(Guid.NewGuid(), amount) };
        var allocations = new[] { new SupplierPaymentAllocationInput(0, 0, amount) };
        return CreatePayment(amount, methods, applications, allocations);
    }

    [Fact]
    public void Reverse_pago_Confirmed_cambia_estado_a_Reversed_y_guarda_ReversedAt_ReversedBy_Reason()
    {
        var payment = CreateSimplePayment();
        var reversedBy = Guid.NewGuid();
        var reversedAt = DateTime.UtcNow;

        payment.Reverse("Error de digitación", reversedBy, reversedAt, bankReversalReason: SupplierPaymentBankReversalReason.NotExecuted);

        payment.Status.Should().Be(SupplierPaymentStatus.Reversed);
        payment.ReversedAtUtc.Should().Be(reversedAt);
        payment.ReversedBy.Should().Be(reversedBy);
        payment.ReverseReason.Should().Be("Error de digitación");
    }

    [Fact]
    public void Reverse_publica_SupplierPaymentReversedEvent_con_snapshot_de_medios_y_aplicaciones()
    {
        var destinationId = Guid.NewGuid();
        var installmentId = Guid.NewGuid();
        var methods = new[] { new SupplierPaymentMethodLineInput(Guid.NewGuid(), destinationId, null, 300m, TransactionDate: PaymentDate) };
        var applications = new[] { new SupplierPaymentApplicationLineInput(installmentId, 300m) };
        var allocations = new[] { new SupplierPaymentAllocationInput(0, 0, 300m) };
        var payment = CreatePayment(300m, methods, applications, allocations);

        payment.Reverse("Duplicado", Guid.NewGuid(), DateTime.UtcNow, bankReversalReason: SupplierPaymentBankReversalReason.NotExecuted);

        var evt = payment.DomainEvents.OfType<SupplierPaymentReversedEvent>().Single();
        evt.SupplierPaymentId.Should().Be(payment.Id);
        evt.TotalAmount.Should().Be(300m);
        evt.ReverseReason.Should().Be("Duplicado");
        evt.MethodLines.Should()
            .BeEquivalentTo(new[] { new SupplierPaymentConfirmedMethodLine(destinationId, null, 300m) });
        evt.ApplicationLines.Should()
            .BeEquivalentTo(new[] { new SupplierPaymentReversedApplicationLine(installmentId, 300m) });
    }

    [Fact]
    public void Bloquea_doble_reversa()
    {
        var payment = CreateSimplePayment();
        payment.Reverse("Primer motivo", Guid.NewGuid(), DateTime.UtcNow, bankReversalReason: SupplierPaymentBankReversalReason.NotExecuted);

        var act = () => payment.Reverse("Segundo intento", Guid.NewGuid(), DateTime.UtcNow, bankReversalReason: SupplierPaymentBankReversalReason.NotExecuted);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Bloquea_reversa_sin_motivo()
    {
        var payment = CreateSimplePayment();

        var act = () => payment.Reverse("   ", Guid.NewGuid(), DateTime.UtcNow, bankReversalReason: SupplierPaymentBankReversalReason.NotExecuted);

        act.Should().Throw<ArgumentException>();
        payment.Status.Should().Be(SupplierPaymentStatus.Confirmed, "un intento inválido no debe mutar el estado");
    }

    // ══════════════════════════════════════════════════════════════════════
    // ZH-SUPPLIER-PAYMENT-CASH-TRANSFER-HARDENING-02A
    // ══════════════════════════════════════════════════════════════════════

    private static SupplierPayment CreateSingle(SupplierPaymentMethodLineInput method) =>
        CreatePayment(
            method.Amount,
            new[] { method },
            new[] { new SupplierPaymentApplicationLineInput(Guid.NewGuid(), method.Amount) },
            new[] { new SupplierPaymentAllocationInput(0, 0, method.Amount) }
        );

    [Fact]
    public void Fuente_bancaria_conserva_su_fecha_real_distinta_de_PaymentDate()
    {
        var bankDate = PaymentDate.AddDays(-2);

        var payment = CreateSingle(
            new SupplierPaymentMethodLineInput(Guid.NewGuid(), Guid.NewGuid(), null, 50m, "OP-1", TransactionDate: bankDate)
        );

        payment.MethodLines[0].TransactionDate.Should().Be(bankDate);
    }

    [Fact]
    public void Fuente_bancaria_sin_fecha_es_invalida_nunca_se_completa_con_PaymentDate()
    {
        var act = () =>
            CreateSingle(new SupplierPaymentMethodLineInput(Guid.NewGuid(), Guid.NewGuid(), null, 50m, TransactionDate: null));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Fuente_de_caja_con_fecha_bancaria_es_invalida()
    {
        var act = () =>
            CreateSingle(
                new SupplierPaymentMethodLineInput(Guid.NewGuid(), null, Guid.NewGuid(), 50m, TransactionDate: PaymentDate)
            );

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void LinkCashMovement_vincula_sesion_y_movimiento_una_sola_vez()
    {
        var payment = CreateSingle(new SupplierPaymentMethodLineInput(Guid.NewGuid(), null, Guid.NewGuid(), 50m));
        var line = payment.MethodLines[0];
        var sessionId = Guid.NewGuid();
        var movementId = Guid.NewGuid();

        payment.LinkCashMovement(line.Id, sessionId, movementId);

        line.CashSessionId.Should().Be(sessionId);
        line.CashMovementId.Should().Be(movementId);
        var again = () => payment.LinkCashMovement(line.Id, Guid.NewGuid(), Guid.NewGuid());
        again.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void LinkCashMovement_rechaza_fuente_bancaria()
    {
        var payment = CreateSingle(new SupplierPaymentMethodLineInput(Guid.NewGuid(), Guid.NewGuid(), null, 50m, TransactionDate: PaymentDate));

        var act = () => payment.LinkCashMovement(payment.MethodLines[0].Id, Guid.NewGuid(), Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>();
    }

    // ══════════════════════════════════════════════════════════════════════
    // ZH-SUPPLIER-PAYMENT-REVERSAL-SEMANTICS-02B-FINAL
    // ══════════════════════════════════════════════════════════════════════

    private static SupplierPayment CreateLinkedCashPayment(decimal amount)
    {
        var payment = CreateSingle(new SupplierPaymentMethodLineInput(Guid.NewGuid(), null, Guid.NewGuid(), amount));
        payment.LinkCashMovement(payment.MethodLines[0].Id, Guid.NewGuid(), Guid.NewGuid());
        return payment;
    }

    [Fact]
    public void Reversa_de_fuente_de_caja_exige_confirmacion_de_efectivo_no_entregado()
    {
        var payment = CreateLinkedCashPayment(50m);

        var act = () => payment.Reverse("Duplicado", Guid.NewGuid(), DateTime.UtcNow, cashNotDeliveredConfirmed: false);

        act.Should().Throw<InvalidOperationException>();
        payment.Status.Should().Be(SupplierPaymentStatus.Confirmed);
    }

    [Fact]
    public void Reversa_de_fuente_de_caja_sin_trazabilidad_de_sesion_se_rechaza()
    {
        var payment = CreateSingle(new SupplierPaymentMethodLineInput(Guid.NewGuid(), null, Guid.NewGuid(), 50m));

        var act = () => payment.Reverse("Duplicado", Guid.NewGuid(), DateTime.UtcNow, cashNotDeliveredConfirmed: true);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Reversa_de_fuente_bancaria_exige_motivo_estructurado_y_lo_registra()
    {
        var payment = CreateSingle(new SupplierPaymentMethodLineInput(Guid.NewGuid(), Guid.NewGuid(), null, 50m, "OP-1", TransactionDate: PaymentDate));

        var withoutReason = () => payment.Reverse("Error", Guid.NewGuid(), DateTime.UtcNow);
        withoutReason.Should().Throw<InvalidOperationException>();

        payment.Reverse("Error", Guid.NewGuid(), DateTime.UtcNow, bankReversalReason: SupplierPaymentBankReversalReason.RejectedByBank);
        payment.ReversalBankReason.Should().Be(SupplierPaymentBankReversalReason.RejectedByBank);
        payment.ReversalCashNotDeliveredConfirmed.Should().BeNull();
    }

    [Fact]
    public void Reversa_de_fuente_de_caja_confirmada_registra_la_afirmacion()
    {
        var payment = CreateLinkedCashPayment(50m);

        payment.Reverse("Duplicado", Guid.NewGuid(), DateTime.UtcNow, cashNotDeliveredConfirmed: true);

        payment.Status.Should().Be(SupplierPaymentStatus.Reversed);
        payment.ReversalCashNotDeliveredConfirmed.Should().BeTrue();
        payment.ReversalBankReason.Should().BeNull();
    }
}
