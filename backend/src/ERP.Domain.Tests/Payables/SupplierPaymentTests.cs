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
        var methods = new[] { new SupplierPaymentMethodLineInput(Guid.NewGuid(), Guid.NewGuid(), 300m) };
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
    /// (FinancialDestinationId, Amount) por cada medio de pago, para que
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
            new SupplierPaymentMethodLineInput(Guid.NewGuid(), destinationA, 100m),
            new SupplierPaymentMethodLineInput(Guid.NewGuid(), destinationB, 200m),
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
                    new SupplierPaymentConfirmedMethodLine(destinationA, 100m),
                    new SupplierPaymentConfirmedMethodLine(destinationB, 200m),
                }
            );
    }

    [Fact]
    public void Create_valido_2_medios_1_cuota()
    {
        var methods = new[]
        {
            new SupplierPaymentMethodLineInput(Guid.NewGuid(), Guid.NewGuid(), 100m),
            new SupplierPaymentMethodLineInput(Guid.NewGuid(), Guid.NewGuid(), 200m),
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
        var methods = new[] { new SupplierPaymentMethodLineInput(Guid.NewGuid(), Guid.NewGuid(), 300m) };
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
            new SupplierPaymentMethodLineInput(Guid.NewGuid(), Guid.NewGuid(), 150m),
            new SupplierPaymentMethodLineInput(Guid.NewGuid(), Guid.NewGuid(), 150m),
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
        var methods = new[] { new SupplierPaymentMethodLineInput(Guid.NewGuid(), Guid.NewGuid(), 300m) };
        var applications = new[] { new SupplierPaymentApplicationLineInput(Guid.NewGuid(), 250m) };
        var allocations = new[] { new SupplierPaymentAllocationInput(0, 0, 250m) };

        var act = () => CreatePayment(300m, methods, applications, allocations);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Bloquea_si_suma_allocations_no_coincide_con_total()
    {
        var methods = new[] { new SupplierPaymentMethodLineInput(Guid.NewGuid(), Guid.NewGuid(), 300m) };
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
            new SupplierPaymentMethodLineInput(Guid.NewGuid(), Guid.NewGuid(), 100m),
            new SupplierPaymentMethodLineInput(Guid.NewGuid(), Guid.NewGuid(), 200m),
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
        var methods = new[] { new SupplierPaymentMethodLineInput(Guid.NewGuid(), Guid.NewGuid(), 300m) };
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
        var methods = new[] { new SupplierPaymentMethodLineInput(Guid.NewGuid(), Guid.NewGuid(), amount) };
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
        var methods = new[] { new SupplierPaymentMethodLineInput(Guid.NewGuid(), Guid.NewGuid(), 300m) };
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
        var methods = new[] { new SupplierPaymentMethodLineInput(Guid.NewGuid(), Guid.NewGuid(), 300m) };
        var applications = new[] { new SupplierPaymentApplicationLineInput(Guid.NewGuid(), 300m) };
        var allocations = new[] { new SupplierPaymentAllocationInput(0, 0, 300m) };

        var act = () => CreatePayment(amount, methods, applications, allocations);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void DisplayNumber_usa_receipt_number_cuando_existe()
    {
        var methods = new[] { new SupplierPaymentMethodLineInput(Guid.NewGuid(), Guid.NewGuid(), 300m) };
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
        var methods = new[] { new SupplierPaymentMethodLineInput(Guid.NewGuid(), Guid.NewGuid(), 300m) };
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
}
