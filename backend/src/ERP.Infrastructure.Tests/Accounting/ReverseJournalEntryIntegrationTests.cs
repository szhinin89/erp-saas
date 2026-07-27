using ERP.Application.Common;
using ERP.Application.Modules.Accounting.UseCases.JournalEntries;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Accounting.Repositories;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace ERP.Infrastructure.Tests.Accounting;

/// <summary>
/// Suite de integración (PostgreSQL 16 real vía Testcontainers) para el reverso contable (Fase
/// 5.4, ADR-026 §9). Cubre lo que un mock no puede: persistencia real de las líneas invertidas,
/// numeración correlativa entre el asiento original y el reverso, y el índice único
/// uq_journal_entries_original_journal_entry_id como garantía final de BD contra un doble reverso
/// bajo lecturas concurrentes. Requiere Docker.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class ReverseJournalEntryIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_reverse_journal_entry_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private Guid _tenantId;
    private Guid _companyId;
    private Guid _accountingPeriodId;
    private Guid _createdBy;
    private Guid _debitAccountId;
    private Guid _creditAccountId;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        _createdBy = Guid.NewGuid();
        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], _createdBy);
        var company = Company.CreateManaged(tenant.Id, "1790012345001", "Test S.A.", createdBy: _createdBy);
        var period = AccountingPeriod.Create(
            tenant.Id, company.Id, 2026, 7, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), _createdBy);
        var debitAccount = Account.Create(
            tenant.Id, company.Id, AccountCode.Create("1.1.01"), "Caja", null,
            AccountType.Asset, AccountNature.Debit, allowsPosting: true, createdBy: _createdBy);
        var creditAccount = Account.Create(
            tenant.Id, company.Id, AccountCode.Create("4.1.01"), "Ventas", null,
            AccountType.Income, AccountNature.Credit, allowsPosting: true, createdBy: _createdBy);

        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        db.AccountingPeriods.Add(period);
        db.Accounts.AddRange(debitAccount, creditAccount);
        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
        _companyId = company.Id;
        _accountingPeriodId = period.Id;
        _debitAccountId = debitAccount.Id;
        _creditAccountId = creditAccount.Id;
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        return new ErpDbContext(options, new FixedCurrentTenant(_tenantId), new NoOpPublisher(), new FixedCurrentCompany(_companyId));
    }

    private async Task<Guid> SeedPostedEntryAsync()
    {
        // Reserva el número vía JournalEntrySequenceRepository (no lo fija a mano) — así la fila
        // de journal_entry_sequences queda en el mismo estado que dejaría el Posting Engine real,
        // y el reverso posterior (que también reserva de la misma secuencia) recibe el siguiente
        // correlativo real en vez de colisionar contra un número asignado a mano.
        await using var db = CreateContext();
        var entryNumber = await new JournalEntrySequenceRepository(db)
            .ReserveNextNumberAsync(_tenantId, _companyId, 2026, CancellationToken.None);

        var entry = JournalEntry.Create(
            _tenantId, _companyId, new DateOnly(2026, 7, 15), _accountingPeriodId, 2026,
            "Sales", "InvoiceIssued", Guid.NewGuid(), "Asiento original", _createdBy);
        entry.AddLine(_debitAccountId, "Débito original", 100m, 0m);
        entry.AddLine(_creditAccountId, "Crédito original", 0m, 100m);
        entry.Post(_createdBy, entryNumber);

        db.JournalEntries.Add(entry);
        await db.SaveChangesAsync();
        return entry.Id;
    }

    private static ReverseJournalEntryCommandHandler BuildHandler(ErpDbContext db, Guid tenantId, Guid companyId, Guid userId) => new(
        new JournalEntryRepository(db),
        new AccountingPeriodRepository(db),
        new JournalEntrySequenceRepository(db),
        new FixedCurrentTenant(tenantId),
        new FixedCurrentCompany(companyId),
        new FixedCurrentUser(userId));

    [Fact]
    public async Task Reverso_persiste_el_nuevo_asiento_con_lineas_invertidas_y_EntryNumber_correlativo()
    {
        var originalId = await SeedPostedEntryAsync();

        await using var db = CreateContext();
        var result = await BuildHandler(db, _tenantId, _companyId, _createdBy)
            .Handle(new ReverseJournalEntryCommand(originalId, "Error de digitación"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.EntryNumber.Should().Be(2, because: "el original ya ocupó el número 1 del mismo (CompanyId, FiscalYear)");

        await using var verifyDb = CreateContext();
        var reversal = await verifyDb.JournalEntries.Include(x => x.Lines)
            .FirstAsync(x => x.Id == result.Value.Id);

        reversal.Status.Should().Be(JournalEntryStatus.Posted);
        reversal.Lines.Should().HaveCount(2);
        reversal.Lines.Should().Contain(l => l.AccountId == _debitAccountId && l.Credit == 100m && l.Debit == 0m);
        reversal.Lines.Should().Contain(l => l.AccountId == _creditAccountId && l.Debit == 100m && l.Credit == 0m);
    }

    [Fact]
    public async Task Reverso_establece_trazabilidad_bidireccional_persistida()
    {
        var originalId = await SeedPostedEntryAsync();

        await using var db = CreateContext();
        var result = await BuildHandler(db, _tenantId, _companyId, _createdBy)
            .Handle(new ReverseJournalEntryCommand(originalId, "Ajuste contable"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        await using var verifyDb = CreateContext();
        var original = await verifyDb.JournalEntries.FirstAsync(x => x.Id == originalId);
        var reversal = await verifyDb.JournalEntries.FirstAsync(x => x.Id == result.Value!.Id);

        original.Status.Should().Be(JournalEntryStatus.Reversed);
        original.ReverseJournalEntryId.Should().Be(reversal.Id);
        original.ReverseReason.Should().Be("Ajuste contable");
        reversal.OriginalJournalEntryId.Should().Be(original.Id);
    }

    [Fact]
    public async Task Indice_unico_original_journal_entry_id_rechaza_un_segundo_reverso_concurrente()
    {
        var originalId = await SeedPostedEntryAsync();

        // Dos contextos independientes leen el mismo asiento (todavía Posted en ambos, ninguno ha
        // comiteado) — simula la ventana de carrera entre dos solicitudes de reverso concurrentes
        // para el mismo asiento original. El primer SaveChangesAsync gana; el segundo debe fallar
        // por uq_journal_entries_original_journal_entry_id, no por el chequeo de Status en memoria
        // (que en este escenario no alcanza a detectar el conflicto porque ambas copias se
        // cargaron antes de que la primera comiteara).
        await using var dbA = CreateContext();
        await using var dbB = CreateContext();

        var originalA = await dbA.JournalEntries.Include(x => x.Lines).FirstAsync(x => x.Id == originalId);
        var originalB = await dbB.JournalEntries.Include(x => x.Lines).FirstAsync(x => x.Id == originalId);

        var reversalA = originalA.Reverse(_createdBy, 2, "Reverso A");
        var reversalB = originalB.Reverse(_createdBy, 3, "Reverso B");

        dbA.JournalEntries.Add(reversalA);
        await dbA.SaveChangesAsync();

        dbB.JournalEntries.Add(reversalB);
        var act = async () => await dbB.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>(
            because: "uq_journal_entries_original_journal_entry_id impide dos reversos para el mismo asiento original");
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

    private sealed class FixedCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId => userId;
        public bool IsAuthenticated => true;
        public string? Username => null;
        public string? Email => null;
        public string? FullName => null;
        public string? Role => null;
    }

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
            => Task.CompletedTask;
    }
}
