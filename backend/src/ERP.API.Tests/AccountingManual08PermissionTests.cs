using System.Reflection;
using ERP.API.Controllers.Accounting;
using ERP.Domain.Kernel.Attributes;
using ERP.Domain.Kernel.Modules;
using ERP.Domain.Kernel.Permissions;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;

namespace ERP.API.Tests;

public sealed class AccountingManual08PermissionTests
{
    [Fact]
    public void A08_05_each_mutation_has_a_distinct_server_permission_registered_on_the_journal_navitem()
    {
        var expected = new Dictionary<string, string>
        {
            [nameof(ManualJournalController.Create)] = AccountingPermissions.ManualCreate,
            [nameof(ManualJournalController.Edit)] = AccountingPermissions.ManualEdit,
            [nameof(ManualJournalController.Post)] = AccountingPermissions.ManualPost,
            [nameof(ManualJournalController.Reverse)] = AccountingPermissions.ManualReverse,
            [nameof(ManualJournalController.Discard)] = AccountingPermissions.ManualDiscard,
        };
        expected.Values.Distinct().Should().HaveCount(5);
        var nav = typeof(AccountingModule).GetField(nameof(AccountingModule.JournalEntries))!.GetCustomAttribute<NavItemAttribute>()!;
        foreach (var (method, permission) in expected)
        {
            typeof(ManualJournalController).GetMethod(method)!.GetCustomAttributes<AuthorizeAttribute>()
                .Select(a => a.Policy).Should().ContainSingle().Which.Should().Be("perm:" + permission);
            nav.RelatedActionPermissionsCsv!.Split(',').Should().Contain(permission);
        }
        typeof(AccountingController).GetMethod(nameof(AccountingController.GetJournalEntryById))!
            .GetCustomAttributes<AuthorizeAttribute>().Select(a => a.Policy).Should().ContainSingle()
            .Which.Should().Be("perm:" + AccountingPermissions.View);
    }
}
