using System.Data.Common;
using ERP.Application.Common;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Application.Modules.InitialLoad.Processors;
using ERP.Application.Modules.InitialLoad.UseCases.CancelImportBatch;
using ERP.Application.Modules.InitialLoad.UseCases.ConfirmImportBatch;
using ERP.Application.Modules.InitialLoad.UseCases.ValidateImportBatch;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.Modules.InitialLoad.Entities;
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

/// <summary>
/// IL-5C — CxC Inicial sobre PostgreSQL real con el mismo bloqueo/idempotencia/reemplazo de
/// staging/alcance de Items, Terceros e Inventario Inicial: un lote se valida, confirma o cancela
/// bajo FOR UPDATE; nunca deja Validating/Confirming commiteado; una confirmación repetida o
/// concurrente devuelve el resultado ya confirmado sin crear CxC, cuotas ni Outbox; nunca queda
/// Cancelled con CxC creadas; y cualquier fila invalidada revierte todo sin el bucle legacy.
/// </summary>
public sealed partial class ValidateInitialReceivablesPostgreSqlTests
{
    private Action? _onLockAttempt;

    private sealed class BatchLockObserver(ValidateInitialReceivablesPostgreSqlTests test) : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("import_batches", StringComparison.Ordinal)
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

    private void ReadRows(params Dictionary<string, string?>[] rows) =>
        _reader.Setup(x => x.ReadAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImportReadResult(rows.Select(r => (IReadOnlyDictionary<string, string?>)r).ToList()));

    private Task<Result<ImportBatchDto>> ValidateAsync(Guid batch, CancellationToken ct = default) =>
        SendAsync(new ValidateImportBatchCommand(batch), ct);

    private Task<Result<ImportBatchConfirmResultDto>> ConfirmAsync(Guid batch, CancellationToken ct = default) =>
        SendAsync(new ConfirmImportBatchCommand(batch), ct);

    private Task<Result<bool>> CancelAsync(Guid batch) => SendAsync(new CancelImportBatchCommand(batch));

    private async Task<Result<TResult>> SendAsync<TResult>(IRequest<Result<TResult>> request, CancellationToken ct)
    {
        await using var scope = _services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(request, ct);
    }

    private Task<ImportBatch> ReloadAsync(Guid batch) =>
        QueryAsync(db => db.ImportBatches.AsNoTracking().SingleAsync(b => b.Id == batch));

    private async Task<Guid[]> StagingIdsAsync(Guid batch) =>
        (await QueryAsync(db => db.ImportBatchRows.Where(r => r.ImportBatchId == batch).Select(r => r.Id).ToListAsync()))
        .Concat(await QueryAsync(db => db.ImportBatchIssues.Where(i => i.ImportBatchId == batch).Select(i => i.Id).ToListAsync()))
        .ToArray();

    /// <summary>CxC InitialBalance, cuotas y Outbox del tenant: lo que una confirmación escribe.</summary>
    private async Task<(int Receivables, int Installments, int Outbox)> EffectsAsync() =>
        (await InitialBalancesAsync(), await InstallmentsAsync(), await OutboxAsync());

    private static Dictionary<string, string?> Ok(int i) => Row("1790016919001", $"IL5C-{i:D4}", "10.00");

    private async Task<Guid> ReadyBatchAsync(int rows)
    {
        var batch = await UploadedBatchAsync(Enumerable.Range(1, rows).Select(Ok).ToArray());
        var validation = await ValidateAsync(batch);
        validation.IsSuccess.Should().BeTrue(validation.Error);
        validation.Value!.IssueRows.Should().Be(0);
        return batch;
    }

    [Fact]
    public async Task Confirmaciones_concurrentes_y_retry_post_commit_ejecutan_una_sola_vez()
    {
        var batch = await ReadyBatchAsync(2);
        var outbox = await OutboxAsync();
        var (connection, transaction) = await HoldLockAsync(batch);
        var entered = WaitForLockAttempts(2);

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
        (await EffectsAsync()).Should().Be((2, 2, outbox), "una sola ejecución efectiva");

        // Cliente que perdió la respuesta ya commiteada y reintenta con request/contexto nuevos.
        var retry = await ConfirmAsync(batch);

        retry.Value.Should().Be(results[0].Value);
        (await EffectsAsync()).Should().Be((2, 2, outbox), "el retry no crea CxC, cuotas ni Outbox");
    }

    [Fact]
    public async Task Cancelacion_mientras_espera_lock_no_deja_confirming_y_retry_funciona()
    {
        var batch = await ReadyBatchAsync(1);
        var (connection, transaction) = await HoldLockAsync(batch);
        var entered = WaitForLockAttempts(1);
        using var cts = new CancellationTokenSource();
        var pending = ConfirmAsync(batch, cts.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(15));

        cts.Cancel();

        await FluentActions.Awaiting(async () => await pending).Should().ThrowAsync<OperationCanceledException>();
        await transaction.CommitAsync();
        await connection.DisposeAsync();
        (await ReloadAsync(batch)).Status.Should().Be(ImportStatus.Validated);
        (await EffectsAsync()).Receivables.Should().Be(0);
        (await ConfirmAsync(batch)).Value!.Status.Should().Be(ImportStatus.Completed);
        (await EffectsAsync()).Receivables.Should().Be(1);
    }

    [Fact]
    public async Task Cancelar_y_confirmar_concurrentes_nunca_cancelan_un_lote_con_CxC_creadas()
    {
        var batch = await ReadyBatchAsync(1);
        var (connection, transaction) = await HoldLockAsync(batch);
        var entered = WaitForLockAttempts(2);

        var confirm = ConfirmAsync(batch);
        var cancel = CancelAsync(batch);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
        await transaction.CommitAsync();
        await connection.DisposeAsync();
        await Task.WhenAll(confirm, cancel).WaitAsync(TimeSpan.FromSeconds(30));

        var final = await ReloadAsync(batch);
        var receivables = await QueryAsync(db => db.SalesReceivables.CountAsync(r => r.ImportBatchId == batch));
        if (final.Status == ImportStatus.Completed)
        {
            cancel.Result.IsSuccess.Should().BeFalse("un lote confirmado no puede cancelarse");
            receivables.Should().Be(1);
        }
        else
        {
            final.Status.Should().Be(ImportStatus.Cancelled);
            confirm.Result.IsSuccess.Should().BeFalse();
            receivables.Should().Be(0, "un lote cancelado no tiene CxC creadas");
        }
    }

    [Fact]
    public async Task Fallo_antes_del_commit_no_deja_escrituras_parciales_y_el_retry_confirma()
    {
        var batch = await ReadyBatchAsync(3);
        var outbox = await OutboxAsync();
        _failOnAdd = 3;

        var failed = await ConfirmAsync(batch);

        _failOnAdd = null;
        failed.IsSuccess.Should().BeFalse();
        (await ReloadAsync(batch)).Status.Should().Be(ImportStatus.Validated, "nunca queda Confirming commiteado");
        (await EffectsAsync()).Should().Be((0, 0, outbox));
        (await QueryAsync(db => db.ImportBatchRows.CountAsync(r => r.ImportBatchId == batch && r.IsImported))).Should().Be(0);

        var retry = await ConfirmAsync(batch);

        retry.Value!.Status.Should().Be(ImportStatus.Completed);
        (await EffectsAsync()).Should().Be((3, 3, outbox));
    }

    [Fact]
    public async Task Revalidacion_reemplaza_staging_sin_duplicar_y_Completed_no_se_revalida()
    {
        var batch = await UploadedBatchAsync(Ok(1), Ok(1), Row("1790000000000", "X-1"));
        var first = await ValidateAsync(batch);
        first.IsSuccess.Should().BeTrue(first.Error);
        first.Value!.IssueRows.Should().Be(3);
        var firstIds = await StagingIdsAsync(batch);

        (await ValidateAsync(batch)).Value!.TotalRows.Should().Be(3);
        var again = await StagingIdsAsync(batch);
        again.Should().HaveCount(firstIds.Length, "revalidar el mismo archivo no duplica filas ni incidencias");
        again.Should().NotIntersectWith(firstIds, "el staging previo se reemplaza");

        ReadRows(Ok(1), Ok(2));
        var replaced = await ValidateAsync(batch);
        replaced.Value!.TotalRows.Should().Be(2);
        replaced.Value.IssueRows.Should().Be(0);

        (await ConfirmAsync(batch)).IsSuccess.Should().BeTrue();
        (await QueryAsync(db => db.SalesReceivables.CountAsync(r => r.ImportBatchId == batch))).Should().Be(2);
        (await ValidateAsync(batch)).IsSuccess.Should().BeFalse("un lote completado no admite revalidación");
        (await QueryAsync(db => db.ImportBatchRows.CountAsync(r => r.ImportBatchId == batch))).Should().Be(2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Revalidacion_que_falla_o_se_cancela_conserva_staging_y_estado(bool cancel)
    {
        var batch = await UploadedBatchAsync(Ok(1), Row("1790000000000", "X-1"));
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
        ReadRows(Ok(1));
        (await ValidateAsync(batch)).Value!.IssueRows.Should().Be(0);
    }

    [Fact]
    public async Task Primera_validacion_cancelada_conserva_uploaded_y_puede_reintentarse()
    {
        var batch = await UploadedBatchAsync(Ok(1));
        _reader.Setup(x => x.ReadAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        await FluentActions.Awaiting(() => ValidateAsync(batch)).Should().ThrowAsync<OperationCanceledException>();

        (await ReloadAsync(batch)).Status.Should().Be(ImportStatus.Uploaded);
        (await StagingIdsAsync(batch)).Should().BeEmpty();
        ReadRows(Ok(1));
        (await ValidateAsync(batch)).Value!.Status.Should().Be(ImportStatus.Validated);
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("company")]
    [InlineData("branch")]
    public async Task Otro_tenant_company_o_sucursal_no_valida_confirma_ni_cancela(string scope)
    {
        var batch = await ReadyBatchAsync(1);
        var staging = await StagingIdsAsync(batch);
        var (tenant, company, branch) = (_tenant, _company, _branch);
        if (scope == "branch") _branch = _otherBranch;
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
        (await EffectsAsync()).Receivables.Should().Be(0);
    }

    [Fact]
    public async Task Lote_de_201_filas_confirma_completo_sin_bucle_legacy()
    {
        var batch = await ReadyBatchAsync(201);

        var result = await ConfirmAsync(batch).WaitAsync(TimeSpan.FromMinutes(2));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.ImportedRows.Should().Be(201);
        (await EffectsAsync()).Receivables.Should().Be(201);
        (await QueryAsync(db => db.ImportBatchIssues.CountAsync(i => i.ImportBatchId == batch && i.Code == "CONFIRM_FAILED")))
            .Should().Be(0, "sin incidencias CONFIRM_FAILED del bucle legacy");
    }

    [Fact]
    public async Task Fila_201_invalidada_antes_de_confirmar_revierte_todo_sin_bucle()
    {
        // La fila 201 es de un segundo cliente (el tercero sin rol recibe el rol Cliente), que se
        // inactiva después del preview.
        var lastCustomer = _noRole;
        await using (var seed = _services.CreateAsyncScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<ErpDbContext>();
            db.BusinessPartnerRoles.Add(BusinessPartnerRole.Create(_tenant, lastCustomer.Id, RoleType.Customer, _user));
            await db.SaveChangesAsync();
        }
        var rows = Enumerable.Range(1, 200).Select(Ok).Append(Row("1791352688001", "IL5C-LAST")).ToArray();
        var batch = await UploadedBatchAsync(rows);
        (await ValidateAsync(batch)).Value!.IssueRows.Should().Be(0);
        var outbox = await OutboxAsync();
        await QueryAsync(db => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE master_business_partners SET is_active = false WHERE id = {lastCustomer.Id}"));

        var result = await ConfirmAsync(batch).WaitAsync(TimeSpan.FromMinutes(2));

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Fila 201").And.Contain("No se registró ningún saldo inicial");
        (await EffectsAsync()).Should().Be((0, 0, outbox));
        (await ReloadAsync(batch)).Status.Should().Be(ImportStatus.Validated);
        (await QueryAsync(db => db.ImportBatchIssues.CountAsync(i => i.ImportBatchId == batch && i.Code == "CONFIRM_FAILED")))
            .Should().Be(0, "sin incidencias CONFIRM_FAILED del bucle legacy");
        (await ConfirmAsync(batch).WaitAsync(TimeSpan.FromMinutes(2))).IsSuccess.Should().BeFalse("el retry falla igual, sin bucle");
        (await EffectsAsync()).Receivables.Should().Be(0);
    }
}
