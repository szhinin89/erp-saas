using System.Data.Common;
using ERP.Application.Common;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Application.Modules.InitialLoad.Processors;
using ERP.Application.Modules.InitialLoad.UseCases.ConfirmImportBatch;
using ERP.Application.Modules.InitialLoad.UseCases.ValidateImportBatch;
using ERP.Domain.Modules.InitialLoad.Enums;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Npgsql;

namespace ERP.Infrastructure.Tests.InitialLoad;

public sealed partial class ConfirmItemsAtomicPostgreSqlTests
{
    private Action? _onLockAttempt;
    private bool _failStaging;

    private sealed class BatchLockObserver(ConfirmItemsAtomicPostgreSqlTests test) : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("import_batches", StringComparison.Ordinal)
                && command.CommandText.Contains("FOR UPDATE", StringComparison.Ordinal)) test._onLockAttempt?.Invoke();
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    private async Task LockAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid batch)
    {
        await using var command = new NpgsqlCommand(
            "SELECT 1 FROM import_batches WHERE id=@id AND tenant_id=@tenant AND company_id=@company FOR UPDATE", connection, transaction);
        command.Parameters.AddWithValue("id", batch);
        command.Parameters.AddWithValue("tenant", _tenant);
        command.Parameters.AddWithValue("company", _company);
        await command.ExecuteNonQueryAsync();
    }

    private void ReadRows(params string[] skus)
    {
        var rows = skus.Select(sku => (IReadOnlyDictionary<string, string?>)new Dictionary<string, string?>
        {
            [ItemImportColumns.Sku] = sku, [ItemImportColumns.Name] = sku,
            [ItemImportColumns.ItemTypeCode] = "Physical", [ItemImportColumns.UomCode] = "19",
            [ItemImportColumns.CategoryName] = "Existing Category", [ItemImportColumns.BrandName] = "Existing Brand",
            [ItemImportColumns.Barcode1] = "BC-" + sku, [ItemImportColumns.BarcodeType1] = "Internal",
            [ItemImportColumns.AvailableOnPos] = "SI", [ItemImportColumns.Pvp] = "10",
        }).ToArray();
        Mock.Get(_services.GetRequiredService<IItemImportSheetReader>())
            .Setup(x => x.ReadAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ImportReadResult(rows));
    }

    private async Task<Result<ImportBatchDto>> ValidateAsync(Guid batch, CancellationToken ct = default)
    {
        await using var scope = _services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new ValidateImportBatchCommand(batch), ct);
    }

    [Fact]
    public async Task Confirmaciones_concurrentes_y_retry_post_commit_ejecutan_una_sola_vez()
    {
        var batch = await BatchAsync(true, Row("CONCURRENT", "New Category", "New Brand"));
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await LockAsync(connection, transaction, batch);
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = 0;
        _onLockAttempt = () => { if (Interlocked.Increment(ref attempts) == 2) entered.TrySetResult(true); };
        var first = ConfirmAsync(batch);
        var second = ConfirmAsync(batch);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
        first.IsCompleted.Should().BeFalse(); second.IsCompleted.Should().BeFalse();
        await transaction.CommitAsync();
        var results = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(30));
        results.Should().OnlyContain(x => x.IsSuccess);
        results[0].Value.Should().Be(results[1].Value);
        int outboxCount;
        await using (var committedScope = _services.CreateAsyncScope())
            outboxCount = await committedScope.ServiceProvider.GetRequiredService<ErpDbContext>().OutboxMessages.CountAsync();
        // Simulates a client discarding the committed response and retrying with a new request/context.
        var retry = await ConfirmAsync(batch);
        retry.Value.Should().Be(results[0].Value);
        _createdDuringConfirmation.Should().Be(1);
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        (await db.OutboxMessages.CountAsync()).Should().Be(outboxCount);
        (await db.Items.CountAsync()).Should().Be(1);
        (await db.ItemVariantBarcodes.CountAsync()).Should().Be(1);
        (await db.Set<ERP.Domain.Modules.Items.Entities.ItemCategoryNode>().CountAsync()).Should().Be(2);
        (await db.Set<ERP.Domain.Modules.Items.Entities.Brand>().CountAsync()).Should().Be(2);
        (await db.ImportBatchRows.CountAsync(x => x.ImportBatchId == batch && x.IsImported)).Should().Be(1);
    }

    [Fact]
    public async Task Cancelacion_mientras_espera_lock_no_deja_confirming_y_retry_funciona()
    {
        var batch = await BatchAsync(false, Row("CANCEL-LOCK"));
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await LockAsync(connection, transaction, batch);
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _onLockAttempt = () => entered.TrySetResult(true);
        using var cts = new CancellationTokenSource();
        await using var request = _services.CreateAsyncScope();
        var pending = request.ServiceProvider.GetRequiredService<IMediator>().Send(new ConfirmImportBatchCommand(batch), cts.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
        cts.Cancel();
        await FluentActions.Awaiting(async () => await pending).Should().ThrowAsync<OperationCanceledException>();
        await transaction.CommitAsync();
        (await ConfirmAsync(batch)).IsSuccess.Should().BeTrue();
        _createdDuringConfirmation.Should().Be(1);
    }

    [Fact]
    public async Task Revalidacion_reemplaza_filas_e_incidencias_sin_duplicarlas()
    {
        var batch = await BatchAsync(false, Row("OLD"));
        ReadRows("DUP", "DUP");
        (await ValidateAsync(batch)).Value!.IssueRows.Should().Be(2);
        Guid[] previousRows;
        await using (var scope = _services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            previousRows = await db.ImportBatchRows.Where(x => x.ImportBatchId == batch).Select(x => x.Id).ToArrayAsync();
            (await db.ImportBatchIssues.CountAsync(x => x.ImportBatchId == batch)).Should().BeGreaterThan(0);
        }
        ReadRows("NEW");
        var revalidated = await ValidateAsync(batch);
        revalidated.IsSuccess.Should().BeTrue(revalidated.Error);
        revalidated.Value!.TotalRows.Should().Be(1);
        revalidated.Value.IssueRows.Should().Be(0);
        await using var check = _services.CreateAsyncScope();
        var persisted = check.ServiceProvider.GetRequiredService<ErpDbContext>();
        (await persisted.ImportBatchRows.CountAsync(x => x.ImportBatchId == batch)).Should().Be(1);
        (await persisted.ImportBatchRows.CountAsync(x => previousRows.Contains(x.Id))).Should().Be(0);
        (await persisted.ImportBatchIssues.CountAsync(x => x.ImportBatchId == batch)).Should().Be(0);
        (await persisted.Items.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Revalidacion_falla_o_se_cancela_tras_delete_y_restaura_staging(bool cancel)
    {
        var batch = await BatchAsync(false, Row("OLD"));
        ReadRows("DUP", "DUP");
        (await ValidateAsync(batch)).IsSuccess.Should().BeTrue();
        Guid[] rowIds; Guid[] issueIds;
        await using (var scope = _services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            rowIds = await db.ImportBatchRows.Where(x => x.ImportBatchId == batch).Select(x => x.Id).ToArrayAsync();
            issueIds = await db.ImportBatchIssues.Where(x => x.ImportBatchId == batch).Select(x => x.Id).ToArrayAsync();
        }
        ReadRows("NEW"); _failStaging = true; _cancel = cancel;
        if (cancel) await FluentActions.Awaiting(() => ValidateAsync(batch)).Should().ThrowAsync<OperationCanceledException>();
        else (await ValidateAsync(batch)).IsSuccess.Should().BeFalse();
        await using (var scope = _services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            (await db.ImportBatches.SingleAsync(x => x.Id == batch)).Status.Should().Be(ImportStatus.Validated);
            (await db.ImportBatchRows.Where(x => x.ImportBatchId == batch).Select(x => x.Id).ToArrayAsync()).Should().BeEquivalentTo(rowIds);
            (await db.ImportBatchIssues.Where(x => x.ImportBatchId == batch).Select(x => x.Id).ToArrayAsync()).Should().BeEquivalentTo(issueIds);
        }
        _failStaging = false; _cancel = false;
        (await ValidateAsync(batch)).Value!.IssueRows.Should().Be(0);
    }

    [Fact]
    public async Task Revalidaciones_concurrentes_serializan_el_reemplazo()
    {
        var batch = await BatchAsync(false, Row("OLD")); ReadRows("NEW1", "NEW2");
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(); await using var transaction = await connection.BeginTransactionAsync();
        await LockAsync(connection, transaction, batch);
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously); var attempts = 0;
        _onLockAttempt = () => { if (Interlocked.Increment(ref attempts) == 2) entered.TrySetResult(true); };
        var first = ValidateAsync(batch); var second = ValidateAsync(batch);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
        await transaction.CommitAsync();
        (await Task.WhenAll(first, second)).Should().OnlyContain(x => x.IsSuccess);
        await using var scope = _services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        (await db.ImportBatchRows.CountAsync(x => x.ImportBatchId == batch)).Should().Be(2);
        (await db.ImportBatchIssues.CountAsync(x => x.ImportBatchId == batch)).Should().Be(0);
    }

    [Fact]
    public async Task Confirmacion_y_revalidacion_concurrentes_no_mezclan_staging()
    {
        var batch = await BatchAsync(false, Row("SAME")); ReadRows("SAME");
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(); await using var transaction = await connection.BeginTransactionAsync();
        await LockAsync(connection, transaction, batch);
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously); var attempts = 0;
        _onLockAttempt = () => { if (Interlocked.Increment(ref attempts) == 2) entered.TrySetResult(true); };
        var confirm = ConfirmAsync(batch); var validate = ValidateAsync(batch);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
        await transaction.CommitAsync();
        (await confirm).IsSuccess.Should().BeTrue();
        await validate;
        // Either validation finished before confirmation, or it observed Completed and rejected.
        (await ValidateAsync(batch)).IsSuccess.Should().BeFalse();
        await using var scope = _services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        (await db.ImportBatches.SingleAsync(x => x.Id == batch)).Status.Should().Be(ImportStatus.Completed);
        (await db.ImportBatchRows.CountAsync(x => x.ImportBatchId == batch && x.IsImported)).Should().Be(1);
        (await db.ImportBatchRows.CountAsync(x => x.ImportBatchId == batch)).Should().Be(1);
        (await db.Items.CountAsync()).Should().Be(1);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("failure")]
    [InlineData("cancel")]
    public async Task Primera_validacion_fallida_conserva_uploaded_y_puede_reintentarse(string kind)
    {
        Guid batchId;
        await using (var scope = _services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var batch = ERP.Domain.Modules.InitialLoad.Entities.ImportBatch.Create(_tenant, _company, ImportType.Items, _user);
            batch.AttachFile("test.xlsx", "test.xlsx", 1, _user); batch.MarkUploaded(_user);
            db.ImportBatches.Add(batch); await db.SaveChangesAsync(); batchId = batch.Id;
        }
        var reader = Mock.Get(_services.GetRequiredService<IItemImportSheetReader>());
        var files = Mock.Get(_services.GetRequiredService<ERP.Application.Common.Interfaces.IFileStorage>());
        if (kind == "missing") files.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Stream?)null);
        else if (kind == "cancel") reader.Setup(x => x.ReadAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>())).ThrowsAsync(new OperationCanceledException());
        else reader.Setup(x => x.ReadAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("Injected"));
        if (kind == "cancel") await FluentActions.Awaiting(() => ValidateAsync(batchId)).Should().ThrowAsync<OperationCanceledException>();
        else (await ValidateAsync(batchId)).IsSuccess.Should().BeFalse();
        await using (var scope = _services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            (await db.ImportBatches.SingleAsync(x => x.Id == batchId)).Status.Should().Be(ImportStatus.Uploaded);
            (await db.ImportBatchRows.CountAsync(x => x.ImportBatchId == batchId)).Should().Be(0);
        }
        files.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => new MemoryStream([1]));
        ReadRows("RETRY");
        (await ValidateAsync(batchId)).Value!.ValidRows.Should().Be(1);
    }

    [Fact]
    public async Task Lote_201_filas_falla_acotado_retry_completa_y_retry_post_commit_no_duplica()
    {
        var rows = Enumerable.Range(1, 200).Select(i => Row("PAGE-" + i)).Append(Row("FAIL")).ToArray();
        var batch = await BatchAsync(false, rows);
        ReadRows(Enumerable.Range(1, 200).Select(i => "PAGE-" + i).Append("FAIL").ToArray());
        var preview = await ValidateAsync(batch);
        preview.IsSuccess.Should().BeTrue(preview.Error);
        preview.Value!.TotalRows.Should().Be(201); preview.Value.IssueRows.Should().Be(0);
        _failureSku = "FAIL";
        (await ConfirmAsync(batch).WaitAsync(TimeSpan.FromSeconds(30))).IsSuccess.Should().BeFalse();
        await AssertRolledBackAsync(batch);
        _failureSku = null; _createdDuringConfirmation = 0;
        var committed = await ConfirmAsync(batch);
        committed.IsSuccess.Should().BeTrue(committed.Error); committed.Value!.ImportedRows.Should().Be(201);
        (await ConfirmAsync(batch)).Value.Should().Be(committed.Value);
        _createdDuringConfirmation.Should().Be(201);
        await using var scope = _services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        (await db.Items.CountAsync()).Should().Be(201); (await db.ItemVariantBarcodes.CountAsync()).Should().Be(201);
        (await db.ImportBatchRows.CountAsync(x => x.ImportBatchId == batch && x.IsImported)).Should().Be(201);
    }

    [Fact]
    public async Task Reimportar_otro_lote_es_create_only_y_completado_no_admite_revalidacion()
    {
        var first = await BatchAsync(false, Row("EXISTING"));
        (await ConfirmAsync(first)).IsSuccess.Should().BeTrue();
        ReadRows("EXISTING");
        (await ValidateAsync(first)).IsSuccess.Should().BeFalse();
        var second = await BatchAsync(false, Row("NEW"));
        var preview = await ValidateAsync(second);
        preview.IsSuccess.Should().BeTrue(preview.Error); preview.Value!.IssueRows.Should().Be(1);
        (await ConfirmAsync(second)).IsSuccess.Should().BeFalse();
        await using var scope = _services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        (await db.Items.CountAsync()).Should().Be(1);
        (await db.ImportBatches.SingleAsync(x => x.Id == first)).Status.Should().Be(ImportStatus.Completed);
        (await db.ImportBatchRows.CountAsync(x => x.ImportBatchId == first && x.IsImported)).Should().Be(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Confirmacion_y_revalidacion_no_acceden_a_otro_scope(bool otherTenant)
    {
        var batch = await BatchAsync(false, Row("SCOPED"));
        var tenant = _tenant; var company = _company;
        if (otherTenant) _tenant = Guid.NewGuid(); else _company = Guid.NewGuid();
        try
        {
            (await ConfirmAsync(batch)).IsSuccess.Should().BeFalse();
            (await ValidateAsync(batch)).IsSuccess.Should().BeFalse();
        }
        finally { _tenant = tenant; _company = company; }
        await using var scope = _services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        (await db.ImportBatches.SingleAsync(x => x.Id == batch)).Status.Should().Be(ImportStatus.Validated);
        (await db.ImportBatchRows.CountAsync(x => x.ImportBatchId == batch)).Should().Be(1);
        (await db.Items.CountAsync()).Should().Be(0);
    }
}
