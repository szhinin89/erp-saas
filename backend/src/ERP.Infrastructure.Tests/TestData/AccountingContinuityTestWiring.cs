using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Continuity;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Expenses.Interfaces;
using ERP.Infrastructure.Accounting.Repositories;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Expenses;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Finance.Interfaces;
using ERP.Domain.Modules.Sales.Interfaces;
using ERP.Infrastructure.Persistence.Repositories.Caja;
using ERP.Infrastructure.Persistence.Repositories.Finance;
using ERP.Infrastructure.Persistence.Repositories.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
        // Actor de sistema por defecto para harnesses que no declaran usuario/auditoría propios.
        services.TryAddSingleton(Moq.Mock.Of<ICurrentUser>(u => u.UserId == Guid.Empty));
        services.TryAddScoped<ERP.Application.Audit.IAuditContext>(sp => new ERP.Infrastructure.Tests.Audit.FixedAuditContext(
            () => sp.GetRequiredService<ICurrentTenant>().TenantId,
            () => sp.GetRequiredService<ICurrentCompany>().CompanyId,
            Guid.Empty
        ));
        services.AddScoped<IAccountingPostingRepository, AccountingPostingRepository>();
        services.AddScoped<IAccountingOperationalPolicyRepository, AccountingOperationalPolicyRepository>();
        services.AddScoped<IExpenseCategoryRepository>(sp => new ExpenseCategoryRepository(
            sp.GetRequiredService<ErpDbContext>(),
            sp.GetRequiredService<ICurrentCompany>()
        ));
        // ACCOUNTING-CONTINUITY-04 — destinos financieros y reglas que el intento resuelve.
        services.TryAddScoped<ICashRegisterRepository, CashRegisterRepository>();
        services.TryAddScoped<ICompanyBankAccountRepository, CompanyBankAccountRepository>();
        services.TryAddScoped<IPaymentMethodAccountRepository, PaymentMethodAccountRepository>();
        services.TryAddScoped<IPostingRuleRepository, PostingRuleRepository>();
        services.TryAddScoped<IAccountRepository, AccountRepository>();
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
        // ACCOUNTING-CONTINUITY-05 — obligaciones de costo de ventas ancladas a la factura.
        services.TryAddScoped<ERP.Domain.Modules.Sales.Interfaces.ISalesInvoiceRepository>(sp =>
            new ERP.Infrastructure.Persistence.Repositories.Sales.SalesInvoiceRepository(
                sp.GetRequiredService<ErpDbContext>(), sp.GetRequiredService<ICurrentCompany>()));
        services.TryAddScoped<ERP.Domain.Modules.Inventory.Interfaces.IInventoryCostLedger>(sp =>
            new ERP.Infrastructure.Persistence.Repositories.Inventory.InventoryCostLedger(sp.GetRequiredService<ErpDbContext>()));
        services.AddScoped<ERP.Application.Modules.Accounting.Posting.Translators.SaleCostPostingRecorder>();
        // Precisión contable de la empresa (SSOT); los hosts que ya registran su proveedor lo conservan.
        services.TryAddSingleton<ERP.Application.Modules.Companies.ICompanyPrecisionPolicyProvider>(
            StandardPrecisionPolicyProvider.Instance);

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

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ErpDbContext, IServiceProvider> Providers = new();

    /// <summary>
    /// Asocia el contenedor que publica los eventos de <paramref name="db"/>: permite ejecutar, tras
    /// el commit del caso de uso, el intento inicial de las obligaciones que registró (igual que
    /// <c>AccountingPostingDispatchBehavior</c> en producción, que los tests omiten al invocar el
    /// handler directamente).
    /// </summary>
    public static void Register(ErpDbContext db, IServiceProvider provider) => Providers.AddOrUpdate(db, provider);

    /// <summary>
    /// Intento inicial post-commit de las obligaciones pendientes del contexto (no-op si no está
    /// cableado). El ejecutor limpia el ChangeTracker de su contexto; como estos harnesses reutilizan
    /// el mismo contexto para el siguiente acto del test (en producción sería otro request), se
    /// vuelven a adjuntar como Unchanged las entidades ya confirmadas que el test tenía en uso.
    /// </summary>
    public static async Task DispatchPendingAsync(ErpDbContext db)
    {
        if (!Providers.TryGetValue(db, out var provider))
            return;
        // Valores originales y actuales (incluidas propiedades sombra como xmin) por entrada: se
        // restauran entrada a entrada, dueños antes que tipos owned, sin recorrer el grafo.
        var tracked = db.ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Unchanged)
            .OrderBy(e => e.Metadata.IsOwned())
            .Select(e => (e.Entity, Original: e.OriginalValues.Clone(), Current: e.CurrentValues.Clone()))
            .ToList();
        await provider.GetRequiredService<IAccountingPostingDispatcher>().DispatchPendingAsync(CancellationToken.None);
        foreach (var (entity, original, current) in tracked)
        {
            var entry = db.Entry(entity);
            if (entry.State != EntityState.Detached)
                continue;
            entry.CurrentValues.SetValues(current);
            entry.OriginalValues.SetValues(original);
            entry.State = EntityState.Unchanged;
        }
    }

    /// <summary>
    /// Re-entrega de un evento ya publicado (simula un duplicado de mensajería) con la misma forma
    /// que la publicación real: dentro de una transacción del contexto, seguida de su commit y del
    /// intento post-commit.
    /// </summary>
    public static async Task RepublishAsync(ErpDbContext db, MediatR.IPublisher publisher, object notification)
    {
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            await publisher.Publish(notification);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        await DispatchPendingAsync(db);
    }

    /// <summary>Reproceso manual de una obligación con el ejecutor común del contenedor cableado.</summary>
    public static async Task<AccountingPostingDto> ReprocessAsync(ErpDbContext db, Guid postingId, string reason)
    {
        var provider = Providers.TryGetValue(db, out var p)
            ? p
            : throw new InvalidOperationException("Contexto sin cableado contable.");
        var result = await provider.GetRequiredService<AccountingPostingExecutor>()
            .ExecuteAsync(postingId, AccountingPostingAttemptTrigger.Manual, reason, CancellationToken.None);
        return result.Value ?? throw new InvalidOperationException(result.Error);
    }

    /// <summary>Completa la operación y luego ejecuta el intento inicial post-commit.</summary>
    public static async Task<T> ThenDispatchAsync<T>(this Task<T> operation, ErpDbContext db)
    {
        var result = await operation;
        await DispatchPendingAsync(db);
        return result;
    }

    /// <summary>
    /// Períodos Open que cubren la fecha efectiva de un reverso (instante del acto convertido a la
    /// fecha de la Company): mes de hoy ±1 día, sin duplicar los ya sembrados.
    /// </summary>
    public static async Task EnsureCurrentPeriodsAsync(ErpDbContext db, Guid tenantId, Guid companyId, Guid userId)
    {
        // El reverso fecha con el instante real del evento; un harness puede fijar además el reloj
        // de la Company con TimeProvider: se cubren ambas fechas.
        var utcToday = DateOnly.FromDateTime(DateTime.UtcNow);
        var clockToday = Providers.TryGetValue(db, out var provider)
            && provider.GetService<ERP.Application.Common.Services.ICompanyClock>() is { } clock
                ? await clock.TodayAsync(companyId, tenantId)
                : utcToday;
        var months = new[] { utcToday.AddDays(-1), utcToday, utcToday.AddDays(1), clockToday }
            .Select(d => new DateOnly(d.Year, d.Month, 1))
            .Distinct();
        foreach (var start in months)
        {
            if (await db.AccountingPeriods.IgnoreQueryFilters().AnyAsync(p => p.CompanyId == companyId && p.StartDate == start))
                continue;
            db.AccountingPeriods.Add(AccountingPeriod.Create(tenantId, companyId, start.Year, start.Month, start,
                start.AddMonths(1).AddDays(-1), userId));
        }
        await db.SaveChangesAsync();
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
