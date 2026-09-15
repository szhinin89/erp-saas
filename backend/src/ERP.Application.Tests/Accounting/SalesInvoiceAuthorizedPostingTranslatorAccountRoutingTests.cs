using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Accounting.Posting.Translators;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Sales.Events;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Accounting;

/// <summary>
/// SALES-TRANSFER-ACCOUNTING-CASH-VS-BANK-01 — confirma que
/// <see cref="SalesInvoiceAuthorizedPostingTranslator"/> enruta el Debe de "Sales/InvoiceIssued" a
/// la cuenta real por método de pago (via <c>PostingFact.Allocations</c>) cuando
/// <see cref="SalesInvoiceAuthorizedEvent.CashByAccount"/> viene poblado, y preserva el
/// comportamiento previo (línea fija de PostingRule, sin allocations) cuando viene vacío —
/// compatibilidad total con <c>SalesInvoiceAuthorizedPostingTranslatorCashSplitTests</c> (Lote 3,
/// no tocado).
/// </summary>
public sealed class SalesInvoiceAuthorizedPostingTranslatorAccountRoutingTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid CashSessionId = Guid.NewGuid();

    private static SalesInvoiceAuthorizedEvent Event(
        decimal grandTotal,
        decimal cashApplied,
        IReadOnlyDictionary<Guid, decimal>? cashByAccount = null
    ) =>
        new(
            Guid.NewGuid(),
            "001-001-000000001",
            grandTotal,
            UserId,
            CashSessionId,
            TenantId,
            CompanyId,
            new DateOnly(2026, 9, 14),
            grandTotal,
            0m,
            0m,
            0m,
            totalIrbpnr: 0m,
            cashApplied: cashApplied,
            cashByAccount: cashByAccount
        );

    private static async Task<PostingFact> CaptureFactAsync(SalesInvoiceAuthorizedEvent evt)
    {
        var postingEngine = new Mock<IPostingEngine>();
        PostingFact? captured = null;
        postingEngine
            .Setup(e => e.PostAsync(It.IsAny<PostingFact>(), It.IsAny<CancellationToken>()))
            .Callback<PostingFact, CancellationToken>((fact, _) => captured = fact)
            .ReturnsAsync(
                Result<PostingOutcomeDto>.Success(
                    new PostingOutcomeDto(Guid.NewGuid(), PostingOutcomeStatus.Created)
                )
            );

        var translator = new SalesInvoiceAuthorizedPostingTranslator(postingEngine.Object);
        await translator.Handle(evt, CancellationToken.None);

        return captured!;
    }

    [Fact]
    public async Task Transferencia_unica_genera_una_allocation_a_la_cuenta_configurada_y_CashApplied_en_cero()
    {
        var bancosAccountId = Guid.NewGuid();
        var fact = await CaptureFactAsync(
            Event(
                grandTotal: 2.64m,
                cashApplied: 2.64m,
                cashByAccount: new Dictionary<Guid, decimal> { [bancosAccountId] = 2.64m }
            )
        );

        // La línea fija "Debe Caja general" de la PostingRule queda anulada (0m) — nunca se
        // contabiliza contra ella cuando hay desglose por cuenta real.
        fact.CashApplied.Should().Be(0m);
        fact.PendingBalance.Should().Be(0m);

        fact.Allocations.Should().NotBeNull();
        fact.Allocations!.Should().ContainSingle();
        var allocation = fact.Allocations!.Single();
        allocation.AccountingAccountId.Should().Be(bancosAccountId);
        allocation.Amount.Should().Be(2.64m);
        allocation.Nature.Should().Be(AccountNature.Debit);
    }

    [Fact]
    public async Task Efectivo_mas_tarjeta_genera_dos_allocations_que_suman_CashApplied_original()
    {
        var cajaAccountId = Guid.NewGuid();
        var tarjetaAccountId = Guid.NewGuid();
        var fact = await CaptureFactAsync(
            Event(
                grandTotal: 100m,
                cashApplied: 100m,
                cashByAccount: new Dictionary<Guid, decimal>
                {
                    [cajaAccountId] = 60m,
                    [tarjetaAccountId] = 40m,
                }
            )
        );

        fact.CashApplied.Should().Be(0m);
        fact.Allocations.Should().HaveCount(2);
        fact.Allocations!.Sum(a => a.Amount).Should().Be(100m);
        fact.Allocations!.Should().Contain(a => a.AccountingAccountId == cajaAccountId && a.Amount == 60m);
        fact.Allocations!.Should().Contain(a => a.AccountingAccountId == tarjetaAccountId && a.Amount == 40m);
    }

    [Fact]
    public async Task Efectivo_sin_mapear_mas_transferencia_mapeada_deja_el_remanente_en_la_linea_fija()
    {
        // Compatibilidad (AuthorizeSalesInvoiceHandler): EFECTIVO sin PaymentMethodAccount
        // configurado NO entra a CashByAccount — su monto debe seguir contabilizándose vía la
        // línea fija histórica de la PostingRule ("Caja general"), nunca perderse ni duplicarse.
        var bancosAccountId = Guid.NewGuid();
        var fact = await CaptureFactAsync(
            Event(
                grandTotal: 100m,
                cashApplied: 100m, // 60 Efectivo (sin mapear) + 40 Transferencia (mapeada)
                cashByAccount: new Dictionary<Guid, decimal> { [bancosAccountId] = 40m }
            )
        );

        fact.CashApplied.Should().Be(60m, "el remanente no cubierto por allocations (Efectivo sin mapear) sigue yendo por la línea fija de la PostingRule");
        fact.Allocations.Should().ContainSingle();
        fact.Allocations!.Single().AccountingAccountId.Should().Be(bancosAccountId);
        fact.Allocations!.Single().Amount.Should().Be(40m);

        // Nunca se pierde ni se duplica un centavo del Debe total.
        (fact.CashApplied + fact.Allocations!.Sum(a => a.Amount)).Should().Be(100m);
    }

    [Fact]
    public async Task Sin_CashByAccount_comportamiento_identico_al_previo_sin_allocations()
    {
        // Callers/tests que no proveen CashByAccount (Lote 1/2/3 ya cerrados) deben seguir
        // resolviendo la cuenta fija de la PostingRule, sin ningún cambio de comportamiento.
        var fact = await CaptureFactAsync(Event(grandTotal: 115m, cashApplied: 115m));

        fact.CashApplied.Should().Be(115m);
        fact.Allocations.Should().BeNull();
    }
}
