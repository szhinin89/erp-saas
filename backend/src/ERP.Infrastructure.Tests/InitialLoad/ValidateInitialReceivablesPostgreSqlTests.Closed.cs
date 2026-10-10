using ERP.Application.Modules.InitialLoad;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.InitialLoad.Enums;
using FluentAssertions;
using Npgsql;

namespace ERP.Infrastructure.Tests.InitialLoad;

/// <summary>
/// IL-8E — tras el cierre definitivo de la Carga Inicial, validar y confirmar un lote de saldos de CxC
/// se rechazan sin escribir CxC, cuotas ni Outbox; y la confirmación toma el bloqueo de la empresa
/// ANTES que el del lote, así una confirmación que espera detrás de un cierre en curso ve el cierre
/// ya confirmado y no registra nada (carrera confirmar lote vs cerrar).
/// </summary>
public sealed partial class ValidateInitialReceivablesPostgreSqlTests
{
    /// <summary>
    /// Transacción de cierre abierta: el mismo <c>FOR UPDATE</c> de la empresa que toma
    /// <c>CloseInitialLoadCommand</c> y el cierre ya escrito, sin confirmar todavía.
    /// </summary>
    private async Task<(NpgsqlConnection Connection, NpgsqlTransaction Transaction)> ClosingTransactionAsync()
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        var transaction = await connection.BeginTransactionAsync();
        await using var command = new NpgsqlCommand(
            "SELECT 1 FROM company WHERE id=@company AND tenant_id=@tenant FOR UPDATE; "
                + "UPDATE company SET initial_load_closed_at = now(), initial_load_closed_by = @user "
                + "WHERE id=@company AND tenant_id=@tenant",
            connection, transaction);
        command.Parameters.AddWithValue("company", _company);
        command.Parameters.AddWithValue("tenant", _tenant);
        command.Parameters.AddWithValue("user", _user);
        await command.ExecuteNonQueryAsync();
        return (connection, transaction);
    }

    private async Task CloseInitialLoadAsync()
    {
        var (connection, transaction) = await ClosingTransactionAsync();
        await transaction.CommitAsync();
        await connection.DisposeAsync();
    }

    [Fact]
    public async Task Tras_el_cierre_validar_y_confirmar_se_rechazan_sin_registrar_saldos()
    {
        var ready = await ReadyBatchAsync(1);
        var uploaded = await UploadedBatchAsync(Ok(2));
        var effects = await EffectsAsync();
        await CloseInitialLoadAsync();

        var confirm = await ConfirmAsync(ready);
        var validate = await ValidateAsync(uploaded);

        foreach (var (success, code, error) in new[]
                 {
                     (confirm.IsSuccess, confirm.Code, confirm.Error),
                     (validate.IsSuccess, validate.Code, validate.Error),
                 })
        {
            success.Should().BeFalse();
            code.Should().Be(InitialLoadClosedGuard.Code, error);
            error.Should().Be(Company.InitialLoadClosedMessage);
        }
        (await ReloadAsync(ready)).Status.Should().Be(ImportStatus.Validated, "el rechazo no deja Confirming");
        (await ReloadAsync(uploaded)).Status.Should().Be(ImportStatus.Uploaded);
        (await EffectsAsync()).Should().Be(effects, "ninguna CxC, cuota ni Outbox");
    }

    [Fact]
    public async Task Confirmar_que_espera_detras_de_un_cierre_en_curso_ve_el_cierre_y_no_registra_nada()
    {
        var batch = await ReadyBatchAsync(1);
        var effects = await EffectsAsync();
        var (connection, transaction) = await ClosingTransactionAsync();
        var entered = WaitForLockAttempts(1);

        var pending = ConfirmAsync(batch);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
        await Task.Delay(200);
        pending.IsCompleted.Should().BeFalse("la confirmación espera el bloqueo de la empresa");
        await transaction.CommitAsync();
        await connection.DisposeAsync();
        var result = await pending.WaitAsync(TimeSpan.FromSeconds(30));

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(InitialLoadClosedGuard.Code, result.Error);
        (await ReloadAsync(batch)).Status.Should().Be(ImportStatus.Validated);
        (await EffectsAsync()).Should().Be(effects);
    }
}
