using System.Net;
using System.Net.Http.Json;
using ERP.Application.Modules.Accounting.UseCases.Accounts;
using ERP.Application.Modules.Accounting.UseCases.PostingRules;
using ERP.Domain.Modules.Accounting.Enums;
using FluentAssertions;

namespace ERP.API.Tests.Integration;

public sealed partial class InternalCapabilitiesHttpTests
{
    [Fact]
    public async Task Plan_y_reglas_CRUD_persistidos_jerarquia_scope_y_cuentas_en_uso_por_HTTP()
    {
        const string accounts = "/api/v1/accounting/accounts";
        const string rules = "/api/v1/accounting/posting-rules";
        using var admin = Admin();
        var root = await Data(await admin.PostAsJsonAsync(accounts, new CreateAccountCommand("1", "Activo", null, AccountType.Asset, AccountNature.Debit, false)));
        var rootId = root.GetProperty("id").GetGuid();
        var cash = await Data(await admin.PostAsJsonAsync(accounts, new CreateAccountCommand("1.91", "Caja prueba", rootId, AccountType.Asset, AccountNature.Debit, true)));
        var cashId = cash.GetProperty("id").GetGuid();
        var income = await Data(await admin.PostAsJsonAsync(accounts, new CreateAccountCommand("4", "Ingreso prueba", null, AccountType.Income, AccountNature.Credit, true)));
        var incomeId = income.GetProperty("id").GetGuid();
        var updated = await Data(await admin.PatchAsJsonAsync($"{accounts}/{cashId}", new UpdateAccountCommand(cashId, "Caja editada", rootId, true)));
        updated.GetProperty("name").GetString().Should().Be("Caja editada");
        using var duplicate = await admin.PostAsJsonAsync(accounts, new CreateAccountCommand("1.91", "Duplicada", rootId, AccountType.Asset, AccountNature.Debit, true));
        duplicate.IsSuccessStatusCode.Should().BeFalse();
        using var cycle = await admin.PatchAsJsonAsync($"{accounts}/{rootId}", new UpdateAccountCommand(rootId, "Ciclo", cashId, false));
        cycle.IsSuccessStatusCode.Should().BeFalse();
        var lines = new PostingRuleLineInput[]
        {
            new(cashId, AccountNature.Debit, PostingAmountKind.PendingBalance),
            new(incomeId, AccountNature.Credit, PostingAmountKind.Subtotal),
        };
        var rule = await Data(await admin.PostAsJsonAsync(rules, new CreatePostingRuleCommand("Sales", "InvoiceIssued", lines)));
        var ruleId = rule.GetProperty("id").GetGuid();
        (await Data(await admin.PatchAsJsonAsync($"{rules}/{ruleId}", new UpdatePostingRuleCommand(ruleId, lines)))).GetProperty("lines").GetArrayLength().Should().Be(2);
        using var referenced = await admin.PatchAsync($"{accounts}/{cashId}/disable", null);
        referenced.IsSuccessStatusCode.Should().BeFalse("una regla activa referencia la cuenta");
        await Data(await admin.PatchAsync($"{rules}/{ruleId}/disable", null));
        (await Data(await admin.PatchAsync($"{accounts}/{cashId}/disable", null))).GetProperty("isActive").GetBoolean().Should().BeFalse();
        await Data(await admin.PatchAsync($"{accounts}/{cashId}/enable", null));
        await Data(await admin.PatchAsync($"{rules}/{ruleId}/enable", null));
        using var reopened = Admin();
        var loaded = await Data(await reopened.GetAsync($"{accounts}/by-code/1.91"));
        loaded.GetProperty("name").GetString().Should().Be("Caja editada");
        loaded.GetProperty("parentAccountId").GetGuid().Should().Be(rootId);
        var persisted = await Data(await reopened.GetAsync($"{rules}/{ruleId}"));
        persisted.GetProperty("isActive").GetBoolean().Should().BeTrue();
        persisted.GetProperty("lines").GetArrayLength().Should().Be(2);
        reopened.DefaultRequestHeaders.Remove("X-Company-Id");
        reopened.DefaultRequestHeaders.Add("X-Company-Id", _f.CompanyBId.ToString());
        (await reopened.GetAsync($"{accounts}/{cashId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await reopened.GetAsync($"{rules}/{ruleId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
