using System.Data.Common;
using ERP.Application.Common;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Application.Modules.InitialLoad.Processors;
using ERP.Application.Modules.InitialLoad.UseCases.CancelImportBatch;
using ERP.Application.Modules.InitialLoad.UseCases.ConfirmImportBatch;
using ERP.Application.Modules.InitialLoad.UseCases.ValidateImportBatch;
using ERP.Domain.Modules.InitialLoad.Entities;
using ERP.Domain.Modules.InitialLoad.Enums;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.ValueObjects;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Npgsql;
using BranchEntity = ERP.Domain.Branches.Entities.Branch;

namespace ERP.Infrastructure.Tests.InitialLoad;

/// <summary>
/// IL-4C — mismo bloqueo/idempotencia/reemplazo de staging probado en Items/Clientes/Proveedores,
/// sobre Inventario Inicial, más el alcance de sucursal derivado del staging: un lote se valida,
/// confirma o cancela bajo FOR UPDATE, nunca deja Validating/Confirming commiteado, una
/// confirmación repetida devuelve el resultado ya confirmado y nunca queda Cancelled con
/// inventario posteado.
/// </summary>
public sealed partial class ConfirmInitialStockAtomicPostgreSqlTests
{
    private sealed class BatchLockObserver(ConfirmInitialStockAtomicPostgreSqlTests test) : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            // IL-8E — la confirmación de un lote de saldos bloquea la empresa y luego el lote: ambos
            // cuentan como intento de bloqueo (dos confirmaciones = empresa + lote + empresa).
            if ((command.CommandText.Contains("import_batches", StringComparison.Ordinal)
                    || command.CommandText.Contains("FROM company", StringComparison.Ordinal))
                && command.CommandText.Contains("FOR UPDATE", StringComparison.Ordinal)) test._onLockAttempt?.Invoke();
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    private async Task<(NpgsqlConnection Connection, NpgsqlTransaction Transaction)> HoldLockAsync(Guid batch)
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        var transaction = await connection.BeginTransactionAsync();
        await using var command = new NpgsqlCommand(
            "SELECT 1 FROM import_batches WHERE id=@id AND tenant_id=@tenant AND company_id=@company FOR UPDATE",
            connection, transaction);
        command.Parameters.AddWithValue("id", batch);
        command.Parameters.AddWithValue("tenant", _tenant);
        command.Parameters.AddWithValue("company", _company);
        await command.ExecuteNonQueryAsync();
        return (connection, transaction);
    }

    private TaskCompletionSource<bool> WaitForLockAttempts(int expected)
    {
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = 0;
        _onLockAttempt = () => { if (Interlocked.Increment(ref attempts) == expected) entered.TrySetResult(true); };
        return entered;
    }

    private static Dictionary<string, string?> Raw(string sku, string warehouseCode, string quantity = "10",
        string cutoff = "2026-08-31") => new()
    {
        [InitialStockImportColumns.Sku] = sku,
        [InitialStockImportColumns.Barcode] = null,
        [InitialStockImportColumns.WarehouseCode] = warehouseCode,
        [InitialStockImportColumns.Quantity] = quantity,
        [InitialStockImportColumns.UnitCost] = "2.50",
        [InitialStockImportColumns.CutoffDate] = cutoff,
        [InitialStockImportColumns.Observation] = null,
    };

    private void ReadRows(params Dictionary<string, string?>[] rows) =>
        _reader.Setup(x => x.ReadAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImportReadResult(rows.Select(r => (IReadOnlyDictionary<string, string?>)r).ToList()));

    private async Task<Guid> UploadedBatchAsync()
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var batch = ImportBatch.Create(_tenant, _company, ImportType.InitialStock, _user);
        batch.AttachFile("stock.xlsx", "stock.xlsx", 1, _user);
        batch.MarkUploaded(_user);
        db.ImportBatches.Add(batch);
        await db.SaveChangesAsync();
        _outboxBaseline = await db.OutboxMessages.CountAsync(o => o.TenantId == _tenant);
        return batch.Id;
    }

    private async Task<Result<ImportBatchDto>> ValidateAsync(Guid batch, CancellationToken ct = default)
    {
        await using var scope = _services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new ValidateImportBatchCommand(batch), ct);
    }

    private async Task<Result<bool>> CancelAsync(Guid batch)
    {
        await using var scope = _services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new CancelImportBatchCommand(batch));
    }

    private Task<ImportBatch> ReloadAsync(Guid batch) =>
        QueryAsync(db => db.ImportBatches.AsNoTracking().SingleAsync(b => b.Id == batch));

    private async Task<Guid[]> StagingIdsAsync(Guid batch) =>
        (await QueryAsync(db => db.ImportBatchRows.Where(r => r.ImportBatchId == batch).Select(r => r.Id).ToListAsync()))
        .Concat(await QueryAsync(db => db.ImportBatchIssues.Where(i => i.ImportBatchId == batch).Select(i => i.Id).ToListAsync()))
        .ToArray();

    private Task<(int Documents, int Movements, decimal Stock)> InventoryAsync() =>
        QueryAsync(async db => (
            await db.StockAdjustments.CountAsync(),
            await db.StockMovements.CountAsync(m => m.MovementType == StockMovementType.InitialBalance),
            await db.CurrentStocks.SumAsync(s => s.Quantity)));

    [Fact]
    public async Task Confirmaciones_concurrentes_y_retry_post_commit_ejecutan_una_sola_vez()
    {
        var batch = await BatchAsync(Row(_itemA, _wh1, 10m, 2m), Row(_itemB, _wh2, 4m, 1m));
        var (connection, transaction) = await HoldLockAsync(batch);
        var entered = WaitForLockAttempts(3);

        var first = ConfirmAsync(batch);
        var second = ConfirmAsync(batch);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
        first.IsCompleted.Should().BeFalse();
        second.IsCompleted.Should().BeFalse();
        await transaction.CommitAsync();
        await connection.DisposeAsync();
        var results = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(30));

        results.Should().OnlyContain(r => r.IsSuccess);
        results[0].Value.Should().Be(results[1].Value);
        results[0].Value!.Status.Should().Be(ImportStatus.Completed);
        (await InventoryAsync()).Should().Be((2, 2, 14m));
        var outbox = await QueryAsync(db => db.OutboxMessages.CountAsync(o => o.TenantId == _tenant));

        // Cliente que perdió la respuesta ya commiteada y reintenta con request/contexto nuevos.
        var retry = await ConfirmAsync(batch);

        retry.Value.Should().Be(results[0].Value);
        (await InventoryAsync()).Should().Be((2, 2, 14m), "el retry no crea documentos, movimientos ni stock");
        (await QueryAsync(db => db.OutboxMessages.CountAsync(o => o.TenantId == _tenant))).Should().Be(outbox);
    }

    [Fact]
    public async Task Cancelacion_mientras_espera_lock_no_deja_confirming_y_retry_funciona()
    {
        var batch = await BatchAsync(Row(_itemA, _wh1, 10m, 2m));
        var (connection, transaction) = await HoldLockAsync(batch);
        var entered = WaitForLockAttempts(2);
        using var cts = new CancellationTokenSource();
        await using var request = _services.CreateAsyncScope();
        var pending = request.ServiceProvider.GetRequiredService<IMediator>()
            .Send(new ConfirmImportBatchCommand(batch), cts.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(15));

        cts.Cancel();

        await FluentActions.Awaiting(async () => await pending).Should().ThrowAsync<OperationCanceledException>();
        await transaction.CommitAsync();
        await connection.DisposeAsync();
        (await ReloadAsync(batch)).Status.Should().Be(ImportStatus.Validated);
        (await InventoryAsync()).Should().Be((0, 0, 0m));
        (await ConfirmAsync(batch)).Value!.Status.Should().Be(ImportStatus.Completed);
        (await InventoryAsync()).Should().Be((1, 1, 10m));
    }

    [Fact]
    public async Task Cancelar_y_confirmar_concurrentes_nunca_cancelan_un_lote_con_inventario_posteado()
    {
        var batch = await BatchAsync(Row(_itemA, _wh1, 10m, 2m));
        var (connection, transaction) = await HoldLockAsync(batch);
        var entered = WaitForLockAttempts(3);

        var confirm = ConfirmAsync(batch);
        var cancel = CancelAsync(batch);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
        await transaction.CommitAsync();
        await connection.DisposeAsync();
        await Task.WhenAll(confirm, cancel).WaitAsync(TimeSpan.FromSeconds(30));

        var final = await ReloadAsync(batch);
        var inventory = await InventoryAsync();
        if (final.Status == ImportStatus.Completed)
        {
            cancel.Result.IsSuccess.Should().BeFalse("un lote confirmado no puede cancelarse");
            inventory.Should().Be((1, 1, 10m));
        }
        else
        {
            final.Status.Should().Be(ImportStatus.Cancelled);
            confirm.Result.IsSuccess.Should().BeFalse();
            inventory.Should().Be((0, 0, 0m), "un lote cancelado no tiene inventario posteado");
        }
    }

    [Fact]
    public async Task Revalidacion_reemplaza_staging_sin_duplicar_y_Completed_no_se_revalida()
    {
        var batch = await UploadedBatchAsync();
        ReadRows(Raw("IL4B-A", "BOD-01"), Raw("IL4B-A", "BOD-01"), Raw("IL4B-B", "BOD-99"));
        var first = await ValidateAsync(batch);
        first.IsSuccess.Should().BeTrue(first.Error);
        first.Value!.IssueRows.Should().Be(3);
        var firstIds = await StagingIdsAsync(batch);

        (await ValidateAsync(batch)).Value!.TotalRows.Should().Be(3);
        var again = await StagingIdsAsync(batch);
        again.Should().HaveCount(firstIds.Length, "revalidar el mismo archivo no duplica filas ni incidencias");
        again.Should().NotIntersectWith(firstIds, "el staging previo se reemplaza");

        ReadRows(Raw("IL4B-A", "BOD-01"), Raw("IL4B-B", "BOD-02"));
        var replaced = await ValidateAsync(batch);
        replaced.Value!.TotalRows.Should().Be(2);
        replaced.Value.IssueRows.Should().Be(0);

        (await ConfirmAsync(batch)).IsSuccess.Should().BeTrue();
        (await InventoryAsync()).Should().Be((2, 2, 20m));
        (await ValidateAsync(batch)).IsSuccess.Should().BeFalse("un lote completado no admite revalidación");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Revalidacion_que_falla_o_se_cancela_conserva_staging_y_estado(bool cancel)
    {
        var batch = await UploadedBatchAsync();
        ReadRows(Raw("IL4B-A", "BOD-01"), Raw("NOEXISTE", "BOD-01"));
        (await ValidateAsync(batch)).IsSuccess.Should().BeTrue();
        var staging = await StagingIdsAsync(batch);
        Exception failure = cancel ? new OperationCanceledException() : new InvalidOperationException("disco");
        _reader.Setup(x => x.ReadAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>())).ThrowsAsync(failure);

        if (cancel)
            await FluentActions.Awaiting(() => ValidateAsync(batch)).Should().ThrowAsync<OperationCanceledException>();
        else
            (await ValidateAsync(batch)).IsSuccess.Should().BeFalse();

        (await ReloadAsync(batch)).Status.Should().Be(ImportStatus.Validated);
        (await StagingIdsAsync(batch)).Should().BeEquivalentTo(staging, "el staging anterior queda intacto");
        ReadRows(Raw("IL4B-A", "BOD-01"));
        (await ValidateAsync(batch)).Value!.IssueRows.Should().Be(0);
    }

    [Fact]
    public async Task Primera_validacion_cancelada_conserva_uploaded_y_puede_reintentarse()
    {
        var batch = await UploadedBatchAsync();
        _reader.Setup(x => x.ReadAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        await FluentActions.Awaiting(() => ValidateAsync(batch)).Should().ThrowAsync<OperationCanceledException>();

        (await ReloadAsync(batch)).Status.Should().Be(ImportStatus.Uploaded);
        (await StagingIdsAsync(batch)).Should().BeEmpty();
        ReadRows(Raw("IL4B-A", "BOD-01"));
        (await ValidateAsync(batch)).Value!.Status.Should().Be(ImportStatus.Validated);
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("company")]
    [InlineData("branch")]
    public async Task Otro_tenant_company_o_sucursal_no_valida_confirma_ni_cancela(string scope)
    {
        var batch = await UploadedBatchAsync();
        ReadRows(Raw("IL4B-A", "BOD-01"));
        (await ValidateAsync(batch)).Value!.IssueRows.Should().Be(0);
        var staging = await StagingIdsAsync(batch);
        var (tenant, company, branch) = (_tenant, _company, _branch);
        if (scope == "branch")
        {
            await using var seed = _services.CreateAsyncScope();
            var db = seed.ServiceProvider.GetRequiredService<ErpDbContext>();
            var other = BranchEntity.Create(tenantId: _tenant, name: "Sucursal 2", address: "Av. 2", code: "B02",
                description: null, reference: null, postalCode: null, phone: null, secondaryPhone: null, email: null,
                website: null, managerName: null, managerPosition: null, managerEmail: null, managerPhone: null,
                countryId: null, provinceId: null, cantonId: null, parishId: null, latitude: null, longitude: null,
                openingDate: null, internalNotes: null, isMainBranch: false, createdBy: _user, companyId: _company);
            db.Branches.Add(other);
            await db.SaveChangesAsync();
            _branch = other.Id;
        }
        else if (scope == "tenant") _tenant = Guid.NewGuid();
        else _company = Guid.NewGuid();
        try
        {
            (await ConfirmAsync(batch)).IsSuccess.Should().BeFalse();
            (await ValidateAsync(batch)).IsSuccess.Should().BeFalse();
            (await CancelAsync(batch)).IsSuccess.Should().BeFalse();
        }
        finally
        {
            (_tenant, _company, _branch) = (tenant, company, branch);
        }

        (await ReloadAsync(batch)).Status.Should().Be(ImportStatus.Validated);
        (await StagingIdsAsync(batch)).Should().BeEquivalentTo(staging);
        (await InventoryAsync()).Should().Be((0, 0, 0m));
    }

    private async Task<List<Item>> SeedItemsAsync(int count, string prefix)
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var type = await db.Set<ItemTypeDefinition>().SingleAsync(t => t.TenantId == _tenant);
        var items = Enumerable.Range(1, count).Select(i => Item.Create(_tenant, $"{prefix}-{i:D3}", $"Producto {prefix}-{i:D3}",
            "Producto", type.Id, "UNIT", ItemTaxConfig.Create(saleVatCode: "4", purchaseVatCode: "4"),
            ItemSaleConfig.Create(isForSale: true), ItemStockConfig.Create(stockControlEnabled: true), _user,
            companyId: _company)).ToList();
        db.Set<Item>().AddRange(items);
        await db.SaveChangesAsync();
        return items;
    }

    [Fact]
    public async Task Lote_de_201_filas_confirma_completo_sin_bucle_legacy()
    {
        var items = await SeedItemsAsync(201, "OK");
        var batch = await UploadedBatchAsync();
        ReadRows(items.Select(i => Raw(i.Code.SKU, "BOD-01")).ToArray());
        (await ValidateAsync(batch)).Value!.IssueRows.Should().Be(0);

        var result = await ConfirmAsync(batch).WaitAsync(TimeSpan.FromMinutes(2));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.ImportedRows.Should().Be(201);
        (await InventoryAsync()).Should().Be((1, 201, 2010m), "un documento para la bodega, 201 líneas de apertura");
    }

    [Fact]
    public async Task Fila_201_invalidada_antes_de_confirmar_revierte_todo_sin_bucle()
    {
        var items = await SeedItemsAsync(201, "RB");
        var batch = await UploadedBatchAsync();
        ReadRows(items.Select(i => Raw(i.Code.SKU, i == items[^1] ? "BOD-02" : "BOD-01")).ToArray());
        (await ValidateAsync(batch)).Value!.IssueRows.Should().Be(0);
        // Después del preview, la fila 201 deja de ser válida: su bodega se inactiva.
        await using (var scope = _services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var wh = await db.Warehouses.SingleAsync(w => w.Id == _wh2.Id);
            wh.Disable(_user);
            await db.SaveChangesAsync();
        }

        var result = await ConfirmAsync(batch).WaitAsync(TimeSpan.FromMinutes(2));

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Fila 201").And.Contain("No se registró ningún saldo inicial");
        (await InventoryAsync()).Should().Be((0, 0, 0m));
        (await ReloadAsync(batch)).Status.Should().Be(ImportStatus.Validated);
        (await QueryAsync(db => db.ImportBatchIssues.CountAsync(i => i.ImportBatchId == batch && i.Code == "CONFIRM_FAILED"))).Should().Be(0,
            "sin incidencias CONFIRM_FAILED del bucle legacy");
        (await ConfirmAsync(batch).WaitAsync(TimeSpan.FromMinutes(2))).IsSuccess.Should().BeFalse("el retry falla igual, sin bucle");
    }
}
