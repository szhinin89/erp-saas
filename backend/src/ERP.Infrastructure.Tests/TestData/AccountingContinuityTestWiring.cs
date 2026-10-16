using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Continuity;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Expenses.Interfaces;
using ERP.Infrastructure.Accounting.Repositories;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Expenses;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Infrastructure.Tests.TestData;

/// <summary>
/// ACCOUNTING-CONTINUITY-01 — mismo registro que <c>ERP.Application.DependencyInjection</c> /
/// <c>ERP.Infrastructure.DependencyInjection</c> para la obligación contable durable, en los
/// contenedores DI manuales de los tests de integración. Requiere que el contenedor ya tenga
/// <see cref="ErpDbContext"/>, ICurrentTenant/ICurrentCompany/ICurrentUser, IAuditContext,
/// IPostingEngine con sus repositorios y logging.
/// </summary>
public static class AccountingContinuityTestWiring
{
    public static IServiceCollection AddAccountingContinuity(this IServiceCollection services)
    {
        services.AddScoped<IUnitOfWork>(sp => new UnitOfWork(sp.GetRequiredService<ErpDbContext>()));
        services.AddScoped<IAccountingPostingRepository, AccountingPostingRepository>();
        services.AddScoped<IAccountingOperationalPolicyRepository, AccountingOperationalPolicyRepository>();
        services.AddScoped<IExpenseCategoryRepository>(sp => new ExpenseCategoryRepository(
            sp.GetRequiredService<ErpDbContext>(),
            sp.GetRequiredService<ICurrentCompany>()
        ));
        services.AddScoped<AccountingOperationalDecision>();
        services.AddScoped<AccountingPostingDispatchQueue>();
        services.AddScoped<AccountingPostingRecorder>();
        services.AddScoped<AccountingPostingFactBuilder>();
        services.AddScoped<AccountingPostingExecutor>();
        services.AddScoped<IAccountingPostingDispatcher>(sp =>
            sp.GetRequiredService<AccountingPostingExecutor>()
        );
        services.AddScoped<AccountingPostingCancellation>();
        services.AddScoped<ERP.Application.Modules.Accounting.UseCases.JournalEntries.JournalEntryReversal>();
        services.AddScoped<AccountingReversalRecorder>();
        services.AddScoped<IAccountingReversalRecorder>(sp => sp.GetRequiredService<AccountingReversalRecorder>());

        return services;
    }

    /// <summary>
    /// Motor contable real (IPostingEngine) con sus repositorios PostgreSQL — mismo registro que
    /// ERP.Infrastructure.DependencyInjection — para hosts que confirman documentos reales.
    /// </summary>
    public static IServiceCollection AddAccountingEngine(this IServiceCollection services)
    {
        services.AddScoped<IJournalEntryRepository, JournalEntryRepository>();
        services.AddScoped<IPostingRuleRepository, PostingRuleRepository>();
        services.AddScoped<IAccountingPeriodRepository, AccountingPeriodRepository>();
        services.AddScoped<IJournalEntrySequenceRepository, JournalEntrySequenceRepository>();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<ERP.Application.Modules.Accounting.Posting.IPostingEngine, ERP.Application.Modules.Accounting.Posting.PostingEngine>();
        return services;
    }

    /// <summary>Política Enabled desde 1900-01-01 — mismo dato que la migración da a las empresas existentes.</summary>
    public static AccountingOperationalPolicy EnabledPolicy(
        Guid tenantId,
        Guid companyId,
        Guid createdBy,
        DateOnly? effectiveFrom = null
    ) =>
        AccountingOperationalPolicy.Create(
            tenantId,
            companyId,
            AccountingOperationalMode.Enabled,
            effectiveFrom ?? new DateOnly(1900, 1, 1),
            "Test",
            createdBy
        );
}
