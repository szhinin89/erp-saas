using ERP.Application.Common.Services;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Enums;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.Integration;

/// <summary>
/// ACCOUNTING-12C — Caja contable contra PostgreSQL real y el pipeline HTTP completo: movimientos
/// manuales con la cuenta del motivo, motivo sin cuenta (Failed recuperable), depósito bancario y
/// diferencia de arqueo al cerrar. La caja de cada operador tiene su propia cuenta real
/// (<see cref="FinancialCommandIdempotencyFixture.CashAccountId"/>, no la Caja general sembrada).
/// </summary>
public sealed class CashAccounting12CTests : IClassFixture<FinancialCommandIdempotencyFixture>
{
    private readonly FinancialCommandIdempotencyFixture _f;

    public CashAccounting12CTests(FinancialCommandIdempotencyFixture f) => _f = f;

    private async Task<Guid> AccountIdAsync(string code)
    {
        using var scope = _f.CreateDbScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var accounts = await db.Accounts.IgnoreQueryFilters().AsNoTracking()
            .Where(a => a.TenantId == _f.TenantId && a.CompanyId == _f.CompanyId).ToListAsync();
        return accounts.Single(a => a.Code.Value == code).Id;
    }

    private async Task<Guid> CreateReasonAsync(CashMovementType type)
    {
        using var scope = _f.CreateDbScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var reason = CashMovementReason.Create(_f.TenantId, _f.CompanyId, $"R{Guid.NewGuid():N}"[..10],
            $"Motivo {type}", type, 1, Guid.NewGuid());
        db.Set<CashMovementReason>().Add(reason);
        await db.SaveChangesAsync();
        return reason.Id;
    }

    private static async Task SetReasonAccountAsync(HttpClient client, Guid reasonId, Guid accountId)
    {
        var response = await client.PutAsJsonAsync($"/api/v1/cash-movement-reasons/{reasonId}/account",
            new { accountingAccountId = accountId });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private static async Task<Guid> RecordMovementAsync(HttpClient client, Guid sessionId, string type, Guid reasonId, decimal amount)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/cash-sessions/{sessionId}/movements", new
        {
            movementType = type, reasonId, amount, description = $"{type} 12C", clientRequestId = Guid.NewGuid(),
        });
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.Created, body);
        return JsonDocument.Parse(body).RootElement.GetProperty("data").GetProperty("id").GetGuid();
    }

    private async Task<(AccountingPosting? Posting, JournalEntry? Entry)> AccountingOfAsync(Guid sourceEventId)
    {
        using var scope = _f.CreateDbScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var posting = await db.AccountingPostings.IgnoreQueryFilters().AsNoTracking()
            .SingleOrDefaultAsync(p => p.TenantId == _f.TenantId && p.SourceModule == "Caja" && p.SourceEventId == sourceEventId);
        var entry = await db.JournalEntries.IgnoreQueryFilters().AsNoTracking().Include(j => j.Lines)
            .SingleOrDefaultAsync(j => j.TenantId == _f.TenantId && j.SourceModule == "Caja" && j.SourceEventId == sourceEventId);
        return (posting, entry);
    }

    private async Task<string> SetCompanyTimezoneAsync(string timezone)
    {
        using var scope = _f.CreateDbScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var company = await db.Companies.IgnoreQueryFilters().SingleAsync(c => c.Id == _f.CompanyId);
        var previous = company.Timezone;
        company.Timezone = timezone;
        await db.SaveChangesAsync();
        return previous;
    }

    private async Task<DateOnly> ExpectedCompanyDateAsync(Guid sourceId, bool closed = false)
    {
        using var scope = _f.CreateDbScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var timezone = await db.Companies.IgnoreQueryFilters().Where(c => c.Id == _f.CompanyId)
            .Select(c => c.Timezone).SingleAsync();
        var instant = closed
            ? (await db.Set<CashSession>().IgnoreQueryFilters().AsNoTracking().SingleAsync(s => s.Id == sourceId)).ClosedAt!.Value
            : (await db.CashMovements.IgnoreQueryFilters().AsNoTracking().SingleAsync(m => m.Id == sourceId)).CreatedAt;
        return CompanyTimeZone.LocalDate(instant, CompanyTimeZone.Resolve(timezone));
    }

    [Theory]
    [InlineData("America/Guayaquil")]
    [InlineData("Etc/GMT+12")]
    [InlineData("Pacific/Kiritimati")]
    public async Task ManualIncome_y_ManualExpense_con_cuenta_asientan_contra_la_caja_real_y_la_cuenta_del_motivo(string timezone)
    {
        var previousTimezone = await SetCompanyTimezoneAsync(timezone);
        try
        {
            var op = await _f.CreateOperatorAsync();
            var client = _f.CreateClient(op.UserId);
            var incomeAccount = await AccountIdAsync("4.2.01.001");
            var expenseAccount = await AccountIdAsync("6.4.01.002");
            var incomeReason = await CreateReasonAsync(CashMovementType.ManualIncome);
            var expenseReason = await CreateReasonAsync(CashMovementType.ManualExpense);
            await SetReasonAccountAsync(client, incomeReason, incomeAccount);
            await SetReasonAccountAsync(client, expenseReason, expenseAccount);

            var incomeId = await RecordMovementAsync(client, op.CashSessionId, "ManualIncome", incomeReason, 25.40m);
            var expenseId = await RecordMovementAsync(client, op.CashSessionId, "ManualExpense", expenseReason, 7.15m);

            var (income, incomeEntry) = await AccountingOfAsync(incomeId);
            income!.Status.Should().Be(AccountingPostingStatus.Posted, income.ErrorMessage);
            income.FactType.Should().Be("ManualIncome");
            income.EntryDate.Should().Be(await ExpectedCompanyDateAsync(incomeId));
            incomeEntry!.Lines.Should().HaveCount(2);
            incomeEntry.Lines.Should().ContainSingle(l => l.AccountId == _f.CashAccountId && l.Debit == 25.40m);
            incomeEntry.Lines.Should().ContainSingle(l => l.AccountId == incomeAccount && l.Credit == 25.40m);

            var (expense, expenseEntry) = await AccountingOfAsync(expenseId);
            expense!.Status.Should().Be(AccountingPostingStatus.Posted, expense.ErrorMessage);
            expense.FactType.Should().Be("ManualExpense");
            expense.EntryDate.Should().Be(await ExpectedCompanyDateAsync(expenseId));
            expenseEntry!.Lines.Should().HaveCount(2);
            expenseEntry.Lines.Should().ContainSingle(l => l.AccountId == expenseAccount && l.Debit == 7.15m);
            expenseEntry.Lines.Should().ContainSingle(l => l.AccountId == _f.CashAccountId && l.Credit == 7.15m);
        }
        finally
        {
            await SetCompanyTimezoneAsync(previousTimezone);
        }
    }

    [Fact]
    public async Task Motivo_sin_cuenta_movimiento_valido_y_posting_Failed_configurar_y_reprocesar_publica()
    {
        var op = await _f.CreateOperatorAsync();
        var client = _f.CreateClient(op.UserId);
        var withdrawalAccount = await AccountIdAsync("6.4.01.002");
        var reason = await CreateReasonAsync(CashMovementType.Withdrawal);

        var movementId = await RecordMovementAsync(client, op.CashSessionId, "Withdrawal", reason, 40m);

        using (var scope = _f.CreateDbScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var session = await db.Set<CashSession>().IgnoreQueryFilters().AsNoTracking().Include(s => s.Movements)
                .SingleAsync(s => s.Id == op.CashSessionId);
            session.Movements.Should().ContainSingle(m => m.Id == movementId && m.MovementType == CashMovementType.Withdrawal);
        }
        var (failed, noEntry) = await AccountingOfAsync(movementId);
        failed!.Status.Should().Be(AccountingPostingStatus.Failed);
        failed.ErrorCode.Should().Be("CASH_MOVEMENT_REASON_ACCOUNT_NOT_CONFIGURED");
        noEntry.Should().BeNull();

        await SetReasonAccountAsync(client, reason, withdrawalAccount);
        var reprocess = await client.PostAsJsonAsync($"/api/v1/accounting/postings/{failed.Id}/reprocess",
            new { reason = "Cuenta del motivo configurada" });
        reprocess.StatusCode.Should().Be(HttpStatusCode.OK, await reprocess.Content.ReadAsStringAsync());

        var (posted, entry) = await AccountingOfAsync(movementId);
        posted!.Status.Should().Be(AccountingPostingStatus.Posted, posted.ErrorMessage);
        entry!.Lines.Should().ContainSingle(l => l.AccountId == withdrawalAccount && l.Debit == 40m);
        entry.Lines.Should().ContainSingle(l => l.AccountId == _f.CashAccountId && l.Credit == 40m);
    }

    [Fact]
    public async Task BankDeposit_Debe_banco_de_la_CompanyBankAccount_elegida_y_Haber_caja()
    {
        var op = await _f.CreateOperatorAsync();
        var client = _f.CreateClient(op.UserId);
        var otherBankAccount = await AccountIdAsync("1.1.02.001");
        var targetBankAccount = await AccountIdAsync("1.1.02.002");
        Guid targetId;
        using (var scope = _f.CreateDbScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var bank = Bank.Create(_f.TenantId, $"B{Guid.NewGuid():N}"[..8], "Banco 12C", "Banco 12C", Guid.NewGuid());
            db.Set<Bank>().Add(bank);
            await db.SaveChangesAsync();
            var other = CompanyBankAccount.Create(_f.TenantId, _f.CompanyId, bank.Id, BankAccountType.Checking,
                $"{Random.Shared.NextInt64(1_000_000_000, 9_999_999_999)}", "Otro banco", otherBankAccount, Guid.NewGuid());
            var target = CompanyBankAccount.Create(_f.TenantId, _f.CompanyId, bank.Id, BankAccountType.Savings,
                $"{Random.Shared.NextInt64(1_000_000_000, 9_999_999_999)}", "Banco destino", targetBankAccount, Guid.NewGuid());
            db.Set<CompanyBankAccount>().AddRange(other, target);
            await db.SaveChangesAsync();
            targetId = target.Id;
        }

        var response = await client.PostAsJsonAsync($"/api/v1/cash-sessions/{op.CashSessionId}/bank-deposits", new
        {
            companyBankAccountId = targetId, amount = 300m, description = "Depósito del día",
            referenceNumber = "PAP-001", clientRequestId = Guid.NewGuid(),
        });
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.Created, body);
        var data = JsonDocument.Parse(body).RootElement.GetProperty("data");
        data.GetProperty("movementType").GetString().Should().Be("BankDeposit");
        data.GetProperty("referenceId").GetGuid().Should().Be(targetId);
        var movementId = data.GetProperty("id").GetGuid();

        var (posting, entry) = await AccountingOfAsync(movementId);
        posting!.Status.Should().Be(AccountingPostingStatus.Posted, posting.ErrorMessage);
        posting.FactType.Should().Be("BankDeposit");
        entry!.Lines.Should().HaveCount(2);
        entry.Lines.Should().ContainSingle(l => l.AccountId == targetBankAccount && l.Debit == 300m);
        entry.Lines.Should().ContainSingle(l => l.AccountId == _f.CashAccountId && l.Credit == 300m);
        entry.Lines.Should().NotContain(l => l.AccountId == otherBankAccount);
    }

    [Theory]
    [InlineData("America/Guayaquil")]
    [InlineData("Etc/GMT+12")]
    [InlineData("Pacific/Kiritimati")]
    public async Task Cierre_faltante_sobrante_y_sin_diferencia(string timezone)
    {
        var previousTimezone = await SetCompanyTimezoneAsync(timezone);
        try
        {
            var surplusAccount = await AccountIdAsync("4.2.01.003");
            var shortageAccount = await AccountIdAsync("6.5.01.001");

            async Task<Guid> CloseAsync(decimal opening, decimal counted)
            {
                var op = await _f.CreateOperatorAsync(opening);
                var client = _f.CreateClient(op.UserId);
                var response = await client.PostAsJsonAsync($"/api/v1/cash-sessions/{op.CashSessionId}/close", new
                {
                    closingCounts = new[] { new { denominationValue = counted, denominationLabel = "Total", quantity = 1 } },
                    closeNotes = "Arqueo 12C",
                });
                response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
                return op.CashSessionId;
            }

            var shortageSession = await CloseAsync(100m, 90m);
            var (shortage, shortageEntry) = await AccountingOfAsync(shortageSession);
            shortage!.Status.Should().Be(AccountingPostingStatus.Posted, shortage.ErrorMessage);
            shortage.FactType.Should().Be("CashCountShortage");
            shortage.EntryDate.Should().Be(await ExpectedCompanyDateAsync(shortageSession, closed: true));
            shortageEntry!.Lines.Should().HaveCount(2);
            shortageEntry.Lines.Should().ContainSingle(l => l.AccountId == shortageAccount && l.Debit == 10m);
            shortageEntry.Lines.Should().ContainSingle(l => l.AccountId == _f.CashAccountId && l.Credit == 10m);

            var surplusSession = await CloseAsync(100m, 112.50m);
            var (surplus, surplusEntry) = await AccountingOfAsync(surplusSession);
            surplus!.Status.Should().Be(AccountingPostingStatus.Posted, surplus.ErrorMessage);
            surplus.FactType.Should().Be("CashCountSurplus");
            surplus.EntryDate.Should().Be(await ExpectedCompanyDateAsync(surplusSession, closed: true));
            surplusEntry!.Lines.Should().HaveCount(2);
            surplusEntry.Lines.Should().ContainSingle(l => l.AccountId == _f.CashAccountId && l.Debit == 12.50m);
            surplusEntry.Lines.Should().ContainSingle(l => l.AccountId == surplusAccount && l.Credit == 12.50m);

            var evenSession = await CloseAsync(100m, 100m);
            var (none, noEntry) = await AccountingOfAsync(evenSession);
            none.Should().BeNull();
            noEntry.Should().BeNull();
        }
        finally
        {
            await SetCompanyTimezoneAsync(previousTimezone);
        }
    }
}
