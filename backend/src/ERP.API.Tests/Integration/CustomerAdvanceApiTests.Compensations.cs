using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Kernel.Permissions;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Caja.Enums;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ERP.API.Tests.Integration;

/// <summary>
/// CUSTOMER-ADVANCES-V1 CA-F5 — devolución, reversa de devolución, reversa del ingreso y anulación
/// administrativa con HTTP real + PostgreSQL real. Tras cada operación se comprueba que el saldo del
/// anticipo, la cuenta contable de anticipos y el efectivo del turno cuadran entre sí.
/// </summary>
public sealed partial class CustomerAdvanceApiTests
{
    private const string DomainRule = "DOMAIN_RULE_VIOLATION";

    private sealed record Ctx(
        Seeded S,
        FinancialCommandIdempotencyFixture.Operator Op,
        HttpClient Client
    );

    // ── Devolución ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Devolucion_parcial_y_total_mueve_el_destino_y_contabiliza_una_vez(bool cash)
    {
        var c = await StartAsync();
        var advance = await CreateAdvanceAsync(c, 100m, cash);
        var cashBefore = await CashBalanceAsync(c.Op.CashSessionId);

        var partial = await RefundAsync(c, advance, Refund(c, 40m, cash));

        partial.Status.Should().Be(HttpStatusCode.OK, partial.Body);
        await AssertBalancesAsync(c, advance, 60m, CustomerAdvanceStatus.Confirmed);
        (await CashBalanceAsync(c.Op.CashSessionId))
            .Should()
            .Be(cash ? cashBefore - 40m : cashBefore);

        var total = await RefundAsync(c, advance, Refund(c, 60m, cash));

        total.Status.Should().Be(HttpStatusCode.OK, total.Body);
        await AssertBalancesAsync(c, advance, 0m, CustomerAdvanceStatus.Confirmed);
        var transactions = await TransactionsAsync(advance);
        transactions.Should().HaveCount(2);
        total.Data.GetProperty("refundTransactions").GetArrayLength().Should().Be(2);
        foreach (var transaction in transactions)
        {
            transaction.TransactionType.Should().Be(CustomerAdvanceRefundTransactionType.Refund);
            var effects = await MovementEffectsAsync(transaction.CustomerAdvanceMovementId);
            effects
                .Postings.Should()
                .ContainSingle()
                .Which.Should()
                .Be(("CustomerAdvanceRefunded", AccountingPostingStatus.Posted));
            effects
                .Lines.Should()
                .BeEquivalentTo(
                    new[]
                    {
                        (c.S.AdvancesAccount, transaction.Amount, 0m),
                        (
                            cash ? _f.CashAccountId : c.S.BankAccountingAccount,
                            0m,
                            transaction.Amount
                        ),
                    }
                );
            var movements = await CashMovementsAsync(transaction.Id);
            if (!cash)
            {
                movements.Should().BeEmpty();
                transaction.CashMovementId.Should().BeNull();
                continue;
            }
            var cashMovement = movements.Should().ContainSingle().Subject;
            cashMovement.MovementType.Should().Be(CashMovementType.ManualExpense);
            cashMovement.ReferenceType.Should().Be(CashReferenceType.CustomerAdvance);
            cashMovement.Amount.Should().Be(transaction.Amount);
            transaction.CashMovementId.Should().Be(cashMovement.Id);
            transaction.CashSessionId.Should().Be(c.Op.CashSessionId);
        }
        (await CajaPostingsAsync()).Should().Be(0, "Caja nunca contabiliza estos movimientos");
        (await AuditsAsync(advance)).Should().BeEquivalentTo(["Received", "Refunded", "Refunded"]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task La_devolucion_admite_un_destino_distinto_del_ingreso(bool incomeCash)
    {
        var c = await StartAsync();
        var advance = await CreateAdvanceAsync(c, 80m, incomeCash);

        var response = await RefundAsync(c, advance, Refund(c, 30m, !incomeCash));

        response.Status.Should().Be(HttpStatusCode.OK, response.Body);
        var transaction = (await TransactionsAsync(advance)).Single();
        (transaction.CashRegisterId is not null).Should().Be(!incomeCash);
        (await MovementEffectsAsync(transaction.CustomerAdvanceMovementId))
            .Lines.Should()
            .Contain((incomeCash ? c.S.BankAccountingAccount : _f.CashAccountId, 0m, 30m));
        await AssertBalancesAsync(c, advance, 50m, CustomerAdvanceStatus.Confirmed);
    }

    [Theory]
    [InlineData("overAvailable", 422)]
    [InlineData("noDestination", 422)]
    [InlineData("twoDestinations", 422)]
    [InlineData("cashMethodToBank", 422)]
    [InlineData("transferToCash", 422)]
    [InlineData("card", 422)]
    [InlineData("cheque", 422)]
    [InlineData("credit", 422)]
    [InlineData("inactiveMethod", 422)]
    [InlineData("unknownMethod", 404)]
    [InlineData("missingReference", 422)]
    [InlineData("missingReason", 422)]
    [InlineData("zeroAmount", 422)]
    [InlineData("threeDecimals", 422)]
    [InlineData("noClientRequestId", 422)]
    [InlineData("inactiveBank", 422)]
    [InlineData("unknownBank", 404)]
    [InlineData("otherCompanyBank", 404)]
    [InlineData("otherCompanyRegister", 404)]
    [InlineData("foreignAccountingAccount", 422)]
    [InlineData("inactiveRegister", 422)]
    [InlineData("noOpenSession", 422)]
    [InlineData("anotherUsersSession", 422)]
    [InlineData("insufficientCash", 422)]
    [InlineData("futureDate", 422)]
    [InlineData("beforeAdvanceDate", 422)]
    [InlineData("closedPeriod", 422)]
    [InlineData("unknownAdvance", 404)]
    public async Task Una_devolucion_invalida_se_rechaza_sin_efectos(string scenario, int status)
    {
        var c = await StartAsync();
        var advance = await CreateAdvanceAsync(c, 5000m, cash: false);
        var target = advance;
        var body = Refund(c, 10m, cash: false);
        switch (scenario)
        {
            case "overAvailable":
                body = body with { Amount = 5000.01m };
                break;
            case "noDestination":
                body = body with { CompanyBankAccountId = null };
                break;
            case "twoDestinations":
                body = body with { CashRegisterId = c.Op.CashRegisterId };
                break;
            case "cashMethodToBank":
                body = body with { PaymentMethodId = _f.CashMethodId };
                break;
            case "transferToCash":
                body = body with
                {
                    CompanyBankAccountId = null,
                    CashRegisterId = c.Op.CashRegisterId,
                };
                break;
            case "card":
                body = body with { PaymentMethodId = c.S.CardMethod };
                break;
            case "cheque":
                body = body with { PaymentMethodId = c.S.ChequeMethod };
                break;
            case "credit":
                body = body with { PaymentMethodId = c.S.CreditMethod };
                break;
            case "inactiveMethod":
                body = body with { PaymentMethodId = c.S.InactiveMethod };
                break;
            case "unknownMethod":
                body = body with { PaymentMethodId = Guid.NewGuid() };
                break;
            case "missingReference":
                body = body with { Reference = " " };
                break;
            case "missingReason":
                body = body with { Reason = " " };
                break;
            case "zeroAmount":
                body = body with { Amount = 0m };
                break;
            case "threeDecimals":
                body = body with { Amount = 1.001m };
                break;
            case "noClientRequestId":
                body = body with { ClientRequestId = Guid.Empty };
                break;
            case "inactiveBank":
                body = body with
                {
                    CompanyBankAccountId = await AddBankAccountAsync(c.S, active: false),
                };
                break;
            case "unknownBank":
                body = body with { CompanyBankAccountId = Guid.NewGuid() };
                break;
            case "otherCompanyBank":
                body = body with { CompanyBankAccountId = (await AddOtherCompanyAsync(c.S)).Bank };
                break;
            case "otherCompanyRegister":
                body = Refund(c, 10m, cash: true) with
                {
                    CashRegisterId = (await AddOtherCompanyAsync(c.S)).Register,
                };
                break;
            case "foreignAccountingAccount":
                body = body with
                {
                    CompanyBankAccountId = await AddBankAccountAsync(
                        c.S,
                        accountId: (await AddOtherCompanyAsync(c.S)).Account
                    ),
                };
                break;
            case "inactiveRegister":
                await _f.SetCashRegisterActiveAsync(c.Op.CashRegisterId, active: false);
                body = Refund(c, 10m, cash: true);
                break;
            case "noOpenSession":
                body = Refund(c, 10m, cash: true) with
                {
                    CashRegisterId = await _f.CreateCashRegisterWithoutAccountAsync(),
                };
                break;
            case "anotherUsersSession":
                var other = await _f.CreateOperatorAsync();
                _f.CreateClient(c.Op.UserId);
                body = Refund(c, 10m, cash: true) with { CashRegisterId = other.CashRegisterId };
                break;
            case "insufficientCash":
                body = Refund(c, 2000m, cash: true);
                break;
            case "futureDate":
                body = body with { EffectiveDate = Today.AddDays(1) };
                break;
            case "beforeAdvanceDate":
                body = body with { EffectiveDate = Today.AddDays(-1) };
                break;
            case "closedPeriod":
                break;
            case "unknownAdvance":
                target = Guid.NewGuid();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario));
        }
        var before = await SnapshotAsync();
        var cashBefore = await CashBalanceAsync(c.Op.CashSessionId);

        var response =
            scenario == "closedPeriod"
                ? await WithClosedPeriodAsync(() => RefundAsync(c, target, body))
                : await RefundAsync(c, target, body);

        ((int)response.Status).Should().Be(status, response.Body);
        if (scenario == "closedPeriod")
            response.Body.Should().Contain("ACCOUNTING_PERIOD_CLOSED");
        (await SnapshotAsync()).Should().Be(before);
        (await CashBalanceAsync(c.Op.CashSessionId)).Should().Be(cashBefore);
    }

    [Fact]
    public async Task Lo_aplicado_a_una_CxC_no_se_devuelve_hasta_revertirlo()
    {
        var c = await StartAsync();
        var advance = await CreateAdvanceAsync(c, 100m, cash: false);
        var operation = await ApplyToNewReceivableAsync(c, advance, 70m);

        var rejected = await RefundAsync(c, advance, Refund(c, 40m, cash: false));
        var allowed = await RefundAsync(c, advance, Refund(c, 30m, cash: false));

        rejected.Status.Should().Be(HttpStatusCode.UnprocessableEntity, rejected.Body);
        rejected.Code.Should().Be(DomainRule);
        allowed.Status.Should().Be(HttpStatusCode.OK, allowed.Body);
        (await ReverseApplicationAsync(c, advance, operation))
            .Status.Should()
            .Be(HttpStatusCode.OK);
        (await RefundAsync(c, advance, Refund(c, 70m, cash: false)))
            .Status.Should()
            .Be(HttpStatusCode.OK);
        await AssertBalancesAsync(c, advance, 0m, CustomerAdvanceStatus.Confirmed);
    }

    // ── Reversa de una devolución ────────────────────────────────────────

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Revertir_una_devolucion_restaura_el_mismo_importe_y_destino(bool cash)
    {
        var c = await StartAsync();
        var advance = await CreateAdvanceAsync(c, 100m, cash: false);
        await RefundAsync(c, advance, Refund(c, 45.50m, cash));
        var original = (await TransactionsAsync(advance)).Single();
        var cashBefore = await CashBalanceAsync(c.Op.CashSessionId);

        var response = await ReverseRefundAsync(c, advance, original.Id, Evidence(Today));

        response.Status.Should().Be(HttpStatusCode.OK, response.Body);
        await AssertBalancesAsync(c, advance, 100m, CustomerAdvanceStatus.Confirmed);
        var reversal = (await TransactionsAsync(advance)).Single(t =>
            t.OriginalTransactionId == original.Id
        );
        reversal.Amount.Should().Be(45.50m);
        reversal.CompanyBankAccountId.Should().Be(original.CompanyBankAccountId);
        reversal.CashRegisterId.Should().Be(original.CashRegisterId);
        reversal.PaymentMethodId.Should().Be(original.PaymentMethodId);
        reversal.EffectiveDate.Should().Be(Today);
        var movement = await MovementAsync(reversal.CustomerAdvanceMovementId);
        movement.MovementType.Should().Be(CustomerAdvanceMovementType.ReversalOfRefund);
        movement.ReversalOfMovementId.Should().Be(original.CustomerAdvanceMovementId);
        movement.EvidenceDate.Should().Be(Today);
        var effects = await MovementEffectsAsync(original.CustomerAdvanceMovementId);
        effects.Entries.Should().Be(1);
        effects
            .MirrorLines.Should()
            .BeEquivalentTo(effects.Lines.Select(l => (l.Account, l.Credit, l.Debit)));
        (await CashBalanceAsync(c.Op.CashSessionId))
            .Should()
            .Be(cash ? cashBefore + 45.50m : cashBefore);
        var cashMovements = await CashMovementsAsync(original.Id);
        if (cash)
        {
            cashMovements
                .Select(m => (m.MovementType, m.Amount))
                .Should()
                .BeEquivalentTo(
                    new[]
                    {
                        (CashMovementType.ManualExpense, 45.50m),
                        (CashMovementType.ManualIncome, 45.50m),
                    }
                );
            reversal.CashMovementId.Should().NotBeNull();
        }
        else
            cashMovements.Should().BeEmpty();
        response
            .Data.GetProperty("refundTransactions")
            .EnumerateArray()
            .Single(t => t.GetProperty("id").GetGuid() == original.Id)
            .GetProperty("isReversed")
            .GetBoolean()
            .Should()
            .BeTrue();

        var again = await ReverseRefundAsync(c, advance, original.Id, Evidence(Today));
        again.Status.Should().Be(HttpStatusCode.UnprocessableEntity, again.Body);
    }

    [Theory]
    [InlineData("missingReason", 422)]
    [InlineData("missingReference", 422)]
    [InlineData("missingEvidenceDate", 422)]
    [InlineData("unknownTransaction", 404)]
    [InlineData("otherAdvanceTransaction", 404)]
    [InlineData("reversalAsTarget", 422)]
    [InlineData("anotherUsersSession", 422)]
    [InlineData("noOpenSession", 422)]
    [InlineData("cancelledAdvance", 422)]
    [InlineData("closedPeriod", 422)]
    public async Task Una_reversa_de_devolucion_invalida_no_cambia_el_saldo(
        string scenario,
        int status
    )
    {
        var c = await StartAsync();
        var cancellable = scenario == "cancelledAdvance";
        var advance = cancellable
            ? await CreateUncountedAdvanceAsync(c, 100m)
            : await CreateAdvanceAsync(c, 100m, cash: false);
        await RefundAsync(c, advance, Refund(c, 20m, cash: true));
        var original = (await TransactionsAsync(advance)).Single();
        var target = original.Id;
        var body = Evidence(Today);
        var client = c.Client;
        switch (scenario)
        {
            case "missingReason":
                body = body with { Reason = " " };
                break;
            case "missingReference":
                body = body with { Reference = " " };
                break;
            case "missingEvidenceDate":
                body = body with { EvidenceDate = default };
                break;
            case "unknownTransaction":
                target = Guid.NewGuid();
                break;
            case "otherAdvanceTransaction":
                var second = await CreateAdvanceAsync(c, 10m, cash: false);
                await RefundAsync(c, second, Refund(c, 5m, cash: false));
                target = (await TransactionsAsync(second)).Single().Id;
                break;
            case "reversalAsTarget":
                (await ReverseRefundAsync(c, advance, original.Id, Evidence(Today)))
                    .Status.Should()
                    .Be(HttpStatusCode.OK);
                target = (await TransactionsAsync(advance))
                    .Single(t => t.OriginalTransactionId == original.Id)
                    .Id;
                break;
            case "anotherUsersSession":
                client = _f.CreateClient((await _f.CreateOperatorAsync()).UserId);
                break;
            case "noOpenSession":
                await CloseSessionAsync(c.Op.CashSessionId);
                break;
            case "cancelledAdvance":
                (await ReverseRefundAsync(c, advance, original.Id, Evidence(Today)))
                    .Status.Should()
                    .Be(HttpStatusCode.OK);
                var cancelled = await CancelAsync(c, advance, Cancellation());
                cancelled.Status.Should().Be(HttpStatusCode.OK, cancelled.Body);
                break;
        }
        var before = await SnapshotAsync();

        Task<Response> Send() =>
            ReverseRefundAsync(c with { Client = client }, advance, target, body);
        var response =
            scenario == "closedPeriod" ? await WithClosedPeriodAsync(Send) : await Send();

        ((int)response.Status).Should().Be(status, response.Body);
        (await SnapshotAsync()).Should().Be(before);
    }

    [Fact]
    public async Task Revertir_una_devolucion_sin_asiento_cancela_su_obligacion()
    {
        var c = await StartAsync();
        var advance = await CreateAdvanceAsync(c, 60m, cash: false);
        var inactive = await AddAccountAsync(_f.CompanyId, c.S.UserId, active: false);
        var body = Refund(c, 25m, cash: false) with
        {
            CompanyBankAccountId = await AddBankAccountAsync(c.S, accountId: inactive),
        };
        (await RefundAsync(c, advance, body)).Status.Should().Be(HttpStatusCode.OK);
        var original = (await TransactionsAsync(advance)).Single();
        (await MovementEffectsAsync(original.CustomerAdvanceMovementId))
            .Postings.Should()
            .ContainSingle()
            .Which.Status.Should()
            .Be(AccountingPostingStatus.Failed);

        var response = await ReverseRefundAsync(c, advance, original.Id, Evidence(Today));

        response.Status.Should().Be(HttpStatusCode.OK, response.Body);
        var effects = await MovementEffectsAsync(original.CustomerAdvanceMovementId);
        effects
            .Postings.Should()
            .ContainSingle()
            .Which.Status.Should()
            .Be(AccountingPostingStatus.Canceled);
        effects.Entries.Should().Be(0);
        effects.MirrorLines.Should().BeEmpty();
        await AssertBalancesAsync(c, advance, 60m, CustomerAdvanceStatus.Confirmed);
    }

    // ── Reversa del ingreso ──────────────────────────────────────────────

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Revertir_el_ingreso_cancela_el_anticipo_y_espeja_su_asiento(bool cash)
    {
        var c = await StartAsync();
        await EnsurePeriodAsync(Today.AddDays(-3));
        var advance = await CreateAdvanceAsync(c, 120m, cash, Today.AddDays(-3));
        var received = (await MovementsAsync(advance)).Single();
        var cashBefore = await CashBalanceAsync(c.Op.CashSessionId);

        var response = await ReverseIncomeAsync(c, advance, Evidence(Today.AddDays(-3)));

        response.Status.Should().Be(HttpStatusCode.OK, response.Body);
        response.Data.GetProperty("status").GetString().Should().Be("Cancelled");
        await AssertBalancesAsync(c, advance, 0m, CustomerAdvanceStatus.Cancelled);
        var reversal = (await MovementsAsync(advance)).Single(m =>
            m.MovementType == CustomerAdvanceMovementType.IncomeReversed
        );
        reversal.Amount.Should().Be(120m);
        reversal.ReversalOfMovementId.Should().Be(received.Id);
        reversal
            .EffectiveDate.Should()
            .Be(Today, "TA-07: la fecha contable no la elige el usuario");
        reversal.EvidenceDate.Should().Be(Today.AddDays(-3));
        var effects = await MovementEffectsAsync(received.Id);
        effects.Entries.Should().Be(1);
        effects.MirrorLines.Should().HaveCount(2);
        effects
            .MirrorLines.Should()
            .BeEquivalentTo(effects.Lines.Select(l => (l.Account, l.Credit, l.Debit)));
        (await CashBalanceAsync(c.Op.CashSessionId))
            .Should()
            .Be(cash ? cashBefore - 120m : cashBefore);
        var cashMovements = await CashMovementsAsync(received.Id);
        if (cash)
        {
            var expense = cashMovements.Single(m =>
                m.MovementType == CashMovementType.ManualExpense
            );
            expense.Amount.Should().Be(120m);
            reversal.CashMovementId.Should().Be(expense.Id);
        }
        else
            cashMovements.Should().BeEmpty();
        (await MovementsAsync(advance))
            .Single(m => m.Id == received.Id)
            .Should()
            .BeEquivalentTo(received, "el historial no se reescribe");

        var again = await ReverseIncomeAsync(c, advance, Evidence(Today));
        again.Status.Should().Be(HttpStatusCode.UnprocessableEntity, again.Body);
    }

    [Theory]
    [InlineData("missingReason")]
    [InlineData("missingReference")]
    [InlineData("missingEvidenceDate")]
    [InlineData("applied")]
    [InlineData("refunded")]
    [InlineData("insufficientCash")]
    [InlineData("noOpenSession")]
    [InlineData("anotherUsersSession")]
    [InlineData("closedPeriod")]
    public async Task Una_reversa_del_ingreso_invalida_se_rechaza_sin_efectos(string scenario)
    {
        var c = await StartAsync(openingCash: 0m);
        var advance = await CreateAdvanceAsync(c, 100m, cash: true);
        var body = Evidence(Today);
        var client = c.Client;
        switch (scenario)
        {
            case "missingReason":
                body = body with { Reason = " " };
                break;
            case "missingReference":
                body = body with { Reference = " " };
                break;
            case "missingEvidenceDate":
                body = body with { EvidenceDate = default };
                break;
            case "applied":
                await ApplyToNewReceivableAsync(c, advance, 10m);
                break;
            case "refunded":
                (await RefundAsync(c, advance, Refund(c, 10m, cash: false)))
                    .Status.Should()
                    .Be(HttpStatusCode.OK);
                break;
            case "insufficientCash":
                // El efectivo del turno sale devolviendo otro anticipo recibido por transferencia.
                var other = await CreateAdvanceAsync(c, 100m, cash: false);
                (await RefundAsync(c, other, Refund(c, 100m, cash: true)))
                    .Status.Should()
                    .Be(HttpStatusCode.OK);
                break;
            case "noOpenSession":
                await CloseSessionAsync(c.Op.CashSessionId);
                break;
            case "anotherUsersSession":
                client = _f.CreateClient((await _f.CreateOperatorAsync()).UserId);
                break;
        }
        var before = await SnapshotAsync();

        Task<Response> Send() => ReverseIncomeAsync(c with { Client = client }, advance, body);
        var response =
            scenario == "closedPeriod" ? await WithClosedPeriodAsync(Send) : await Send();

        response.Status.Should().Be(HttpStatusCode.UnprocessableEntity, response.Body);
        (await SnapshotAsync()).Should().Be(before);
    }

    [Fact]
    public async Task Revertir_un_ingreso_sin_asiento_cancela_su_obligacion()
    {
        var c = await StartAsync();
        var advance = await CreateUncountedAdvanceAsync(c, 35m);
        var received = (await MovementsAsync(advance)).Single();

        var response = await ReverseIncomeAsync(c, advance, Evidence(Today));

        response.Status.Should().Be(HttpStatusCode.OK, response.Body);
        var effects = await MovementEffectsAsync(received.Id);
        effects
            .Postings.Should()
            .ContainSingle()
            .Which.Status.Should()
            .Be(AccountingPostingStatus.Canceled);
        effects.Entries.Should().Be(0);
        effects.MirrorLines.Should().BeEmpty();
    }

    // ── Anulación administrativa ─────────────────────────────────────────

    [Theory]
    [InlineData(AccountingPostingStatus.Failed)]
    [InlineData(AccountingPostingStatus.Pending)]
    public async Task Anular_un_registro_sin_dinero_recibido_cancela_la_obligacion_sin_asiento(
        AccountingPostingStatus obligation
    )
    {
        var c = await StartAsync();
        var advance = await CreateUncountedAdvanceAsync(c, 90m);
        var received = (await MovementsAsync(advance)).Single();
        if (obligation == AccountingPostingStatus.Pending)
            await DbAsync(async db =>
            {
                var posting = await db
                    .AccountingPostings.IgnoreQueryFilters()
                    .SingleAsync(p => p.SourceEventId == received.Id);
                db.Entry(posting).Property(p => p.Status).CurrentValue = obligation;
            });
        var before = await SnapshotAsync();

        var response = await CancelAsync(c, advance, Cancellation());

        response.Status.Should().Be(HttpStatusCode.OK, response.Body);
        await AssertBalancesAsync(c, advance, 0m, CustomerAdvanceStatus.Cancelled);
        var cancellation = (await MovementsAsync(advance)).Single(m =>
            m.MovementType == CustomerAdvanceMovementType.SourceCancelled
        );
        cancellation.ReversalOfMovementId.Should().Be(received.Id);
        cancellation.EffectiveDate.Should().Be(Today);
        var effects = await MovementEffectsAsync(received.Id);
        effects
            .Postings.Should()
            .ContainSingle()
            .Which.Status.Should()
            .Be(AccountingPostingStatus.Canceled);
        effects.Entries.Should().Be(0);
        var after = await SnapshotAsync();
        (after.CashMovements, after.Entries, after.Postings, after.Transactions)
            .Should()
            .Be((before.CashMovements, before.Entries, before.Postings, before.Transactions));
        (await AuditsAsync(advance)).Should().BeEquivalentTo(["Received", "Cancelled"]);
    }

    [Theory]
    [InlineData("noDeclaration")]
    [InlineData("missingReference")]
    [InlineData("missingReason")]
    [InlineData("postedEntry")]
    [InlineData("cash")]
    [InlineData("applied")]
    [InlineData("refunded")]
    public async Task Una_anulacion_no_elegible_se_rechaza_sin_efectos(string scenario)
    {
        var c = await StartAsync();
        Guid advance;
        if (scenario == "postedEntry")
            advance = await CreateAdvanceAsync(c, 50m, cash: false);
        else if (scenario == "cash")
        {
            await DbAsync(async db =>
            {
                var register = await db
                    .CashRegisters.IgnoreQueryFilters()
                    .SingleAsync(r => r.Id == c.Op.CashRegisterId);
                db.Entry(register).Property(r => r.AccountingAccountId).CurrentValue = null;
            });
            advance = await CreateAdvanceAsync(c, 50m, cash: true);
        }
        else
            advance = await CreateUncountedAdvanceAsync(c, 50m);
        var body = Cancellation();
        switch (scenario)
        {
            case "noDeclaration":
                body = body with { MoneyNotReceived = false };
                break;
            case "missingReference":
                body = body with { Reference = " " };
                break;
            case "missingReason":
                body = body with { Reason = " " };
                break;
            case "applied":
                await ApplyToNewReceivableAsync(c, advance, 10m);
                break;
            case "refunded":
                (await RefundAsync(c, advance, Refund(c, 10m, cash: false)))
                    .Status.Should()
                    .Be(HttpStatusCode.OK);
                break;
        }
        var before = await SnapshotAsync();

        var response = await CancelAsync(c, advance, body);

        response.Status.Should().Be(HttpStatusCode.UnprocessableEntity, response.Body);
        (await SnapshotAsync()).Should().Be(before);
    }

    // ── Idempotencia ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("refund", false)]
    [InlineData("refund", true)]
    [InlineData("reverseRefund", false)]
    [InlineData("reverseRefund", true)]
    [InlineData("reverseIncome", false)]
    [InlineData("reverseIncome", true)]
    [InlineData("cancel", false)]
    [InlineData("cancel", true)]
    public async Task Cada_operacion_es_idempotente(string operation, bool concurrent)
    {
        var c = await StartAsync();
        var (advance, send) = await PrepareAsync(c, operation, Guid.NewGuid());
        var before = await SnapshotAsync();

        var responses = concurrent
            ? await Task.WhenAll(Enumerable.Range(0, ConcurrentRequests).Select(_ => send()))
            : [await send(), await send()];

        responses
            .Should()
            .OnlyContain(
                r => r.Status == HttpStatusCode.OK,
                string.Join('\n', responses.Select(r => r.Body))
            );
        responses.Select(r => r.Id).Distinct().Should().ContainSingle().Which.Should().Be(advance);
        var after = await SnapshotAsync();
        (after.Movements - before.Movements).Should().Be(1);
        (after.Audits - before.Audits).Should().Be(1);
        (after.Transactions - before.Transactions)
            .Should()
            .Be(operation is "refund" or "reverseRefund" ? 1 : 0);
        (after.Outbox - before.Outbox).Should().Be(1);
    }

    [Theory]
    [InlineData("refund")]
    [InlineData("reverseRefund")]
    [InlineData("reverseIncome")]
    [InlineData("cancel")]
    public async Task Una_clave_ya_usada_por_otra_operacion_o_empresa_responde_409(string operation)
    {
        var c = await StartAsync();
        var registration = Transfer(c.S, 15m);
        (await PostAsync(c.Client, registration)).Status.Should().Be(HttpStatusCode.Created);
        var foreignKey = Guid.NewGuid();
        var foreign = await AddAdvanceInOtherCompanyAsync(c.S, foreignKey, amount: 777m);

        foreach (var key in new[] { registration.ClientRequestId, foreignKey })
        {
            var (_, send) = await PrepareAsync(c, operation, key);
            var before = await SnapshotAsync();

            var response = await send();

            response.Status.Should().Be(HttpStatusCode.Conflict, response.Body);
            response.Body.Should().NotContain(foreign.ToString()).And.NotContain(":777");
            (await SnapshotAsync()).Should().Be(before);
        }
    }

    [Fact]
    public async Task Otros_datos_responden_409_un_rechazo_no_consume_la_clave_y_el_replay_no_se_reevalua()
    {
        var c = await StartAsync();
        var advance = await CreateAdvanceAsync(c, 50m, cash: false);
        var body = Refund(c, 60m, cash: false);

        var rejected = await RefundAsync(c, advance, body);
        var accepted = await RefundAsync(c, advance, body with { Amount = 50m });
        var conflict = await RefundAsync(c, advance, body with { Amount = 49m });

        rejected.Status.Should().Be(HttpStatusCode.UnprocessableEntity, rejected.Body);
        accepted.Status.Should().Be(HttpStatusCode.OK, accepted.Body);
        conflict.Status.Should().Be(HttpStatusCode.Conflict, conflict.Body);
        // El saldo ya está agotado y el banco queda inactivo: el reintento no se vuelve a evaluar.
        await DbAsync(async db =>
        {
            var bank = await db.Set<CompanyBankAccount>()
                .IgnoreQueryFilters()
                .SingleAsync(b => b.Id == c.S.BankAccount);
            db.Entry(bank).Property(b => b.IsActive).CurrentValue = false;
        });
        try
        {
            var replay = await RefundAsync(c, advance, body with { Amount = 50m });
            replay.Status.Should().Be(HttpStatusCode.OK, replay.Body);
        }
        finally
        {
            await DbAsync(async db =>
            {
                var bank = await db.Set<CompanyBankAccount>()
                    .IgnoreQueryFilters()
                    .SingleAsync(b => b.Id == c.S.BankAccount);
                db.Entry(bank).Property(b => b.IsActive).CurrentValue = true;
            });
        }
        await AssertBalancesAsync(c, advance, 0m, CustomerAdvanceStatus.Confirmed);
    }

    // ── Concurrencia ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("refund-refund")]
    [InlineData("refund-apply")]
    [InlineData("reverseIncome-refund")]
    [InlineData("reverseIncome-apply")]
    [InlineData("cancel-refund")]
    [InlineData("cancel-apply")]
    [InlineData("reverseRefund-reverseRefund")]
    [InlineData("reverseIncome-reverseIncome")]
    [InlineData("cancel-cancel")]
    [InlineData("cancel-reverseIncome")]
    public async Task Dos_operaciones_que_compiten_dejan_un_solo_ganador(string race)
    {
        var c = await StartAsync();
        for (var round = 0; round < 3; round++)
        {
            var uncounted = race.StartsWith("cancel", StringComparison.Ordinal);
            var advance = uncounted
                ? await CreateUncountedAdvanceAsync(c, 100m)
                : await CreateAdvanceAsync(c, 100m, cash: false);
            Guid original = default;
            if (race == "reverseRefund-reverseRefund")
            {
                await RefundAsync(c, advance, Refund(c, 100m, cash: false));
                original = (await TransactionsAsync(advance)).Single().Id;
            }
            Guid receivable = default;
            if (race.EndsWith("apply", StringComparison.Ordinal))
                receivable = await CreateDatedReceivableAsync(100m);
            var before = await SnapshotAsync();

            Task<Response> Start(string operation) =>
                operation switch
                {
                    "refund" => RefundAsync(c, advance, Refund(c, 100m, cash: false)),
                    "apply" => ApplyAsync(c, advance, receivable, 100m, Guid.NewGuid()),
                    "reverseIncome" => ReverseIncomeAsync(c, advance, Evidence(Today)),
                    "reverseRefund" => ReverseRefundAsync(c, advance, original, Evidence(Today)),
                    _ => CancelAsync(c, advance, Cancellation()),
                };
            var parts = race.Split('-');
            var responses = await Task.WhenAll(Start(parts[0]), Start(parts[1]));

            responses
                .Select(r => r.Status)
                .Should()
                .BeEquivalentTo(
                    new[] { HttpStatusCode.OK, HttpStatusCode.UnprocessableEntity },
                    string.Join('\n', responses.Select(r => r.Body))
                );
            (await SnapshotAsync()).Movements.Should().Be(before.Movements + 1);
        }
    }

    [Fact]
    public async Task Devolver_mientras_se_revierte_una_aplicacion_no_pierde_ni_duplica_saldo()
    {
        var c = await StartAsync();
        for (var round = 0; round < 4; round++)
        {
            var advance = await CreateAdvanceAsync(c, 100m, cash: false);
            var operation = await ApplyToNewReceivableAsync(c, advance, 100m);

            var reversal = ReverseApplicationAsync(c, advance, operation);
            var refund = RefundAsync(c, advance, Refund(c, 100m, cash: false));
            await Task.WhenAll(reversal, refund);

            reversal.Result.Status.Should().Be(HttpStatusCode.OK, reversal.Result.Body);
            refund
                .Result.Status.Should()
                .BeOneOf(HttpStatusCode.OK, HttpStatusCode.UnprocessableEntity);
            await AssertBalancesAsync(
                c,
                advance,
                refund.Result.Status == HttpStatusCode.OK ? 0m : 100m,
                CustomerAdvanceStatus.Confirmed
            );
        }
    }

    [Fact]
    public async Task Dos_devoluciones_en_efectivo_del_mismo_turno_no_lo_sobregiran()
    {
        var c = await StartAsync(openingCash: 100m);
        var first = await CreateAdvanceAsync(c, 80m, cash: false);
        var second = await CreateAdvanceAsync(c, 80m, cash: false);

        var responses = await Task.WhenAll(
            RefundAsync(c, first, Refund(c, 80m, cash: true)),
            RefundAsync(c, second, Refund(c, 80m, cash: true))
        );

        responses
            .Select(r => r.Status)
            .Should()
            .BeEquivalentTo(new[] { HttpStatusCode.OK, HttpStatusCode.UnprocessableEntity });
        (await CashBalanceAsync(c.Op.CashSessionId)).Should().Be(20m);
    }

    // ── Seguridad y aislamiento ──────────────────────────────────────────

    [Fact]
    public async Task Los_permisos_de_devolver_y_de_anular_son_independientes()
    {
        var c = await StartAsync();
        var advance = await CreateUncountedAdvanceAsync(c, 40m);
        var unknown = Guid.NewGuid();

        async Task<HttpStatusCode[]> StatusesAsync(params string[] permissions)
        {
            var client = _f.CreateClient(
                await _f.CreateUserWithPermissionsAsync(permissions),
                "User"
            );
            var limited = c with { Client = client };
            return
            [
                (await RefundAsync(limited, unknown, Refund(c, 1m, cash: false))).Status,
                (await ReverseRefundAsync(limited, unknown, unknown, Evidence(Today))).Status,
                (await ReverseIncomeAsync(limited, unknown, Evidence(Today))).Status,
                (await CancelAsync(limited, unknown, Cancellation())).Status,
            ];
        }
        var before = await SnapshotAsync();

        (await StatusesAsync(FinancePermissions.View))
            .Should()
            .OnlyContain(s => s == HttpStatusCode.Forbidden);
        (await StatusesAsync(FinancePermissions.CustomerAdvancesRefund))
            .Should()
            .Equal(
                HttpStatusCode.NotFound,
                HttpStatusCode.NotFound,
                HttpStatusCode.NotFound,
                HttpStatusCode.Forbidden
            );
        (await StatusesAsync(FinancePermissions.CustomerAdvancesReverse))
            .Should()
            .Equal(
                HttpStatusCode.Forbidden,
                HttpStatusCode.Forbidden,
                HttpStatusCode.Forbidden,
                HttpStatusCode.NotFound
            );
        (await SnapshotAsync()).Should().Be(before);
        advance.Should().NotBe(Guid.Empty);
    }

    [Theory]
    [InlineData("otherCompany")]
    [InlineData("unauthorizedBranch")]
    [InlineData("missingBranch")]
    public async Task Ninguna_operacion_alcanza_otra_empresa_ni_una_sucursal_no_autorizada(
        string scenario
    )
    {
        var c = await StartAsync();
        var advance =
            scenario == "otherCompany"
                ? await AddAdvanceInOtherCompanyAsync(c.S, Guid.NewGuid(), amount: 50m)
                : await CreateAdvanceAsync(c, 50m, cash: false);
        if (scenario != "otherCompany")
        {
            c.Client.DefaultRequestHeaders.Remove("X-Branch-Id");
            if (scenario == "unauthorizedBranch")
                c.Client.DefaultRequestHeaders.Add(
                    "X-Branch-Id",
                    (await AddBranchAsync(authorizedUser: null)).ToString()
                );
        }
        var expected =
            scenario == "otherCompany" ? HttpStatusCode.NotFound : HttpStatusCode.Forbidden;
        var before = await SnapshotAsync();

        Response[] responses =
        [
            await RefundAsync(c, advance, Refund(c, 1m, cash: false)),
            await ReverseRefundAsync(c, advance, Guid.NewGuid(), Evidence(Today)),
            await ReverseIncomeAsync(c, advance, Evidence(Today)),
            await CancelAsync(c, advance, Cancellation()),
        ];

        responses.Should().OnlyContain(r => r.Status == expected);
        (await SnapshotAsync()).Should().Be(before);
    }

    // ── Fecha de la evidencia (ADR-052 §17) ──────────────────────────────

    [Theory]
    [InlineData("reverseRefund", false)]
    [InlineData("reverseRefund", true)]
    [InlineData("reverseIncome", false)]
    [InlineData("reverseIncome", true)]
    public async Task La_evidencia_va_de_la_operacion_original_al_dia_de_negocio(
        string operation,
        bool evidenceToday
    )
    {
        var c = await StartAsync();
        await EnsurePeriodAsync(Today.AddDays(-3));
        var income = operation == "reverseIncome";
        var advance = await CreateAdvanceAsync(c, 50m, cash: income, Today.AddDays(-3));
        var originalDate = Today.AddDays(income ? -3 : -2);
        Guid transaction = default;
        if (!income)
        {
            var refund = Refund(c, 20m, cash: true) with { EffectiveDate = originalDate };
            (await RefundAsync(c, advance, refund)).Status.Should().Be(HttpStatusCode.OK);
            transaction = (await TransactionsAsync(advance)).Single().Id;
        }
        var key = Guid.NewGuid();
        Task<Response> ReverseOn(DateOnly evidenceDate)
        {
            var body = Evidence(evidenceDate) with { ClientRequestId = key };
            return income
                ? ReverseIncomeAsync(c, advance, body)
                : ReverseRefundAsync(c, advance, transaction, body);
        }
        var before = await SnapshotAsync();
        var cashBefore = await CashBalanceAsync(c.Op.CashSessionId);

        var future = await ReverseOn(Today.AddDays(1));
        var early = await ReverseOn(originalDate.AddDays(-1));

        foreach (var rejected in new[] { future, early })
        {
            rejected.Status.Should().Be(HttpStatusCode.UnprocessableEntity, rejected.Body);
            rejected.Code.Should().Be(DomainRule);
        }
        future.Body.Should().Contain("es posterior al d");
        early.Body.Should().Contain("es anterior a la operaci");
        (await SnapshotAsync()).Should().Be(before);
        (await CashBalanceAsync(c.Op.CashSessionId)).Should().Be(cashBefore);

        var accepted = evidenceToday ? Today : originalDate;
        var response = await ReverseOn(accepted);

        response.Status.Should().Be(HttpStatusCode.OK, response.Body);
        var reversal = (await MovementsAsync(advance)).Last();
        reversal.EvidenceDate.Should().Be(accepted);
        reversal.EffectiveDate.Should().Be(Today, "la fecha contable es la de la reversa (TA-07)");
        await AssertBalancesAsync(
            c,
            advance,
            income ? 0m : 50m,
            income ? CustomerAdvanceStatus.Cancelled : CustomerAdvanceStatus.Confirmed
        );

        // La operación original pasa a ser posterior a la evidencia: el reintento no se reevalúa.
        var later = Today.AddDays(1);
        await DbAsync(async db =>
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE customer_advance_movements SET effective_date = {later} WHERE customer_advance_id = {advance} AND movement_type IN (1, 4)"
            );
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE customer_advance_refund_transactions SET effective_date = {later} WHERE customer_advance_id = {advance}"
            );
        });
        var afterAccepted = await SnapshotAsync();
        var replay = await ReverseOn(accepted);
        replay.Status.Should().Be(HttpStatusCode.OK, replay.Body);
        (await SnapshotAsync()).Should().Be(afterAccepted);
    }

    // ── Destinos históricos ──────────────────────────────────────────────

    [Theory]
    [InlineData("reverseRefund")]
    [InlineData("reverseIncome")]
    public async Task La_reversa_conserva_destino_y_cuentas_historicas_aunque_el_banco_cambie(
        string operation
    )
    {
        var c = await StartAsync();
        var income = operation == "reverseIncome";
        var bank = await AddBankAccountAsync(c.S);
        var registration = Transfer(c.S, 50m) with { CompanyBankAccountId = bank };
        var created = await PostAsync(c.Client, registration);
        created.Status.Should().Be(HttpStatusCode.Created, created.Body);
        var advance = created.Id;
        if (!income)
            (
                await RefundAsync(
                    c,
                    advance,
                    Refund(c, 20m, cash: false) with
                    {
                        CompanyBankAccountId = bank,
                    }
                )
            )
                .Status.Should()
                .Be(HttpStatusCode.OK);
        var original = (await MovementsAsync(advance)).Last();
        var replacement = await AddAccountAsync(_f.CompanyId, c.S.UserId);
        await DbAsync(async db =>
        {
            var account = await db.Set<CompanyBankAccount>()
                .IgnoreQueryFilters()
                .SingleAsync(b => b.Id == bank);
            db.Entry(account).Property(b => b.IsActive).CurrentValue = false;
            db.Entry(account).Property(b => b.AccountingAccountId).CurrentValue = replacement;
        });
        var cashBefore = await CashBalanceAsync(c.Op.CashSessionId);
        var transaction = income ? default : (await TransactionsAsync(advance)).Single().Id;
        // El contrato no admite destino: lo que se envíe de más se ignora.
        var url = income
            ? $"{Url}/{advance}/reverse-income"
            : $"{Url}/{advance}/refunds/{transaction}/reverse";

        var response = await SendAsync(
            c,
            url,
            new
            {
                reason = "Reverso bancario",
                reference = "EXTRACTO-2",
                evidenceDate = Today,
                clientRequestId = Guid.NewGuid(),
                companyBankAccountId = c.S.BankAccount,
                cashRegisterId = c.Op.CashRegisterId,
            }
        );

        response.Status.Should().Be(HttpStatusCode.OK, response.Body);
        var effects = await MovementEffectsAsync(original.Id);
        effects.Entries.Should().Be(1);
        effects.MirrorLines.Should().HaveCount(2);
        effects
            .MirrorLines.Should()
            .BeEquivalentTo(effects.Lines.Select(l => (l.Account, l.Credit, l.Debit)));
        effects
            .MirrorLines.Select(l => l.Account)
            .Should()
            .Contain(c.S.BankAccountingAccount)
            .And.NotContain(replacement);
        (await CashBalanceAsync(c.Op.CashSessionId)).Should().Be(cashBefore);
        if (!income)
            (await TransactionsAsync(advance))
                .Should()
                .OnlyContain(t => t.CompanyBankAccountId == bank && t.CashRegisterId == null);
    }

    // ── Aislamiento entre tenants ────────────────────────────────────────

    [Fact]
    public async Task Ninguna_operacion_alcanza_un_anticipo_de_otro_tenant()
    {
        var c = await StartAsync();
        var own = await CreateAdvanceAsync(c, 50m, cash: false);
        var foreign = await AddAdvanceInOtherTenantAsync(c.S);
        var before = await SnapshotAsync();
        var cashBefore = await CashBalanceAsync(c.Op.CashSessionId);

        Response[] responses =
        [
            await RefundAsync(c, foreign.Advance, Refund(c, 1m, cash: false)),
            await RefundAsync(c, foreign.Advance, Refund(c, 1m, cash: true)),
            await ReverseRefundAsync(c, foreign.Advance, foreign.Transaction, Evidence(Today)),
            await ReverseRefundAsync(c, own, foreign.Transaction, Evidence(Today)),
            await ReverseIncomeAsync(c, foreign.Advance, Evidence(Today)),
            await CancelAsync(c, foreign.Advance, Cancellation()),
        ];

        responses
            .Should()
            .OnlyContain(
                r => r.Status == HttpStatusCode.NotFound,
                string.Join('\n', responses.Select(r => r.Body))
            );
        foreach (var response in responses)
            response
                .Body.Should()
                .NotContain(foreign.Advance.ToString())
                .And.NotContain(foreign.Transaction.ToString())
                .And.NotContain(":777")
                .And.NotContain("AJENA");
        (await SnapshotAsync()).Should().Be(before);
        (await CashBalanceAsync(c.Op.CashSessionId)).Should().Be(cashBefore);
        await AssertBalancesAsync(c, own, 50m, CustomerAdvanceStatus.Confirmed);
    }

    /// <summary>Anticipo con una devolución, persistido en otro tenant (sin eventos).</summary>
    private async Task<(Guid Advance, Guid Transaction)> AddAdvanceInOtherTenantAsync(Seeded s)
    {
        var (customer, bankAccount) = await AddForeignTenantAsync(s);
        var (tenant, company) = await QueryAsync(async db =>
        {
            var account = await db.Set<CompanyBankAccount>()
                .IgnoreQueryFilters()
                .SingleAsync(b => b.Id == bankAccount);
            return (account.TenantId, account.CompanyId);
        });
        var branch = Branch.Create(
            tenant,
            "Ajena",
            "Av. 4",
            "T001",
            description: null,
            reference: null,
            postalCode: null,
            phone: null,
            secondaryPhone: null,
            email: null,
            website: null,
            managerName: null,
            managerPosition: null,
            managerEmail: null,
            managerPhone: null,
            countryId: null,
            provinceId: null,
            cantonId: null,
            parishId: null,
            latitude: null,
            longitude: null,
            openingDate: null,
            internalNotes: null,
            isMainBranch: true,
            createdBy: s.UserId,
            companyId: company
        );
        var method = PaymentMethod.Create(
            tenant,
            "TRANSF",
            "Transferencia",
            false,
            false,
            1,
            s.UserId,
            PaymentMethodDetailType.Transfer
        );
        await DbAsync(db =>
        {
            db.Branches.Add(branch);
            db.PaymentMethods.Add(method);
            return Task.CompletedTask;
        });
        var advance = CustomerAdvance.Register(
            tenant,
            company,
            branch.Id,
            customer,
            "USD",
            777m,
            Today,
            method.Id,
            bankAccount,
            null,
            s.UserId,
            new ERP.Domain.Common.ClientRequestKey(Guid.NewGuid(), new string('C', 64))
        );
        var movement = advance.RegisterRefund(
            77m,
            new CustomerAdvanceCompensation("Devolución ajena", "AJENA", Today),
            branch.Id,
            s.UserId,
            new ERP.Domain.Common.ClientRequestKey(Guid.NewGuid(), new string('D', 64))
        );
        var transaction = CustomerAdvanceRefundTransaction.CreateRefund(
            advance,
            movement,
            method.Id,
            bankAccount,
            null
        );
        advance.ClearDomainEvents();
        await DbAsync(db =>
        {
            db.CustomerAdvances.Add(advance);
            db.CustomerAdvanceRefundTransactions.Add(transaction);
            return Task.CompletedTask;
        });
        return (advance.Id, transaction.Id);
    }

    // ── Operaciones ──────────────────────────────────────────────────────

    private sealed record RefundBody(
        decimal Amount,
        DateOnly EffectiveDate,
        Guid PaymentMethodId,
        Guid? CompanyBankAccountId,
        Guid? CashRegisterId,
        string Reference,
        string Reason,
        Guid ClientRequestId
    );

    private sealed record EvidenceBody(
        string Reason,
        string Reference,
        DateOnly EvidenceDate,
        Guid ClientRequestId
    );

    private sealed record CancelBody(
        string Reason,
        string Reference,
        bool MoneyNotReceived,
        Guid ClientRequestId
    );

    private RefundBody Refund(Ctx c, decimal amount, bool cash) =>
        new(
            amount,
            Today,
            cash ? _f.CashMethodId : c.S.TransferMethod,
            cash ? null : c.S.BankAccount,
            cash ? c.Op.CashRegisterId : null,
            "DEV-1",
            "Devolución al cliente",
            Guid.NewGuid()
        );

    private static EvidenceBody Evidence(DateOnly date) =>
        new("El dinero se deshizo", "EXTRACTO-1", date, Guid.NewGuid());

    private static CancelBody Cancellation() =>
        new("La transferencia no se acreditó", "BANCO-1", true, Guid.NewGuid());

    private static async Task<Response> SendAsync(Ctx c, string url, object body) =>
        await ReadAsync(await c.Client.PostAsJsonAsync(url, body));

    private static Task<Response> RefundAsync(Ctx c, Guid advance, RefundBody body) =>
        SendAsync(c, $"{Url}/{advance}/refunds", body);

    private static Task<Response> ReverseRefundAsync(
        Ctx c,
        Guid advance,
        Guid transaction,
        EvidenceBody body
    ) => SendAsync(c, $"{Url}/{advance}/refunds/{transaction}/reverse", body);

    private static Task<Response> ReverseIncomeAsync(Ctx c, Guid advance, EvidenceBody body) =>
        SendAsync(c, $"{Url}/{advance}/reverse-income", body);

    private static Task<Response> CancelAsync(Ctx c, Guid advance, CancelBody body) =>
        SendAsync(c, $"{Url}/{advance}/cancel", body);

    private Task<Response> ApplyAsync(
        Ctx c,
        Guid advance,
        Guid receivable,
        decimal amount,
        Guid key
    ) =>
        SendAsync(
            c,
            $"{Url}/{advance}/applications",
            new
            {
                effectiveDate = Today,
                lines = new[] { new { receivableId = receivable, amount } },
                clientRequestId = key,
            }
        );

    private async Task<Guid> ApplyToNewReceivableAsync(Ctx c, Guid advance, decimal amount)
    {
        var key = Guid.NewGuid();
        var response = await ApplyAsync(
            c,
            advance,
            await CreateDatedReceivableAsync(amount),
            amount,
            key
        );
        response.Status.Should().Be(HttpStatusCode.OK, response.Body);
        return key;
    }

    private static Task<Response> ReverseApplicationAsync(Ctx c, Guid advance, Guid operation) =>
        SendAsync(
            c,
            $"{Url}/{advance}/applications/{operation}/reverse",
            new
            {
                reason = "Error",
                reference = "ACTA",
                clientRequestId = Guid.NewGuid(),
            }
        );

    /// <summary>CxC de factura emitida el día de negocio de la empresa.</summary>
    private async Task<Guid> CreateDatedReceivableAsync(decimal amount)
    {
        var id = await _f.CreateReceivableAsync(amount, installments: 1);
        await DbAsync(async db =>
        {
            var invoiceId = await db
                .SalesReceivables.IgnoreQueryFilters()
                .Where(r => r.Id == id)
                .Select(r => r.InvoiceId)
                .SingleAsync();
            var invoice = await db
                .SalesInvoices.IgnoreQueryFilters()
                .SingleAsync(i => i.Id == invoiceId);
            db.Entry(invoice).Property(i => i.IssueDate).CurrentValue = Today;
        });
        return id;
    }

    // ── Escenarios ───────────────────────────────────────────────────────

    private async Task<Ctx> StartAsync(decimal openingCash = 1000m)
    {
        var s = await SeedAsync();
        var op = await _f.CreateOperatorAsync(openingCash);
        return new Ctx(s, op, _f.CreateClient(op.UserId));
    }

    private async Task<Guid> CreateAdvanceAsync(
        Ctx c,
        decimal amount,
        bool cash,
        DateOnly? date = null
    )
    {
        var body = cash ? Cash(c.Op, amount) : Transfer(c.S, amount);
        var response = await PostAsync(c.Client, body with { AdvanceDate = date ?? Today });
        response.Status.Should().Be(HttpStatusCode.Created, response.Body);
        return response.Id;
    }

    private Task CloseSessionAsync(Guid sessionId) =>
        DbAsync(async db =>
        {
            var session = await db
                .CashSessions.IgnoreQueryFilters()
                .SingleAsync(x => x.Id == sessionId);
            db.Entry(session).Property(x => x.Status).CurrentValue = CashSessionStatus.Closed;
        });

    /// <summary>Período abierto cuando una fecha pasada cae en el año anterior.</summary>
    private async Task EnsurePeriodAsync(DateOnly date)
    {
        var userId = (await SeedAsync()).UserId;
        await DbAsync(async db =>
        {
            if (
                await db
                    .AccountingPeriods.IgnoreQueryFilters()
                    .AnyAsync(p => p.CompanyId == _f.CompanyId && p.FiscalYear == date.Year)
            )
                return;
            db.AccountingPeriods.Add(
                ERP.Domain.Modules.Accounting.Entities.AccountingPeriod.Create(
                    _f.TenantId,
                    _f.CompanyId,
                    date.Year,
                    1,
                    new DateOnly(date.Year, 1, 1),
                    new DateOnly(date.Year, 12, 31),
                    userId
                )
            );
        });
    }

    /// <summary>Transferencia cuya obligación queda Failed: el registro sin asiento publicado.</summary>
    private async Task<Guid> CreateUncountedAdvanceAsync(Ctx c, decimal amount)
    {
        var inactive = await AddAccountAsync(_f.CompanyId, c.S.UserId, active: false);
        var response = await PostAsync(
            c.Client,
            Transfer(c.S, amount) with
            {
                CompanyBankAccountId = await AddBankAccountAsync(c.S, accountId: inactive),
            }
        );
        response.Status.Should().Be(HttpStatusCode.Created, response.Body);
        return response.Id;
    }

    /// <summary>Anticipo listo para la operación pedida y la intención a enviar con la clave dada.</summary>
    private async Task<(Guid Advance, Func<Task<Response>> Send)> PrepareAsync(
        Ctx c,
        string operation,
        Guid key
    )
    {
        var advance =
            operation == "cancel"
                ? await CreateUncountedAdvanceAsync(c, 30m)
                : await CreateAdvanceAsync(c, 30m, cash: operation == "reverseIncome");
        switch (operation)
        {
            case "refund":
                var refund = Refund(c, 10m, cash: true) with { ClientRequestId = key };
                return (advance, () => RefundAsync(c, advance, refund));
            case "reverseRefund":
                await RefundAsync(c, advance, Refund(c, 10m, cash: true));
                var transaction = (await TransactionsAsync(advance)).Single().Id;
                var reversal = Evidence(Today) with { ClientRequestId = key };
                return (advance, () => ReverseRefundAsync(c, advance, transaction, reversal));
            case "reverseIncome":
                var income = Evidence(Today) with { ClientRequestId = key };
                return (advance, () => ReverseIncomeAsync(c, advance, income));
            default:
                var cancel = Cancellation() with { ClientRequestId = key };
                return (advance, () => CancelAsync(c, advance, cancel));
        }
    }

    private async Task<Response> WithClosedPeriodAsync(Func<Task<Response>> send)
    {
        await SetCurrentPeriodAsync(PeriodStatus.Closed);
        try
        {
            return await send();
        }
        finally
        {
            await SetCurrentPeriodAsync(PeriodStatus.Open);
        }
    }

    private Task SetCurrentPeriodAsync(PeriodStatus status) =>
        DbAsync(async db =>
        {
            var today = Today;
            var period = await db
                .AccountingPeriods.IgnoreQueryFilters()
                .SingleAsync(p =>
                    p.CompanyId == _f.CompanyId && p.StartDate <= today && p.EndDate >= today
                );
            db.Entry(period).Property(p => p.Status).CurrentValue = status;
        });

    // ── Lecturas y comprobaciones ────────────────────────────────────────

    /// <summary>
    /// El disponible coincide con el libro y, cuando todo está contabilizado, con el saldo acreedor
    /// de la cuenta de anticipos formado por sus asientos y espejos.
    /// </summary>
    private async Task AssertBalancesAsync(
        Ctx c,
        Guid advance,
        decimal available,
        CustomerAdvanceStatus status
    )
    {
        var (persisted, movements) = await QueryAsync(async db =>
            (
                await db
                    .CustomerAdvances.IgnoreQueryFilters()
                    .AsNoTracking()
                    .SingleAsync(a => a.Id == advance),
                await db
                    .CustomerAdvanceMovements.IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(m => m.CustomerAdvanceId == advance)
                    .ToListAsync()
            )
        );
        persisted.AvailableAmount.Should().Be(available);
        persisted.Status.Should().Be(status);
        var ids = movements.Select(m => m.Id).ToList();
        var (unresolved, net) = await QueryAsync(async db =>
        {
            var unresolved = await db
                .AccountingPostings.IgnoreQueryFilters()
                .CountAsync(p =>
                    ids.Contains(p.SourceEventId)
                    && (
                        p.Status == AccountingPostingStatus.Failed
                        || p.Status == AccountingPostingStatus.Canceled
                    )
                );
            var entries = await db
                .JournalEntries.IgnoreQueryFilters()
                .AsNoTracking()
                .Include(j => j.Lines)
                .Where(j => ids.Contains(j.SourceEventId))
                .ToListAsync();
            var originals = entries.Select(e => (Guid?)e.Id).ToList();
            var mirrors = await db
                .JournalEntries.IgnoreQueryFilters()
                .AsNoTracking()
                .Include(j => j.Lines)
                .Where(j => originals.Contains(j.OriginalJournalEntryId))
                .ToListAsync();
            var net = entries
                .Concat(mirrors)
                .SelectMany(e => e.Lines)
                .Where(l => l.AccountId == c.S.AdvancesAccount)
                .Sum(l => l.Credit - l.Debit);
            return (unresolved, net);
        });
        if (unresolved == 0)
            net.Should().Be(available, "la cuenta de anticipos respalda el saldo disponible");
    }

    private sealed record Snapshot(
        int Advances,
        int Movements,
        int Audits,
        int CashMovements,
        int Postings,
        int Entries,
        int Outbox,
        int Transactions,
        decimal Available,
        int ResolvedOrCanceled
    );

    private async Task<Snapshot> SnapshotAsync()
    {
        var totals = await TotalsAsync();
        return await QueryAsync(async db => new Snapshot(
            totals.Advances,
            totals.Movements,
            totals.Audits,
            totals.CashMovements,
            totals.Postings,
            totals.Entries,
            totals.Outbox,
            await db.CustomerAdvanceRefundTransactions.IgnoreQueryFilters().CountAsync(),
            await db.CustomerAdvances.IgnoreQueryFilters().SumAsync(a => a.AvailableAmount),
            await db
                .AccountingPostings.IgnoreQueryFilters()
                .CountAsync(p => p.Status == AccountingPostingStatus.Canceled)
        ));
    }

    private Task<List<CustomerAdvanceRefundTransaction>> TransactionsAsync(Guid advance) =>
        QueryAsync(db =>
            db.CustomerAdvanceRefundTransactions.IgnoreQueryFilters()
                .AsNoTracking()
                .Where(t => t.CustomerAdvanceId == advance)
                .OrderBy(t => t.CreatedAtUtc)
                .ToListAsync()
        );

    private Task<List<CustomerAdvanceMovement>> MovementsAsync(Guid advance) =>
        QueryAsync(db =>
            db.CustomerAdvanceMovements.IgnoreQueryFilters()
                .AsNoTracking()
                .Where(m => m.CustomerAdvanceId == advance)
                .OrderBy(m => m.CreatedAtUtc)
                .ToListAsync()
        );

    private Task<CustomerAdvanceMovement> MovementAsync(Guid id) =>
        QueryAsync(db =>
            db.CustomerAdvanceMovements.IgnoreQueryFilters()
                .AsNoTracking()
                .SingleAsync(m => m.Id == id)
        );

    private Task<List<ERP.Domain.Modules.Caja.Entities.CashMovement>> CashMovementsAsync(
        Guid referenceId
    ) =>
        QueryAsync(db =>
            db.CashMovements.IgnoreQueryFilters()
                .AsNoTracking()
                .Where(m => m.ReferenceId == referenceId)
                .ToListAsync()
        );

    private Task<decimal> CashBalanceAsync(Guid sessionId) =>
        QueryAsync(async db =>
            (
                await db
                    .CashSessions.IgnoreQueryFilters()
                    .AsNoTracking()
                    .Include(s => s.Movements)
                    .SingleAsync(s => s.Id == sessionId)
            ).CurrentBalance
        );

    private Task<int> CajaPostingsAsync() =>
        QueryAsync(db =>
            db.AccountingPostings.IgnoreQueryFilters().CountAsync(p => p.SourceModule == "Caja")
        );

    private Task<List<string>> AuditsAsync(Guid advance) =>
        QueryAsync(db =>
            db.CustomerAdvanceAudits.IgnoreQueryFilters()
                .Where(a => a.EntityId == advance)
                .Select(a => a.Action)
                .ToListAsync()
        );

    private sealed record MovementEffects(
        List<(string Fact, AccountingPostingStatus Status)> Postings,
        int Entries,
        List<(Guid Account, decimal Debit, decimal Credit)> Lines,
        List<(Guid Account, decimal Debit, decimal Credit)> MirrorLines
    );

    private Task<MovementEffects> MovementEffectsAsync(Guid movementId) =>
        QueryAsync(async db =>
        {
            var postings = await db
                .AccountingPostings.IgnoreQueryFilters()
                .Where(p => p.SourceEventId == movementId)
                .Select(p => new { p.FactType, p.Status })
                .ToListAsync();
            var entries = await db
                .JournalEntries.IgnoreQueryFilters()
                .AsNoTracking()
                .Include(j => j.Lines)
                .Where(j => j.SourceEventId == movementId)
                .ToListAsync();
            var ids = entries.Select(e => (Guid?)e.Id).ToList();
            var mirrors = await db
                .JournalEntries.IgnoreQueryFilters()
                .AsNoTracking()
                .Include(j => j.Lines)
                .Where(j => ids.Contains(j.OriginalJournalEntryId))
                .ToListAsync();
            return new MovementEffects(
                postings.Select(p => (p.FactType, p.Status)).ToList(),
                entries.Count,
                entries
                    .SelectMany(e => e.Lines)
                    .Select(l => (l.AccountId, l.Debit, l.Credit))
                    .ToList(),
                mirrors
                    .SelectMany(e => e.Lines)
                    .Select(l => (l.AccountId, l.Debit, l.Credit))
                    .ToList()
            );
        });
}
