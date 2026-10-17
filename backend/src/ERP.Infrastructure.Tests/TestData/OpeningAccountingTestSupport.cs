using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Continuity;
using ERP.Application.Modules.Companies;
using ERP.Application.Modules.InitialLoad.OpeningPosting;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Infrastructure.Accounting.Repositories;
using ERP.Infrastructure.InitialLoad;
using ERP.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Infrastructure.Tests.TestData;

/// <summary>
/// ACCOUNTING-CONTINUITY-05 — la apertura aplica la política operativa sobre la fecha de apertura.
/// Las empresas de prueba eligen su modo explícitamente (fail-closed sin versión).
/// </summary>
public static class OpeningAccountingTestSupport
{
    public static readonly DateOnly SinceAlways = new(1900, 1, 1);

    public static IServiceCollection AddOpeningAccountingGate(this IServiceCollection services)
    {
        services.AddScoped<AccountingOperationalDecision>();
        services.AddScoped<OpeningAccountingGate>();
        return services;
    }

    public static OpeningAccountingGate Gate(ErpDbContext db, IOperationalContext ctx, Guid companyId)
    {
        var policies = new AccountingOperationalPolicyRepository(db);
        return new OpeningAccountingGate(policies, new AccountingOperationalDecision(policies),
            new OpeningBalanceConstraintsReader(db,
                Moq.Mock.Of<ICurrentCompany>(c => c.CompanyId == companyId && c.HasCompanyContext)), ctx);
    }

    public static async Task SeedModeAsync(ErpDbContext db, Guid tenantId, Guid companyId, Guid actor,
        AccountingOperationalMode mode = AccountingOperationalMode.Enabled, DateOnly? effectiveFrom = null)
    {
        db.AccountingOperationalPolicies.Add(AccountingOperationalPolicy.Create(
            tenantId, companyId, mode, effectiveFrom ?? SinceAlways, "test", actor));
        await db.SaveChangesAsync();
    }
}
