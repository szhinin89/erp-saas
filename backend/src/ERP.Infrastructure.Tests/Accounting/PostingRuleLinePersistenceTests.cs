using ERP.Application.Common;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Accounting;

/// <summary>
/// Suite de integración (PostgreSQL 16 real vía Testcontainers) para la persistencia EF Core de
/// <see cref="PostingRuleLine"/> (Fase 3.5.4 — modelo de dominio aprobado en Fase 3.5.3). Solo
/// verifica el mapeo/las relaciones — <c>PostingRuleResolver</c>/<c>JournalFactory</c> siguen sin
/// consumir estas líneas. Requiere Docker.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class PostingRuleLinePersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_posting_rule_line_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private Guid _tenantId;
    private Guid _companyId;
    private Guid _createdBy;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        _createdBy = Guid.NewGuid();
        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], _createdBy);
        var company = Company.CreateManaged(
            tenant.Id,
            "1790012345001",
            "Test S.A.",
            createdBy: _createdBy
        );

        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
        _companyId = company.Id;
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        return new ErpDbContext(
            options,
            new FixedCurrentTenant(_tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany(_companyId)
        );
    }

    private PostingRule BuildRuleWithLines(out Guid debitAccountId, out Guid creditAccountId)
    {
        debitAccountId = Guid.NewGuid();
        creditAccountId = Guid.NewGuid();

        var rule = PostingRule.Create(
            _tenantId,
            _companyId,
            "Sales",
            "InvoiceIssued",
            null,
            null,
            null,
            _createdBy
        );
        rule.AddLine(debitAccountId, AccountNature.Debit, PostingAmountKind.Subtotal);
        rule.AddLine(creditAccountId, AccountNature.Credit, PostingAmountKind.TaxVat);
        return rule;
    }

    [Fact]
    public async Task Guardar_PostingRule_con_lineas_persiste_las_lineas()
    {
        var rule = BuildRuleWithLines(out _, out _);

        await using var db = CreateContext();
        db.PostingRules.Add(rule);
        await db.SaveChangesAsync();

        await using var verifyDb = CreateContext();
        var count = await verifyDb.PostingRuleLines.CountAsync(x => x.PostingRuleId == rule.Id);
        count.Should().Be(2);
    }

    [Fact]
    public async Task Recuperar_PostingRule_incluye_las_lineas_via_navegacion()
    {
        var rule = BuildRuleWithLines(out var debitAccountId, out var creditAccountId);

        await using var db = CreateContext();
        db.PostingRules.Add(rule);
        await db.SaveChangesAsync();

        await using var verifyDb = CreateContext();
        var loaded = await verifyDb
            .PostingRules.Include(x => x.Lines)
            .FirstAsync(x => x.Id == rule.Id);

        loaded.Lines.Should().HaveCount(2);
        loaded
            .Lines.Should()
            .Contain(l =>
                l.AccountId == debitAccountId
                && l.Nature == AccountNature.Debit
                && l.AmountKind == PostingAmountKind.Subtotal
            );
        loaded
            .Lines.Should()
            .Contain(l =>
                l.AccountId == creditAccountId
                && l.Nature == AccountNature.Credit
                && l.AmountKind == PostingAmountKind.TaxVat
            );
    }

    [Fact]
    public async Task PostingRuleLine_no_valida_existencia_de_AccountId_a_nivel_de_base_de_datos()
    {
        // Decisión de diseño (ADR-026 §6.2, ver PostingRule.cs): AccountId es columna plana sin
        // FK — la validación de existencia/pertenencia a la Company es responsabilidad de
        // Application/Infrastructure al resolver, no de la base de datos. Esto contrasta
        // deliberadamente con JournalEntryLine.AccountId, que sí tiene FK real.
        var rule = PostingRule.Create(
            _tenantId,
            _companyId,
            "Sales",
            "InvoiceIssued",
            null,
            null,
            null,
            _createdBy
        );
        rule.AddLine(Guid.NewGuid(), AccountNature.Debit, PostingAmountKind.Subtotal);

        await using var db = CreateContext();
        db.PostingRules.Add(rule);
        var act = async () => await db.SaveChangesAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Eliminar_PostingRule_elimina_sus_lineas_en_cascada()
    {
        var rule = BuildRuleWithLines(out _, out _);

        await using (var db = CreateContext())
        {
            db.PostingRules.Add(rule);
            await db.SaveChangesAsync();
        }

        await using (var db = CreateContext())
        {
            var loaded = await db.PostingRules.FirstAsync(x => x.Id == rule.Id);
            db.PostingRules.Remove(loaded);
            await db.SaveChangesAsync();
        }

        await using var verifyDb = CreateContext();
        var remaining = await verifyDb.PostingRuleLines.CountAsync(x => x.PostingRuleId == rule.Id);
        remaining
            .Should()
            .Be(0, because: "ON DELETE CASCADE elimina las líneas junto con su regla padre");
    }

    private sealed class FixedCurrentTenant(Guid tenantId) : ICurrentTenant
    {
        public Guid TenantId => tenantId;
        public string? Slug => null;
    }

    private sealed class FixedCurrentCompany(Guid companyId) : ICurrentCompany
    {
        public Guid CompanyId => companyId;
        public bool IsAuthenticated => true;
        public bool HasCompanyContext => companyId != Guid.Empty;
    }

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken cancellationToken = default
        )
            where TNotification : INotification => Task.CompletedTask;
    }
}
