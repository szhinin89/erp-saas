using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Accounting.Posting.Translators;
using ERP.Application.Modules.Payables.Exceptions;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Finance.Interfaces;
using ERP.Domain.Modules.Payables.Events;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Accounting;

/// <summary>
/// SUPPLIER-PAYMENTS-POSTING-15D — mismo criterio que
/// <see cref="ExpenseDocumentConfirmedPostingTranslatorTests"/>: un posting fallido debe LANZAR
/// (nunca solo loguear un warning), para que <c>ErpDbContext.SaveChangesAsync</c> revierta todo el
/// registro del pago (ver <see cref="SupplierPaymentPostingFailedException"/>). A diferencia de
/// Gastos, aquí las cuentas de crédito no vienen ya resueltas en el evento — se resuelven leyendo
/// <c>CompanyFinancialDestination.AccountingAccountId</c> por cada medio de pago, mismo patrón que
/// <c>CollectionAppliedPostingTranslator</c> pero generalizado a N destinos.
/// </summary>
public sealed class SupplierPaymentConfirmedPostingTranslatorTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private sealed class Mocks
    {
        public Mock<IPostingEngine> PostingEngine { get; } = new();
        public Mock<ICompanyFinancialDestinationRepository> FinancialDestinations { get; } = new();

        public SupplierPaymentConfirmedPostingTranslator BuildTranslator() =>
            new(PostingEngine.Object, FinancialDestinations.Object);
    }

    private static CompanyFinancialDestination Destination(Guid? companyId = null, bool isActive = true)
    {
        var destination = CompanyFinancialDestination.Create(
            TenantId,
            companyId ?? CompanyId,
            $"CAJA-{Guid.NewGuid():N}"[..10],
            "Caja Principal",
            FinancialDestinationTypeCode.CashRegister,
            Guid.NewGuid(),
            "USD",
            UserId,
            cashRegisterId: Guid.NewGuid()
        );
        if (!isActive)
            destination.SetActive(false, UserId);
        return destination;
    }

    private static SupplierPaymentConfirmedEvent Event(
        IReadOnlyList<SupplierPaymentConfirmedMethodLine> methodLines,
        decimal totalAmount,
        Guid? supplierPaymentId = null
    ) =>
        new(
            TenantId,
            supplierPaymentId ?? Guid.NewGuid(),
            CompanyId,
            SupplierId,
            totalAmount,
            new DateOnly(2026, 8, 28),
            methodLines
        );

    private void SetupSuccess(Mocks m) =>
        m.PostingEngine
            .Setup(e => e.PostAsync(It.IsAny<PostingFact>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<PostingOutcomeDto>.Success(
                    new PostingOutcomeDto(Guid.NewGuid(), PostingOutcomeStatus.Created)
                )
            );

    [Fact]
    public async Task Pago_con_1_medio_genera_1_credito_y_debita_CxP_por_el_total()
    {
        var m = new Mocks();
        SetupSuccess(m);
        var destination = Destination();
        m.FinancialDestinations
            .Setup(f => f.GetByIdAsync(TenantId, destination.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(destination);

        PostingFact? captured = null;
        m.PostingEngine
            .Setup(e => e.PostAsync(It.IsAny<PostingFact>(), It.IsAny<CancellationToken>()))
            .Callback<PostingFact, CancellationToken>((fact, _) => captured = fact)
            .ReturnsAsync(
                Result<PostingOutcomeDto>.Success(new PostingOutcomeDto(Guid.NewGuid(), PostingOutcomeStatus.Created))
            );

        var supplierPaymentId = Guid.NewGuid();
        var evt = Event(
            new[] { new SupplierPaymentConfirmedMethodLine(destination.Id, 300m) },
            300m,
            supplierPaymentId
        );

        await m.BuildTranslator().Handle(evt, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.TenantId.Should().Be(TenantId);
        captured.CompanyId.Should().Be(CompanyId);
        captured.SourceModule.Should().Be("Payables");
        captured.FactType.Should().Be("SupplierPaymentConfirmed");
        captured.SourceEventId.Should().Be(supplierPaymentId);
        captured.GrandTotal.Should().Be(300m, "el Debe de CxP se resuelve vía PostingRule con GrandTotal");
        captured.Allocations.Should().ContainSingle();
        var allocation = captured.Allocations!.Single();
        allocation.AccountingAccountId.Should().Be(destination.AccountingAccountId);
        allocation.Amount.Should().Be(300m);
        allocation.Nature.Should().Be(AccountNature.Credit);
    }

    [Fact]
    public async Task Pago_con_2_medios_genera_2_creditos()
    {
        var m = new Mocks();
        var destinationA = Destination();
        var destinationB = Destination();
        m.FinancialDestinations
            .Setup(f => f.GetByIdAsync(TenantId, destinationA.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(destinationA);
        m.FinancialDestinations
            .Setup(f => f.GetByIdAsync(TenantId, destinationB.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(destinationB);

        PostingFact? captured = null;
        m.PostingEngine
            .Setup(e => e.PostAsync(It.IsAny<PostingFact>(), It.IsAny<CancellationToken>()))
            .Callback<PostingFact, CancellationToken>((fact, _) => captured = fact)
            .ReturnsAsync(
                Result<PostingOutcomeDto>.Success(new PostingOutcomeDto(Guid.NewGuid(), PostingOutcomeStatus.Created))
            );

        var evt = Event(
            new[]
            {
                new SupplierPaymentConfirmedMethodLine(destinationA.Id, 100m),
                new SupplierPaymentConfirmedMethodLine(destinationB.Id, 200m),
            },
            300m
        );

        await m.BuildTranslator().Handle(evt, CancellationToken.None);

        captured!.Allocations.Should().HaveCount(2);
        captured.Allocations!.Should().OnlyContain(a => a.Nature == AccountNature.Credit);
        captured.Allocations!.Sum(a => a.Amount).Should().Be(300m, "Debe (GrandTotal) debe balancear con la suma de créditos");
    }

    [Fact]
    public async Task Pago_con_3_medios_genera_3_creditos()
    {
        var m = new Mocks();
        var destinations = new[] { Destination(), Destination(), Destination() };
        foreach (var d in destinations)
            m.FinancialDestinations
                .Setup(f => f.GetByIdAsync(TenantId, d.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(d);

        PostingFact? captured = null;
        m.PostingEngine
            .Setup(e => e.PostAsync(It.IsAny<PostingFact>(), It.IsAny<CancellationToken>()))
            .Callback<PostingFact, CancellationToken>((fact, _) => captured = fact)
            .ReturnsAsync(
                Result<PostingOutcomeDto>.Success(new PostingOutcomeDto(Guid.NewGuid(), PostingOutcomeStatus.Created))
            );

        var evt = Event(
            destinations
                .Select((d, i) => new SupplierPaymentConfirmedMethodLine(d.Id, 100m * (i + 1)))
                .ToList(),
            600m
        );

        await m.BuildTranslator().Handle(evt, CancellationToken.None);

        captured!.Allocations.Should().HaveCount(3);
    }

    [Fact]
    public async Task Evento_con_2_medios_postea_por_medio_sin_referenciar_cuotas()
    {
        // SupplierPaymentConfirmedEvent no transporta información de cuotas/aplicaciones —
        // solo medios de pago. Esto por construcción garantiza que el posting nunca puede
        // "postear por cuota": no hay forma de que lo haga con los datos que recibe.
        var m = new Mocks();
        var destinationA = Destination();
        var destinationB = Destination();
        m.FinancialDestinations
            .Setup(f => f.GetByIdAsync(TenantId, destinationA.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(destinationA);
        m.FinancialDestinations
            .Setup(f => f.GetByIdAsync(TenantId, destinationB.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(destinationB);
        SetupSuccess(m);

        // El mismo pago total (300) pudo haberse aplicado a 1 o a 2 cuotas — el evento no lo dice,
        // y el posting resultante (2 créditos, uno por medio) es idéntico en ambos casos.
        var evt = Event(
            new[]
            {
                new SupplierPaymentConfirmedMethodLine(destinationA.Id, 150m),
                new SupplierPaymentConfirmedMethodLine(destinationB.Id, 150m),
            },
            300m
        );

        var act = async () => await m.BuildTranslator().Handle(evt, CancellationToken.None);

        await act.Should().NotThrowAsync();
        m.PostingEngine.Verify(
            e =>
                e.PostAsync(
                    It.Is<PostingFact>(f => f.Allocations!.Count == 2),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task Posting_exitoso_no_lanza()
    {
        var m = new Mocks();
        var destination = Destination();
        m.FinancialDestinations
            .Setup(f => f.GetByIdAsync(TenantId, destination.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(destination);
        SetupSuccess(m);

        var evt = Event(new[] { new SupplierPaymentConfirmedMethodLine(destination.Id, 100m) }, 100m);

        var act = async () => await m.BuildTranslator().Handle(evt, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Posting_failure_lanza_SupplierPaymentPostingFailedException_en_vez_de_loguear_warning()
    {
        var m = new Mocks();
        var destination = Destination();
        m.FinancialDestinations
            .Setup(f => f.GetByIdAsync(TenantId, destination.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(destination);
        m.PostingEngine
            .Setup(e => e.PostAsync(It.IsAny<PostingFact>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<PostingOutcomeDto>.ValidationFailure("No existe regla de contabilización.", "RULE_NOT_FOUND")
            );

        var evt = Event(new[] { new SupplierPaymentConfirmedMethodLine(destination.Id, 100m) }, 100m);

        var act = async () => await m.BuildTranslator().Handle(evt, CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<SupplierPaymentPostingFailedException>();
        thrown.Which.Code.Should().Be("RULE_NOT_FOUND");
    }

    [Fact]
    public async Task Destino_financiero_inexistente_bloquea_lanzando()
    {
        var m = new Mocks();
        var missingId = Guid.NewGuid();
        m.FinancialDestinations
            .Setup(f => f.GetByIdAsync(TenantId, missingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompanyFinancialDestination?)null);

        var evt = Event(new[] { new SupplierPaymentConfirmedMethodLine(missingId, 100m) }, 100m);

        var act = async () => await m.BuildTranslator().Handle(evt, CancellationToken.None);

        await act.Should().ThrowAsync<SupplierPaymentPostingFailedException>();
        m.PostingEngine.Verify(
            e => e.PostAsync(It.IsAny<PostingFact>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Destino_financiero_de_otra_empresa_bloquea_lanzando()
    {
        var m = new Mocks();
        var destination = Destination(companyId: Guid.NewGuid());
        m.FinancialDestinations
            .Setup(f => f.GetByIdAsync(TenantId, destination.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(destination);

        var evt = Event(new[] { new SupplierPaymentConfirmedMethodLine(destination.Id, 100m) }, 100m);

        var act = async () => await m.BuildTranslator().Handle(evt, CancellationToken.None);

        await act.Should().ThrowAsync<SupplierPaymentPostingFailedException>();
    }

    [Fact]
    public async Task Destino_financiero_inactivo_bloquea_lanzando()
    {
        var m = new Mocks();
        var destination = Destination(isActive: false);
        m.FinancialDestinations
            .Setup(f => f.GetByIdAsync(TenantId, destination.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(destination);

        var evt = Event(new[] { new SupplierPaymentConfirmedMethodLine(destination.Id, 100m) }, 100m);

        var act = async () => await m.BuildTranslator().Handle(evt, CancellationToken.None);

        await act.Should().ThrowAsync<SupplierPaymentPostingFailedException>();
        m.PostingEngine.Verify(
            e => e.PostAsync(It.IsAny<PostingFact>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }
}
