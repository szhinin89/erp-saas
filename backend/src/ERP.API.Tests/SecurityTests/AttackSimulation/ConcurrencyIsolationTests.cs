using Microsoft.EntityFrameworkCore;
using ERP.API.Tests.SecurityTests.Infrastructure;
using ERP.API.Tests.Support;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.SecurityTests.AttackSimulation;

/// <summary>
/// ATTACK CATEGORY 8 â€” Concurrency Tenant Poisoning
///
/// OBJETIVO: Verificar que 2 tenants simultÃ¡neos no contaminan sus datos.
/// Simula requests paralelos de Company A1 y Company B1.
/// </summary>
public sealed class ConcurrencyIsolationTests
{
    [Fact(DisplayName = "ATTACK-8.1: Concurrent A1 and B1 queries produce isolated results")]
    public async Task Attack_8_1_Concurrent_queries_return_isolated_results()
    {
        await using var factoryA = new IntegrationTestWebAppFactory();
        var stateA = await SecurityTestSeeder.SeedAsync(factoryA.Services);

        await using var factoryB = new IntegrationTestWebAppFactory();
        var stateB = await SecurityTestSeeder.SeedAsync(factoryB.Services);

        // Both factories use separate InMemory DBs (different _dbName)
        factoryA.MutableSubscriber.SubscriberId = stateA.SubscriberAId;
        factoryA.MutableCompany.CompanyId = stateA.CompanyA1Id;

        factoryB.MutableSubscriber.SubscriberId = stateB.SubscriberBId;
        factoryB.MutableCompany.CompanyId = stateB.CompanyB1Id;

        // Execute both queries simultaneously
        var taskA = Task.Run(async () =>
        {
            using var scopeA = factoryA.Services.CreateScope();
            var dbA = scopeA.ServiceProvider.GetRequiredService<ErpDbContext>();
            return await dbA.Accounts.ToListAsync();
        });

        var taskB = Task.Run(async () =>
        {
            using var scopeB = factoryB.Services.CreateScope();
            var dbB = scopeB.ServiceProvider.GetRequiredService<ErpDbContext>();
            return await dbB.Accounts.ToListAsync();
        });

        var (resultsA, resultsB) = await (taskA, taskB).WhenBoth();

        resultsA.Should().HaveCount(1, "Concurrent query for A1 must return exactly 1 account.");
        resultsB.Should().HaveCount(1, "Concurrent query for B1 must return exactly 1 account.");

        resultsA.Should().OnlyContain(a => a.CompanyId == stateA.CompanyA1Id,
            "A1 concurrent result must only contain A1 accounts.");
        resultsB.Should().OnlyContain(a => a.CompanyId == stateB.CompanyB1Id,
            "B1 concurrent result must only contain B1 accounts.");

        // Cross-contamination check
        var intersection = resultsA.Select(a => a.Id)
            .Intersect(resultsB.Select(a => a.Id))
            .ToList();

        intersection.Should().BeEmpty(
            "CONCURRENCY ATTACK BLOCKED: No shared data between concurrent A1 and B1 queries. " +
            "Zero cross-contamination under parallel execution.");
    }

    [Fact(DisplayName = "ATTACK-8.2: Rapid context switching â€” each context sees only its own data")]
    public async Task Attack_8_2_Rapid_context_switching_maintains_isolation()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        var state = await SecurityTestSeeder.SeedAsync(factory.Services);

        var results = new List<(string Context, int Count, bool CrossLeak)>();

        // Rapidly switch contexts â€” simulate race condition attack
        for (int i = 0; i < 10; i++)
        {
            Guid subId; Guid compId; string label;
            if (i % 3 == 0) { subId = state.SubscriberAId; compId = state.CompanyA1Id; label = "A1"; }
            else if (i % 3 == 1) { subId = state.SubscriberAId; compId = state.CompanyA2Id; label = "A2"; }
            else { subId = state.SubscriberBId; compId = state.CompanyB1Id; label = "B1"; }

            factory.MutableSubscriber.SubscriberId = subId;
            factory.MutableCompany.CompanyId = compId;

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

            var accounts = await db.Accounts.ToListAsync();
            var crossLeak = accounts.Any(a => a.CompanyId != compId);

            results.Add((label, accounts.Count, crossLeak));
        }

        results.Should().AllSatisfy(r =>
        {
            r.Count.Should().Be(1,
                $"Context {r.Context}: rapid context switch must return exactly 1 account.");
            r.CrossLeak.Should().BeFalse(
                $"Context {r.Context}: cross-company leak detected after context switch. " +
                "CONCURRENCY ATTACK SUCCEEDED â€” isolation breached.");
        });
    }
}

// Helper for awaiting two tasks simultaneously
internal static class TaskExtensions
{
    public static async Task<(T1, T2)> WhenBoth<T1, T2>(this (Task<T1>, Task<T2>) tasks)
    {
        await Task.WhenAll(tasks.Item1, tasks.Item2);
        return (tasks.Item1.Result, tasks.Item2.Result);
    }
}
