using System.Data.Common;
using ERP.Application.Common;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Application.Modules.InitialLoad.Processors;
using ERP.Application.Modules.InitialLoad.UseCases.CancelImportBatch;
using ERP.Application.Modules.InitialLoad.UseCases.ConfirmImportBatch;
using ERP.Application.Modules.InitialLoad.UseCases.ValidateImportBatch;
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
/// IL-2C — mismo bloqueo/idempotencia/reemplazo de staging probado en Items (IL-1C), sobre
/// Clientes: un lote se valida, confirma o cancela bajo FOR UPDATE, nunca deja Validating/Confirming
/// commiteado y una confirmación repetida devuelve el resultado ya confirmado.
/// </summary>
public sealed partial class ConfirmCustomersAtomicPostgreSqlTests
{
    private sealed class BatchLockObserver(ConfirmCustomersAtomicPostgreSqlTests test) : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("import_batches", StringComparison.Ordinal)
                && command.CommandText.Contains("FOR UPDATE", StringComparison.Ordinal)) test._onLockAttempt?.Invoke();
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    /// <summary>Retiene el lote desde otra conexión hasta que el test decida liberarlo.</summary>
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

    private static Dictionary<string, string?> Raw(string type, string number, string? term = "CONTADO") => new()
    {
        [CustomerImportColumns.IdentificationType] = type,
        [CustomerImportColumns.IdentificationNumber] = number,
        [CustomerImportColumns.LegalEntityTypeCode] = type == "06" ? "1" : null,
        [CustomerImportColumns.LegalName] = "Cliente " + number,
        [CustomerImportColumns.PaymentTermCode] = term,
    };

    private void ReadRows(params Dictionary<string, string?>[] rows) =>
        _reader.Setup(x => x.ReadAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImportReadResult(rows.Select(r => (IReadOnlyDictionary<string, string?>)r).ToList()));

    private async Task<Guid> UploadedBatchAsync()
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var batch = ImportBatch.Create(_tenant, _company, ImportType.Customers, _user);
        batch.AttachFile("clientes.xlsx", "clientes.xlsx", 1, _user);
        batch.MarkUploaded(_user);
        db.ImportBatches.Add(batch);
        await db.SaveChangesAsync();
        _baseline = await CountsAsync();
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

    private async Task<ImportBatch> ReloadAsync(Guid batch)
    {
        await using var scope = _services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ErpDbContext>().ImportBatches.AsNoTracking()
            .SingleAsync(b => b.Id == batch);
    }

    private async Task<(int Rows, int Issues)> StagingAsync(Guid batch)
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        return (await db.ImportBatchRows.CountAsync(r => r.ImportBatchId == batch),
            await db.ImportBatchIssues.CountAsync(i => i.ImportBatchId == batch));
    }

    [Fact]
    public async Task Confirmaciones_concurrentes_y_retry_post_commit_ejecutan_una_sola_vez()
    {
        // Mezcla las tres semánticas IL-2A: cliente nuevo, BP sin rol y BP ya Cliente.
        var batch = await BatchAsync(NewCustomer("PAS0401"), AssignRole(), AlreadyCustomer());
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
        var afterConcurrent = await CountsAsync();
        afterConcurrent.Partners.Should().Be(_baseline.Partners + 1);
        afterConcurrent.CustomerRoles.Should().Be(_baseline.CustomerRoles + 2);
        afterConcurrent.Settings.Should().Be(_baseline.Settings + 3);

        // Cliente que perdió la respuesta ya commiteada y reintenta con request/contexto nuevos.
        var retry = await ConfirmAsync(batch);

        retry.IsSuccess.Should().BeTrue();
        retry.Value.Should().Be(results[0].Value);
        (await CountsAsync()).Should().Be(afterConcurrent, "el retry no re-ejecuta ninguna escritura");
    }

    [Fact]
    public async Task Cancelacion_mientras_espera_lock_no_deja_confirming_y_retry_funciona()
    {
        var batch = await BatchAsync(NewCustomer("PAS0501"));
        var (connection, transaction) = await HoldLockAsync(batch);
        var entered = WaitForLockAttempts(1);
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
        (await CountsAsync()).Should().Be(_baseline);
        (await ConfirmAsync(batch)).Value!.Status.Should().Be(ImportStatus.Completed);
        (await CountsAsync()).Partners.Should().Be(_baseline.Partners + 1);
    }

    [Fact]
    public async Task Cancelar_y_confirmar_concurrentes_nunca_cancelan_un_lote_importado()
    {
        var batch = await BatchAsync(NewCustomer("PAS0601"));
        var (connection, transaction) = await HoldLockAsync(batch);
        var entered = WaitForLockAttempts(2);

        var confirm = ConfirmAsync(batch);
        var cancel = CancelAsync(batch);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
        await transaction.CommitAsync();
        await connection.DisposeAsync();
        await Task.WhenAll(confirm, cancel).WaitAsync(TimeSpan.FromSeconds(30));

        var final = await ReloadAsync(batch);
        var partners = (await CountsAsync()).Partners;
        if (final.Status == ImportStatus.Completed)
        {
            cancel.Result.IsSuccess.Should().BeFalse("un lote confirmado no puede cancelarse");
            partners.Should().Be(_baseline.Partners + 1);
        }
        else
        {
            final.Status.Should().Be(ImportStatus.Cancelled);
            confirm.Result.IsSuccess.Should().BeFalse();
            partners.Should().Be(_baseline.Partners, "un lote cancelado no importa nada");
        }
    }

    [Fact]
    public async Task Revalidacion_reemplaza_filas_e_incidencias_sin_duplicarlas()
    {
        var batch = await UploadedBatchAsync();
        ReadRows(Raw("06", "PAS0701"), Raw("06", "pas0701"), Raw("06", "PAS0702", term: null));
        var first = await ValidateAsync(batch);
        first.IsSuccess.Should().BeTrue(first.Error);
        first.Value!.IssueRows.Should().Be(3);
        var (rows, issues) = await StagingAsync(batch);
        rows.Should().Be(3);
        issues.Should().BeGreaterThan(0);

        (await ValidateAsync(batch)).Value!.TotalRows.Should().Be(3);
        (await StagingAsync(batch)).Should().Be((rows, issues), "revalidar el mismo archivo no duplica staging");

        ReadRows(Raw("06", "PAS0703"), Raw("04", ExistingRuc), Raw("04", CustomerRuc));
        var replaced = await ValidateAsync(batch);
        replaced.IsSuccess.Should().BeTrue(replaced.Error);
        replaced.Value!.TotalRows.Should().Be(3);
        replaced.Value.IssueRows.Should().Be(0);
        (await StagingAsync(batch)).Issues.Should().Be(2, "solo quedan las advertencias de los BP existentes");

        var confirmed = await ConfirmAsync(batch);
        confirmed.IsSuccess.Should().BeTrue(confirmed.Error);
        var after = await CountsAsync();
        after.Partners.Should().Be(_baseline.Partners + 1, "BP existentes se reutilizan; solo PAS0703 es nuevo");
        after.CustomerRoles.Should().Be(_baseline.CustomerRoles + 2);
        (await ValidateAsync(batch)).IsSuccess.Should().BeFalse("un lote completado no admite revalidación");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Revalidacion_que_falla_o_se_cancela_restaura_staging_y_no_deja_validating(bool cancel)
    {
        var batch = await UploadedBatchAsync();
        ReadRows(Raw("06", "PAS0801"), Raw("06", "PAS0802", term: null));
        (await ValidateAsync(batch)).IsSuccess.Should().BeTrue();
        var staging = await StagingAsync(batch);
        Exception failure = cancel ? new OperationCanceledException() : new InvalidOperationException("disco");
        _reader.Setup(x => x.ReadAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>())).ThrowsAsync(failure);

        if (cancel)
            await FluentActions.Awaiting(() => ValidateAsync(batch)).Should().ThrowAsync<OperationCanceledException>();
        else
            (await ValidateAsync(batch)).IsSuccess.Should().BeFalse();

        (await ReloadAsync(batch)).Status.Should().Be(ImportStatus.Validated);
        (await StagingAsync(batch)).Should().Be(staging);
        ReadRows(Raw("06", "PAS0803"));
        (await ValidateAsync(batch)).Value!.IssueRows.Should().Be(0);
        (await StagingAsync(batch)).Rows.Should().Be(1);
    }

    [Fact]
    public async Task Primera_validacion_cancelada_conserva_uploaded_y_puede_reintentarse()
    {
        var batch = await UploadedBatchAsync();
        _reader.Setup(x => x.ReadAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        await FluentActions.Awaiting(() => ValidateAsync(batch)).Should().ThrowAsync<OperationCanceledException>();

        (await ReloadAsync(batch)).Status.Should().Be(ImportStatus.Uploaded);
        (await StagingAsync(batch)).Should().Be((0, 0));
        ReadRows(Raw("06", "PAS0901"));
        (await ValidateAsync(batch)).Value!.Status.Should().Be(ImportStatus.Validated);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Validar_confirmar_y_cancelar_no_acceden_a_otro_scope(bool otherTenant)
    {
        var batch = await BatchAsync(NewCustomer("PAS1001"));
        var (tenant, company) = (_tenant, _company);
        if (otherTenant) _tenant = Guid.NewGuid();
        else _company = Guid.NewGuid();
        try
        {
            (await ConfirmAsync(batch)).IsSuccess.Should().BeFalse();
            (await ValidateAsync(batch)).IsSuccess.Should().BeFalse();
            (await CancelAsync(batch)).IsSuccess.Should().BeFalse();
        }
        finally
        {
            (_tenant, _company) = (tenant, company);
        }

        var untouched = await ReloadAsync(batch);
        untouched.Status.Should().Be(ImportStatus.Validated);
        (await CountsAsync()).Should().Be(_baseline);
        (await StagingAsync(batch)).Rows.Should().Be(1);
    }

    [Fact]
    public async Task Reimportar_en_otro_lote_reutiliza_el_cliente_sin_duplicar()
    {
        var first = await BatchAsync(NewCustomer("PAS1101"));
        (await ConfirmAsync(first)).IsSuccess.Should().BeTrue();
        var afterFirst = await CountsAsync();

        var second = await UploadedBatchAsync();
        ReadRows(Raw("06", "pas1101"));
        var validated = await ValidateAsync(second);
        validated.Value!.IssueRows.Should().Be(0);
        var confirmed = await ConfirmAsync(second);

        confirmed.IsSuccess.Should().BeTrue(confirmed.Error);
        var after = await CountsAsync();
        after.Partners.Should().Be(afterFirst.Partners);
        after.CustomerRoles.Should().Be(afterFirst.CustomerRoles);
        after.Settings.Should().Be(afterFirst.Settings, "misma condición: ya Cliente es idempotente");
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        (await db.BusinessPartnerRoles.CountAsync(r => r.RoleType == RoleType.Customer)).Should().Be(after.CustomerRoles);
    }
}
