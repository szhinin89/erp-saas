using System.Reflection;
using ERP.API.Controllers.Accounting;
using ERP.Domain.Kernel.Attributes;
using ERP.Domain.Kernel.Modules;
using ERP.Domain.Kernel.Permissions;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;

namespace ERP.API.Tests;

/// <summary>
/// ACCOUNTING-11 — reglas contables: consulta con accounting.view; crear, editar, activar y
/// desactivar con accounting.rules.manage (asignable desde el NavItem "Reglas contables").
/// </summary>
public sealed class AccountingPostingRulePermissionContractTests
{
    [Theory]
    [InlineData(nameof(AccountingController.GetPostingRules), AccountingPermissions.View)]
    [InlineData(nameof(AccountingController.GetPostingRuleById), AccountingPermissions.View)]
    [InlineData(nameof(AccountingController.GetPostingFactCatalog), AccountingPermissions.View)]
    [InlineData(nameof(AccountingController.CreatePostingRule), AccountingPermissions.RulesManage)]
    [InlineData(nameof(AccountingController.UpdatePostingRule), AccountingPermissions.RulesManage)]
    [InlineData(nameof(AccountingController.EnablePostingRule), AccountingPermissions.RulesManage)]
    [InlineData(nameof(AccountingController.DisablePostingRule), AccountingPermissions.RulesManage)]
    public void A11_endpoints_de_reglas_exigen_su_permiso(string methodName, string permission)
    {
        var policies = typeof(AccountingController)
            .GetMethod(methodName)!
            .GetCustomAttributes<AuthorizeAttribute>()
            .Select(a => a.Policy)
            .ToList();

        policies.Should().ContainSingle().Which.Should().Be($"perm:{permission}");

        var navItem = typeof(AccountingModule)
            .GetField(nameof(AccountingModule.PostingRules))!
            .GetCustomAttribute<NavItemAttribute>()!;
        navItem.RelatedActionPermissionsCsv.Should().Be(AccountingPermissions.RulesManage);
    }
}
