using ERP.Application.Common;
using ERP.Application.Modules.InitialLoad.UseCases.ConfirmImportBatch;
using ERP.Application.Modules.InitialLoad.UseCases.ValidateImportBatch;
using ERP.Application.Modules.Sales.UseCases;
using ERP.Domain.Modules.InitialLoad.Enums;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Domain.Modules.Sales.Interfaces;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Tests.InitialLoad;

/// <summary>
/// IL-5B — confirmación atómica de CxC Inicial sobre PostgreSQL real: una SalesReceivable
/// InitialBalance por fila con su cuota, todo el lote en una transacción, revalidación contra el
/// estado actual y rollback total ante cualquier fallo. Nunca crea facturas.
/// </summary>
public sealed partial class ValidateInitialReceivablesPostgreSqlTests
{
    private int? _failOnAdd;

    private async Task<Guid> ValidatedBatchAsync(params Dictionary<string, string?>[] rows)
    {
        var batchId = await UploadedBatchAsync(rows);
        var validation = await SendAsync(new ValidateImportBatchCommand(batchId));
        validation.IsSuccess.Should().BeTrue(validation.Error);
        validation.Value!.IssueRows.Should().Be(0);
        return batchId;
    }

    private Task<int> InitialBalancesAsync() =>
        QueryAsync(db => db.SalesReceivables.IgnoreQueryFilters()
            .CountAsync(r => r.TenantId == _tenant && r.Origin == SalesReceivableOrigin.InitialBalance));

    private Task<int> InstallmentsAsync() =>
        QueryAsync(db => db.SalesReceivableInstallments.IgnoreQueryFilters().CountAsync(i => i.TenantId == _tenant));

    private Task<int> OutboxAsync() =>
        QueryAsync(db => db.OutboxMessages.IgnoreQueryFilters().CountAsync(o => o.TenantId == _tenant));

    private async Task AssertRolledBackAsync(Guid batchId, int outboxBefore)
    {
        (await InitialBalancesAsync()).Should().Be(0, "ninguna CxC parcial");
        (await InstallmentsAsync()).Should().Be(0, "ninguna cuota parcial");
        (await OutboxAsync()).Should().Be(outboxBefore, "sin Outbox parcial");
        var batch = await QueryAsync(db => db.ImportBatches.SingleAsync(b => b.Id == batchId));
        batch.Status.Should().Be(ImportStatus.Validated);
        batch.ImportedRows.Should().Be(0);
        (await QueryAsync(db => db.ImportBatchRows.CountAsync(r => r.ImportBatchId == batchId && r.IsImported)))
            .Should().Be(0);
    }

    [Fact]
    public async Task Confirma_una_fila_como_saldo_inicial_sin_factura()
    {
        var batchId = await ValidatedBatchAsync(Row("1790016919001", "001-001-000000123", "150.75"));

        var result = await SendAsync(new ConfirmImportBatchCommand(batchId));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Status.Should().Be(ImportStatus.Completed);
        result.Value.ImportedRows.Should().Be(1);
        var stored = await QueryAsync(db => db.SalesReceivables.Include(r => r.Installments)
            .SingleAsync(r => r.ImportBatchId == batchId));
        stored.Origin.Should().Be(SalesReceivableOrigin.InitialBalance);
        stored.InvoiceId.Should().BeNull();
        stored.CustomerId.Should().Be(_customer.Id);
        stored.DocumentNumber.Should().Be("001-001-000000123");
        stored.DocumentNumberNormalized.Should().Be("001001000000123");
        stored.IssueDate.Should().Be(new DateOnly(2026, 7, 15));
        stored.BranchId.Should().Be(_branch);
        stored.OriginalAmount.Should().Be(150.75m);
        stored.PaidAmount.Should().Be(0m);
        stored.Installments.Should().ContainSingle(i => i.DueDate == new DateOnly(2026, 9, 15) && i.Amount == 150.75m);
        (await QueryAsync(db => db.SalesInvoices.IgnoreQueryFilters().CountAsync(i => i.TenantId == _tenant)))
            .Should().Be(0, "nunca se crea una factura ficticia");
        (await QueryAsync(db => db.ImportBatchRows.SingleAsync(r => r.ImportBatchId == batchId)))
            .IsImported.Should().BeTrue();
    }

    [Fact]
    public async Task Confirma_varias_filas_en_una_transaccion()
    {
        var rows = Enumerable.Range(1, 201)
            .Select(i => Row("1790016919001", $"001-002-{i:D9}", $"{i}.50"))
            .ToArray();
        var batchId = await ValidatedBatchAsync(rows);

        var result = await SendAsync(new ConfirmImportBatchCommand(batchId));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.ImportedRows.Should().Be(201);
        (await InitialBalancesAsync()).Should().Be(201);
        (await InstallmentsAsync()).Should().Be(201);
        (await QueryAsync(db => db.SalesReceivables.Where(r => r.ImportBatchId == batchId).SumAsync(r => r.OriginalAmount)))
            .Should().Be(Enumerable.Range(1, 201).Sum(i => i + 0.50m));
        (await QueryAsync(db => db.SalesInvoices.IgnoreQueryFilters().CountAsync(i => i.TenantId == _tenant)))
            .Should().Be(0);
    }

    [Fact]
    public async Task Duplicado_aparecido_despues_del_preview_hace_rollback_total()
    {
        var batchId = await ValidatedBatchAsync(Row("1790016919001", "A-1"), Row("1790016919001", "A-2"));
        // Otro lote ya confirmó "a 2" para el mismo cliente después del preview.
        await SeedInitialBalanceAsync(_company, _branch, "a 2");
        var initialBefore = await InitialBalancesAsync();
        var outboxBefore = await OutboxAsync();

        var result = await SendAsync(new ConfirmImportBatchCommand(batchId));

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Fila 2").And.Contain("ya tiene una cuenta por cobrar");
        (await InitialBalancesAsync()).Should().Be(initialBefore, "ninguna CxC del lote se escribe");
        (await QueryAsync(db => db.SalesReceivables.CountAsync(r => r.ImportBatchId == batchId))).Should().Be(0);
        (await OutboxAsync()).Should().Be(outboxBefore);
        (await QueryAsync(db => db.ImportBatches.SingleAsync(b => b.Id == batchId))).Status
            .Should().Be(ImportStatus.Validated);
    }

    [Fact]
    public async Task Cliente_inactivado_despues_del_preview_hace_rollback_total()
    {
        var batchId = await ValidatedBatchAsync(Row("1790016919001", "A-1"));
        var outboxBefore = await OutboxAsync();
        await QueryAsync(db => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE master_business_partners SET is_active = false WHERE id = {_customer.Id}"));

        var result = await SendAsync(new ConfirmImportBatchCommand(batchId));

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("inactivado");
        await AssertRolledBackAsync(batchId, outboxBefore);
    }

    [Fact]
    public async Task Fallo_intermedio_al_escribir_hace_rollback_total()
    {
        var batchId = await ValidatedBatchAsync(
            Row("1790016919001", "A-1"), Row("1790016919001", "A-2"), Row("1790016919001", "A-3"));
        var outboxBefore = await OutboxAsync();
        _failOnAdd = 2;

        var result = await SendAsync(new ConfirmImportBatchCommand(batchId));

        _failOnAdd = null;
        result.IsSuccess.Should().BeFalse();
        await AssertRolledBackAsync(batchId, outboxBefore);
        // El lote sigue confirmable tras el fallo.
        var retry = await SendAsync(new ConfirmImportBatchCommand(batchId));
        retry.IsSuccess.Should().BeTrue(retry.Error);
        (await InitialBalancesAsync()).Should().Be(3);
    }

    [Fact]
    public async Task Listado_muestra_el_saldo_inicial_sin_depender_de_factura()
    {
        var batchId = await ValidatedBatchAsync(Row("1790016919001", "001-001-000000777", "80.00"));
        (await SendAsync(new ConfirmImportBatchCommand(batchId))).IsSuccess.Should().BeTrue();

        var list = await SendAsync(new GetReceivablesListQuery());

        list.IsSuccess.Should().BeTrue(list.Error);
        var row = list.Value!.Items.Should().ContainSingle().Subject;
        row.InvoiceId.Should().BeNull();
        row.InvoiceNumber.Should().Be("001-001-000000777");
        row.CustomerId.Should().Be(_customer.Id);
        row.CustomerName.Should().Be("Cliente Uno S.A.");
        row.CustomerIdentification.Should().Be("04-1790016919001");
        row.BranchId.Should().Be(_branch);
        row.BranchName.Should().Be("Sucursal B01");
        row.InvoiceIssuedAt.Should().Be(new DateOnly(2026, 7, 15));
        row.DueDate.Should().Be(new DateOnly(2026, 9, 15));
        row.OriginalAmount.Should().Be(80m);
        row.BalanceDue.Should().Be(80m);
        row.Installments.Should().ContainSingle();
    }

    [Fact]
    public async Task Saldo_inicial_confirmado_no_admite_la_cancelacion_generica()
    {
        var batchId = await ValidatedBatchAsync(Row("1790016919001", "A-1"));
        (await SendAsync(new ConfirmImportBatchCommand(batchId))).IsSuccess.Should().BeTrue();
        var stored = await QueryAsync(db => db.SalesReceivables.Include(r => r.Installments)
            .SingleAsync(r => r.ImportBatchId == batchId));

        var act = () => stored.Cancel(_user);

        act.Should().Throw<ERP.Domain.Exceptions.DomainRuleViolationException>();
    }

    /// <summary>Repositorio real que falla en la N-ésima alta para probar el rollback a mitad del lote.</summary>
    private sealed class FailingReceivableRepository(ISalesReceivableRepository inner, Func<int?> failOnAdd)
        : ISalesReceivableRepository
    {
        private int _adds;

        public async Task AddAsync(SalesReceivable receivable, CancellationToken ct = default)
        {
            if (++_adds == failOnAdd())
                throw new InvalidOperationException("Fallo inyectado a mitad del lote.");
            await inner.AddAsync(receivable, ct);
        }

        public Task<SalesReceivable?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
            inner.GetByIdAsync(tenantId, id, ct);

        public Task<IReadOnlyDictionary<Guid, SalesReceivable>> GetByIdsForUpdateAsync(
            Guid tenantId, IReadOnlyCollection<Guid> ids, CancellationToken ct = default) =>
            inner.GetByIdsForUpdateAsync(tenantId, ids, ct);

        public Task<SalesReceivable?> GetByInvoiceIdForUpdateAsync(Guid tenantId, Guid invoiceId, CancellationToken ct = default) =>
            inner.GetByInvoiceIdForUpdateAsync(tenantId, invoiceId, ct);

        public Task<SalesReceivable?> GetByInvoiceIdAsync(Guid tenantId, Guid invoiceId, CancellationToken ct = default) =>
            inner.GetByInvoiceIdAsync(tenantId, invoiceId, ct);

        public Task<(IReadOnlyList<SalesReceivable> Items, int Total)> GetPagedAsync(
            Guid tenantId, string? search, string? status, int page, int pageSize, CancellationToken ct = default) =>
            inner.GetPagedAsync(tenantId, search, status, page, pageSize, ct);

        public Task SaveChangesAsync(CancellationToken ct = default) => inner.SaveChangesAsync(ct);
    }
}
