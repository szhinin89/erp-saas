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
/// SUPPLIER-PAYMENTS-REVERSE-16 — reemplaza al test homónimo legacy (Finance/Payment, código
/// muerto eliminado junto con su traductor en este ticket). Mismo criterio estricto que
/// <see cref="SupplierPaymentConfirmedPostingTranslator"/>: un posting fallido debe LANZAR (nunca
/// solo loguear), para que la reversa completa se revierta.
/// </summary>
public sealed class SupplierPaymentReversedPostingTranslatorTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private sealed class Mocks
    {
        public Mock<IPostingEngine> PostingEngine { get; } = new();
        public Mock<ICompanyFinancialDestinationRepository> FinancialDestinations { get; } = new();

        public SupplierPaymentReversedPostingTranslator BuildTranslator() =>
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

    private static SupplierPaymentReversedEvent Event(
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
            "Error de digitación",
            methodLines,
            new[] { new SupplierPaymentReversedApplicationLine(Guid.NewGuid(), totalAmount) }
        );

    [Fact]
    public async Task Reversa_con_1_medio_genera_1_debito_y_acredita_CxP_por_el_total()
    {
        var m = new Mocks();
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
        captured!.SourceModule.Should().Be("Payables");
        captured.FactType.Should().Be("SupplierPaymentReversed");
        captured.SourceEventId.Should().Be(supplierPaymentId);
        captured.GrandTotal.Should().Be(300m, "el Haber de CxP se resuelve vía PostingRule con GrandTotal");
        captured.Allocations.Should().ContainSingle();
        var allocation = captured.Allocations!.Single();
        allocation.AccountingAccountId.Should().Be(destination.AccountingAccountId);
        allocation.Amount.Should().Be(300m);
        allocation.Nature.Should().Be(AccountNature.Debit, "el reverso invierte: Debe caja/banco, Haber CxP");
    }

    [Fact]
    public async Task Reversa_con_2_medios_genera_2_debitos_banco_caja()
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
        captured.Allocations!.Should().OnlyContain(a => a.Nature == AccountNature.Debit);
        captured.Allocations!.Sum(a => a.Amount).Should().Be(300m);
    }

    [Fact]
    public async Task Posting_exitoso_no_lanza()
    {
        var m = new Mocks();
        var destination = Destination();
        m.FinancialDestinations
            .Setup(f => f.GetByIdAsync(TenantId, destination.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(destination);
        m.PostingEngine
            .Setup(e => e.PostAsync(It.IsAny<PostingFact>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<PostingOutcomeDto>.Success(new PostingOutcomeDto(Guid.NewGuid(), PostingOutcomeStatus.Created))
            );

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
