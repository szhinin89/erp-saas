using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Continuity;
using ERP.Application.Modules.Accounting.UseCases.AccountingPeriods;
using ERP.Application.Modules.Accounting.UseCases.JournalEntries;
using ERP.Application.Modules.Accounting.UseCases.Reports;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Infrastructure.Accounting.Repositories;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Tests.TestData;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace ERP.Infrastructure.Tests.Accounting;

/// <summary>
/// ACCOUNTING-12D — cierre anual contable: cuatro casos focales sobre PostgreSQL 16 migrado, con el
/// mismo libro aislado por Company de ACCOUNTING-08 (período anual 2026, Accounting Enabled).
/// </summary>
public sealed class AccountingFiscalYearClosing12DTests : IClassFixture<AccountingManual08Fixture>
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly DateOnly DuringYear = new(2026, 10, 17);
    private static readonly DateOnly AfterYear = new(2027, 1, 5);
    private static readonly DateOnly YearEnd = new(2026, 12, 31);
    private readonly AccountingManual08Fixture _fixture;

    public AccountingFiscalYearClosing12DTests(AccountingManual08Fixture fixture) => _fixture = fixture;

    [Fact]
    public async Task A12D_01_utilidad_genera_draft_balanceado_con_resultado_al_haber()
    {
        var (book, result, cost, expense) = await ProfitBookAsync();

        await using var db = book.Db();
        var generated = await book.GenerateClosing(db, AfterYear).Handle(new GenerateFiscalYearClosingEntryCommand(2026), Ct);

        generated.IsSuccess.Should().BeTrue(generated.Error);
        generated.Value!.Status.Should().Be("Draft");
        generated.Value.Result.Should().Be(449.75m);
        await using var verify = book.Db();
        var entry = await verify.JournalEntries.Include(e => e.Lines).SingleAsync(e => e.Id == generated.Value.JournalEntryId);
        entry.IsFiscalYearClosing.Should().BeTrue();
        entry.EntryDate.Should().Be(YearEnd);
        entry.AccountingPeriodId.Should().Be(book.PeriodId);
        entry.EntryNumber.Should().BeNull();
        entry.Lines.Select(l => (l.AccountId, l.Debit, l.Credit)).Should().BeEquivalentTo(new[]
        {
            (book.CreditId, 1000m, 0m),
            (cost, 0m, 400m),
            (expense, 0m, 150.25m),
            (result, 0m, 449.75m),
        });
        entry.Lines.Sum(l => l.Debit).Should().Be(entry.Lines.Sum(l => l.Credit));
        (await verify.JournalEntryAudits.CountAsync(a => a.Action == "Created" && a.EntityId == entry.Id)).Should().Be(1);
    }

    [Fact]
    public async Task A12D_02_perdida_y_cuentas_correctoras_invierten_el_sentido_y_se_publica()
    {
        var book = await _fixture.BookAsync();
        var result = await AccountAsync(book, "3.1.03.001", "Resultado del ejercicio", AccountType.Equity, AccountNature.Credit);
        var salesReturns = await AccountAsync(book, "4.1.03", "Devoluciones en ventas", AccountType.Income, AccountNature.Debit);
        var cost = await AccountAsync(book, "5.1.01", "Costo de ventas", AccountType.Cost, AccountNature.Debit);
        var purchaseDiscounts = await AccountAsync(book, "5.1.09", "Descuentos en compras", AccountType.Cost, AccountNature.Credit);
        await PostAsync(book, new(2026, 3, 1), book.DebitId, book.CreditId, 300m);
        await PostAsync(book, new(2026, 4, 1), salesReturns, book.DebitId, 50m);
        await PostAsync(book, new(2026, 5, 1), cost, book.DebitId, 600m);
        await PostAsync(book, new(2026, 6, 1), book.DebitId, purchaseDiscounts, 20m);

        await using var db = book.Db();
        var generated = await book.GenerateClosing(db, AfterYear).Handle(new GenerateFiscalYearClosingEntryCommand(2026), Ct);

        generated.IsSuccess.Should().BeTrue(generated.Error);
        generated.Value!.Result.Should().Be(-330m, "ingresos 300 − 50, costos 600 − 20");
        var draft = await db.JournalEntries.AsNoTracking().Include(e => e.Lines).SingleAsync(e => e.Id == generated.Value.JournalEntryId);
        draft.Lines.Select(l => (l.AccountId, l.Debit, l.Credit)).Should().BeEquivalentTo(new[]
        {
            (book.CreditId, 300m, 0m),
            (salesReturns, 0m, 50m),
            (cost, 0m, 600m),
            (purchaseDiscounts, 20m, 0m),
            (result, 330m, 0m),
        });

        var posted = await book.Handler(db, AfterYear).Handle(new PostManualJournalCommand(draft.Id, draft.Version, Guid.NewGuid()), Ct);
        posted.IsSuccess.Should().BeTrue(posted.Error);
        posted.Value!.Status.Should().Be("Posted");
        posted.Value.EntryNumber.Should().NotBeNull();
        var totals = await new JournalEntryRepository(db).GetAccountLineTotalsAsync(book.TenantId, book.CompanyId,
            new(2026, 1, 1), YearEnd, [book.CreditId, salesReturns, cost, purchaseDiscounts], Ct);
        totals.Values.Should().OnlyContain(t => t.TotalDebit == t.TotalCredit, "el cierre deja las nominales en cero");
    }

    [Fact]
    public async Task A12D_03_duplicado_desactualizado_periodos_abiertos_y_obligaciones_pendientes_bloquean()
    {
        // Duplicado + Draft desactualizado: nunca dos cierres vigentes ni un cierre con saldos viejos.
        var (book, _, _, _) = await ProfitBookAsync();
        await using (var db = book.Db())
        {
            var first = await book.GenerateClosing(db, AfterYear).Handle(new GenerateFiscalYearClosingEntryCommand(2026), Ct);
            first.IsSuccess.Should().BeTrue(first.Error);
            var again = await book.GenerateClosing(db, AfterYear).Handle(new GenerateFiscalYearClosingEntryCommand(2026), Ct);
            again.Code.Should().Be(FiscalYearClosingCodes.DraftExists);

            await PostAsync(book, new(2026, 11, 1), book.DebitId, book.CreditId, 10m, AfterYear);
            var handler = book.Handler(db, AfterYear);
            var stale = await handler.Handle(new PostManualJournalCommand(first.Value!.JournalEntryId!.Value, 1, Guid.NewGuid()), Ct);
            stale.Code.Should().Be(FiscalYearClosingCodes.Outdated);
            (await handler.Handle(new EditManualJournalCommand(first.Value.JournalEntryId.Value, 1, YearEnd, "x", null, []), Ct))
                .Code.Should().Be(FiscalYearClosingCodes.NotEditable);
            (await handler.Handle(new DiscardManualJournalCommand(first.Value.JournalEntryId.Value, 1, "saldos cambiaron"), Ct))
                .IsSuccess.Should().BeTrue();

            var regenerated = await book.GenerateClosing(db, AfterYear).Handle(new GenerateFiscalYearClosingEntryCommand(2026), Ct);
            regenerated.Value!.Result.Should().Be(459.75m);
            (await handler.Handle(new PostManualJournalCommand(regenerated.Value.JournalEntryId!.Value, 1, Guid.NewGuid()), Ct))
                .IsSuccess.Should().BeTrue();
            var closed = await book.GenerateClosing(db, AfterYear).Handle(new GenerateFiscalYearClosingEntryCommand(2026), Ct);
            closed.Code.Should().Be(FiscalYearClosingCodes.AlreadyPosted);
            (await db.JournalEntries.CountAsync(e => e.SourceEventType == JournalEntry.FiscalYearClosingFact
                && e.Status == JournalEntryStatus.Posted)).Should().Be(1);
        }

        // Obligación contable pendiente dentro del ejercicio.
        var pendingBook = await _fixture.BookAsync();
        await AccountAsync(pendingBook, "3.1.03.001", "Resultado del ejercicio", AccountType.Equity, AccountNature.Credit);
        await PostAsync(pendingBook, new(2026, 2, 1), pendingBook.DebitId, pendingBook.CreditId, 100m);
        await using (var db = pendingBook.Db())
        {
            db.AccountingPostings.Add(AccountingPosting.CreatePending(pendingBook.TenantId, pendingBook.CompanyId, "Sales",
                "InvoiceIssued", Guid.NewGuid(), "001-001-000000001", new(2026, 11, 3), new(2026, 11, 3), 2, "{}",
                pendingBook.UserId, null));
            await db.SaveChangesAsync();
            var blocked = await pendingBook.GenerateClosing(db, AfterYear).Handle(new GenerateFiscalYearClosingEntryCommand(2026), Ct);
            blocked.Code.Should().Be(FiscalYearClosingCodes.AccountingIncomplete);
            blocked.Error.Should().Contain(AccountingCompletenessCodes.PostingsUnresolved);
            (await db.JournalEntries.CountAsync(e => e.SourceEventType == JournalEntry.FiscalYearClosingFact)).Should().Be(0);
        }

        // Ejercicio mensual con períodos anteriores abiertos.
        var monthlyBook = await _fixture.BookAsync();
        await using (var db = monthlyBook.Db())
        {
            for (var month = 1; month <= 12; month++)
                db.AccountingPeriods.Add(AccountingPeriod.Create(monthlyBook.TenantId, monthlyBook.CompanyId, 2027, month,
                    new(2027, month, 1), new DateOnly(2027, month, 1).AddMonths(1).AddDays(-1), monthlyBook.UserId));
            await db.SaveChangesAsync();
            var blocked = await monthlyBook.GenerateClosing(db, new(2028, 1, 10)).Handle(new GenerateFiscalYearClosingEntryCommand(2027), Ct);
            blocked.Code.Should().Be(FiscalYearClosingCodes.PreviousPeriodsOpen);
        }
    }

    [Fact]
    public async Task A12D_04_ultimo_periodo_exige_cierre_Posted_y_reportes_no_duplican_resultado()
    {
        var (book, result, _, _) = await ProfitBookAsync();
        await using var db = book.Db();
        var completeness = AccountingCompletenessTestSupport.Create(db, book.CompanyId);
        var tenant = Mock.Of<ICurrentTenant>(t => t.TenantId == book.TenantId);
        var company = Mock.Of<ICurrentCompany>(c => c.CompanyId == book.CompanyId && c.HasCompanyContext && c.IsAuthenticated);
        var user = Mock.Of<ICurrentUser>(u => u.UserId == book.UserId);
        var clock = book.Clock(AfterYear);
        var readiness = new GetAccountingPeriodClosureReadinessHandler(new AccountingPeriodRepository(db), completeness, clock, tenant, company);
        CloseAccountingPeriodHandler Close() => new(new AccountingPeriodRepository(db), completeness,
            new AccountingOperationalPolicyRepository(db), new UnitOfWork(db), clock, tenant, company, user);

        var before = await readiness.Handle(new GetAccountingPeriodClosureReadinessQuery(book.PeriodId), Ct);
        before.Value!.CanClose.Should().BeFalse();
        before.Value.Completeness!.Reasons.Should().ContainSingle()
            .Which.Should().Be(new AccountingCompletenessReason(AccountingCompletenessCodes.YearEndClosingMissing, 1));
        await Close().Invoking(h => h.Handle(new CloseAccountingPeriodCommand(book.PeriodId), Ct))
            .Should().ThrowAsync<DomainRuleViolationException>().WithMessage("*cierre anual*");

        var generated = await book.GenerateClosing(db, AfterYear).Handle(new GenerateFiscalYearClosingEntryCommand(2026), Ct);
        var draftReadiness = await readiness.Handle(new GetAccountingPeriodClosureReadinessQuery(book.PeriodId), Ct);
        draftReadiness.Value!.Completeness!.Reasons.Select(r => r.Code).Should()
            .BeEquivalentTo(AccountingCompletenessCodes.JournalEntriesNotFinal, AccountingCompletenessCodes.YearEndClosingMissing);
        (await book.Handler(db, AfterYear).Handle(new PostManualJournalCommand(generated.Value!.JournalEntryId!.Value, 1, Guid.NewGuid()), Ct))
            .IsSuccess.Should().BeTrue();

        var after = await readiness.Handle(new GetAccountingPeriodClosureReadinessQuery(book.PeriodId), Ct);
        after.Value!.CanClose.Should().BeTrue(string.Join(",", after.Value.Completeness!.Reasons.Select(r => r.Code)));
        (await Close().Handle(new CloseAccountingPeriodCommand(book.PeriodId), Ct)).IsSuccess.Should().BeTrue();

        var journal = new JournalEntryRepository(db);
        var accounts = new AccountRepository(db);
        var income = await new GetIncomeStatementReportHandler(journal, accounts, tenant, company, completeness)
            .Handle(new GetIncomeStatementReportQuery(new(2026, 1, 1), YearEnd), Ct);
        income.Value!.NetProfit.Should().Be(449.75m, "el cierre no es un movimiento de resultados");
        var balance = await new GetBalanceSheetReportHandler(journal, accounts, tenant, company, completeness)
            .Handle(new GetBalanceSheetReportQuery(YearEnd), Ct);
        balance.Value!.CurrentYearResult.Should().Be(0m);
        balance.Value.EquityLines.Single(l => l.AccountId == result).Amount.Should().Be(449.75m);
        balance.Value.TotalEquity.Should().Be(449.75m, "el resultado se muestra una sola vez");
        balance.Value.IsBalanced.Should().BeTrue();
        var nextYear = await new GetBalanceSheetReportHandler(journal, accounts, tenant, company, completeness)
            .Handle(new GetBalanceSheetReportQuery(new(2027, 1, 31)), Ct);
        nextYear.Value!.PriorYearsUnclosedResult.Should().Be(0m);
        nextYear.Value.TotalEquity.Should().Be(449.75m);
    }

    /// <summary>
    /// ACCOUNTING-FINAL-FIX (BUG-1): el reverso del cierre anual solo procede con su fecha original (31-dic)
    /// y su último período Open. Reversar + regenerar no duplica el resultado, diciembre solo se cierra con
    /// el cierre vigente, y con el período Closed/Locked el reverso se rechaza sin escribir.
    /// </summary>
    [Fact]
    public async Task A12D_05_reverso_del_cierre_usa_su_fecha_y_regenerar_no_duplica_resultado()
    {
        var (book, result, _, _) = await ProfitBookAsync();
        Guid firstId;
        await using (var setup = book.Db())
        {
            setup.AccountingPeriods.Add(AccountingPeriod.Create(book.TenantId, book.CompanyId, 2027, 1,
                new(2027, 1, 1), new(2027, 1, 31), book.UserId));
            await setup.SaveChangesAsync();
            var first = await book.GenerateClosing(setup, AfterYear).Handle(new GenerateFiscalYearClosingEntryCommand(2026), Ct);
            (await book.Handler(setup, AfterYear).Handle(new PostManualJournalCommand(first.Value!.JournalEntryId!.Value, 1, Guid.NewGuid()), Ct))
                .IsSuccess.Should().BeTrue();
            firstId = first.Value.JournalEntryId.Value;
        }

        // Ajuste tardío de 2026: el cierre queda desactualizado y diciembre no puede cerrarse.
        await PostAsync(book, new(2026, 11, 1), book.DebitId, book.CreditId, 10m, AfterYear);
        await using var db = book.Db();
        var handler = book.Handler(db, AfterYear);
        var completeness = AccountingCompletenessTestSupport.Create(db, book.CompanyId);
        var period = await db.AccountingPeriods.AsNoTracking().SingleAsync(p => p.Id == book.PeriodId);
        (await completeness.EvaluateClosureAsync(book.TenantId, book.CompanyId, period, Ct)).Reasons
            .Select(r => r.Code).Should().Contain(AccountingCompletenessCodes.YearEndClosingMissing);

        // Fecha distinta del 31-dic (aunque enero 2027 esté Open): rechazo sin escribir.
        var wrongDate = await handler.Handle(new ReverseManualJournalCommand(firstId, AfterYear, "ajuste tardío"), Ct);
        wrongDate.Code.Should().Be(FiscalYearClosingCodes.ReversalDateInvalid);
        (await db.JournalEntries.CountAsync(e => e.OriginalJournalEntryId == firstId)).Should().Be(0);

        var reversed = await handler.Handle(new ReverseManualJournalCommand(firstId, YearEnd, "ajuste tardío"), Ct);
        reversed.IsSuccess.Should().BeTrue(reversed.Error);
        var reversal = await db.JournalEntries.AsNoTracking().SingleAsync(e => e.OriginalJournalEntryId == firstId);
        reversal.EntryDate.Should().Be(YearEnd);
        reversal.AccountingPeriodId.Should().Be(book.PeriodId);
        (await new JournalEntryRepository(db).FindReversalOfAsync(book.TenantId, book.CompanyId, firstId, Ct))!.Id
            .Should().Be(reversal.Id, "el detalle resuelve el reverso por OriginalJournalEntryId");
        (await handler.Handle(new ReverseManualJournalCommand(firstId, YearEnd, "repetido"), Ct)).Value!.Id
            .Should().Be(reversal.Id, "el reverso común es idempotente");
        (await db.JournalEntryAudits.CountAsync(a => a.Action == "Reversed" && a.EntityId == reversal.Id)).Should().Be(1);

        var regenerated = await book.GenerateClosing(db, AfterYear).Handle(new GenerateFiscalYearClosingEntryCommand(2026), Ct);
        regenerated.Value!.Result.Should().Be(459.75m, "el cierre anterior y su reverso se excluyen del cálculo");
        (await handler.Handle(new PostManualJournalCommand(regenerated.Value.JournalEntryId!.Value, 1, Guid.NewGuid()), Ct))
            .IsSuccess.Should().BeTrue();

        var journal = new JournalEntryRepository(db);
        var accounts = new AccountRepository(db);
        var tenant = Mock.Of<ICurrentTenant>(t => t.TenantId == book.TenantId);
        var company = Mock.Of<ICurrentCompany>(c => c.CompanyId == book.CompanyId && c.HasCompanyContext && c.IsAuthenticated);
        var user = Mock.Of<ICurrentUser>(u => u.UserId == book.UserId);
        (await new GetIncomeStatementReportHandler(journal, accounts, tenant, company, completeness)
            .Handle(new GetIncomeStatementReportQuery(new(2026, 1, 1), YearEnd), Ct)).Value!.NetProfit.Should().Be(459.75m);
        var balance = await new GetBalanceSheetReportHandler(journal, accounts, tenant, company, completeness)
            .Handle(new GetBalanceSheetReportQuery(new(2027, 1, 31)), Ct);
        balance.Value!.CurrentYearResult.Should().Be(0m, "el resultado de 2026 no se traslada a 2027");
        balance.Value.PriorYearsUnclosedResult.Should().Be(0m);
        balance.Value.EquityLines.Single(l => l.AccountId == result).Amount.Should().Be(459.75m);
        balance.Value.IsBalanced.Should().BeTrue();

        var periods = new AccountingPeriodRepository(db);
        var clock = book.Clock(AfterYear);
        (await new CloseAccountingPeriodHandler(periods, completeness, new AccountingOperationalPolicyRepository(db),
            new UnitOfWork(db), clock, tenant, company, user).Handle(new CloseAccountingPeriodCommand(book.PeriodId), Ct))
            .IsSuccess.Should().BeTrue();

        // Closed y luego Locked: el reverso del cierre vigente se rechaza sin escribir.
        var currentId = regenerated.Value.JournalEntryId.Value;
        (await handler.Handle(new ReverseManualJournalCommand(currentId, YearEnd, "tarde"), Ct)).Code.Should().Be("PERIOD_NOT_OPEN");
        (await new LockAccountingPeriodHandler(periods, tenant, company, user)
            .Handle(new LockAccountingPeriodCommand(book.PeriodId), Ct)).IsSuccess.Should().BeTrue();
        (await handler.Handle(new ReverseManualJournalCommand(currentId, YearEnd, "tarde"), Ct)).Code.Should().Be("PERIOD_NOT_OPEN");
        (await db.JournalEntries.CountAsync(e => e.OriginalJournalEntryId == currentId)).Should().Be(0);
    }

    /// <summary>Ventas 1000, costo 400, gasto 150.25 → utilidad 449.75.</summary>
    private async Task<(ManualBook Book, Guid Result, Guid Cost, Guid Expense)> ProfitBookAsync()
    {
        var book = await _fixture.BookAsync();
        var result = await AccountAsync(book, "3.1.03.001", "Resultado del ejercicio", AccountType.Equity, AccountNature.Credit);
        var cost = await AccountAsync(book, "5.1.01", "Costo de ventas", AccountType.Cost, AccountNature.Debit);
        var expense = await AccountAsync(book, "6.1.01", "Arriendo", AccountType.Expense, AccountNature.Debit);
        await PostAsync(book, new(2026, 3, 1), book.DebitId, book.CreditId, 1000m);
        await PostAsync(book, new(2026, 4, 1), cost, book.DebitId, 400m);
        await PostAsync(book, new(2026, 5, 1), expense, book.DebitId, 150.25m);
        return (book, result, cost, expense);
    }

    private static async Task<Guid> AccountAsync(ManualBook book, string code, string name, AccountType type, AccountNature nature)
    {
        await using var db = book.Db();
        var account = Account.Create(book.TenantId, book.CompanyId, AccountCode.Create(code), name, null, type, nature, true, book.UserId);
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        return account.Id;
    }

    private static async Task PostAsync(ManualBook book, DateOnly date, Guid debit, Guid credit, decimal amount, DateOnly? today = null)
    {
        await using var db = book.Db();
        var handler = book.Handler(db, today ?? DuringYear);
        var draft = await handler.Handle(new CreateManualJournalCommand(date, "movimiento", null,
            [new(debit, null, amount, 0m), new(credit, null, 0m, amount)], Guid.NewGuid()), Ct);
        draft.IsSuccess.Should().BeTrue(draft.Error);
        var posted = await handler.Handle(new PostManualJournalCommand(draft.Value!.Id, draft.Value.Version, Guid.NewGuid()), Ct);
        posted.IsSuccess.Should().BeTrue(posted.Error);
    }
}
