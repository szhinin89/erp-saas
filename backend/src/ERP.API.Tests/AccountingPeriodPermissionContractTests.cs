using System.Reflection;
using ERP.API.Controllers.Accounting;
using ERP.Domain.Kernel.Permissions;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;

namespace ERP.API.Tests;

/// <summary>
/// ACCOUNTING-07 — permisos dedicados de Períodos: lectura con accounting.view, crear/generar con
/// accounting.periods.create y cerrar/bloquear con accounting.periods.close (nunca accounting.update).
/// </summary>
public sealed class AccountingPeriodPermissionContractTests
{
    [Theory]
    [InlineData(nameof(AccountingController.CloseAccountingPeriod), AccountingPermissions.PeriodsClose)]
    [InlineData(nameof(AccountingController.LockAccountingPeriod), AccountingPermissions.PeriodsClose)]
    [InlineData(nameof(AccountingController.CreateAccountingPeriod), AccountingPermissions.PeriodsCreate)]
    [InlineData(nameof(AccountingController.GenerateAccountingFiscalYear), AccountingPermissions.PeriodsCreate)]
    [InlineData(nameof(AccountingController.GetAccountingPeriods), AccountingPermissions.View)]
    [InlineData(nameof(AccountingController.GetAccountingPeriodClosureReadiness), AccountingPermissions.View)]
    public void A07_endpoints_de_periodos_exigen_su_permiso_dedicado(string methodName, string permission)
    {
        var policies = typeof(AccountingController)
            .GetMethod(methodName)!
            .GetCustomAttributes<AuthorizeAttribute>()
            .Select(a => a.Policy)
            .ToList();

        policies.Should().ContainSingle().Which.Should().Be($"perm:{permission}");
    }
}
