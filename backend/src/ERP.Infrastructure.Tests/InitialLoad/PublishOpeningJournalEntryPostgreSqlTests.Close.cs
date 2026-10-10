using ERP.Application.Common;
using ERP.Application.Modules.InitialLoad;
using ERP.Application.Modules.InitialLoad.OpeningPosting;
using ERP.Application.Modules.InitialLoad.UseCases.OpeningBalanceDate;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.InitialLoad.Entities;
using ERP.Domain.Modules.InitialLoad.Enums;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Infrastructure.Tests.InitialLoad;

/// <summary>
/// IL-8E — cierre definitivo de la Carga Inicial sobre PostgreSQL real: cierre con conciliación sin
/// blockers, idempotencia, ASI vigente Posted obligatorio aun sin lotes, cada blocker (contabilización
/// pendiente, lote de saldos en curso, ASI reversado sin reemplazo, puente sin reclasificar) impide
/// cerrar sin dejar rastro, y después del cierre la fecha de apertura, IL-7B, IL-8A e IL-8B rechazan
/// cualquier cambio (INITIAL_LOAD_CLOSED) sin asiento, número ni versión nueva.
/// </summary>
public sealed partial class PublishOpeningJournalEntryPostgreSqlTests
{
    private Task<Result<InitialLoadClosureDto>> CloseAsync() => SendAsync(new CloseInitialLoadCommand());

    private Task<Result<OpeningBalanceReconciliationDto>> ReconcileAsync() =>
        SendAsync(new GetOpeningBalanceReconciliationQuery());

    private Task<Company> CompanyAsync() =>
        QueryAsync(db => db.Companies.AsNoTracking().SingleAsync(c => c.Id == _company));

    private async Task<OpeningJournalEntryPostingDto> ClosedAsync()
    {
        var published = await PublishedAsync();
        var closed = await CloseAsync();
        closed.IsSuccess.Should().BeTrue(closed.Error);
        return published;
    }

    /// <summary>Lote de saldos de CxC confirmado (Completed con su CxC inicial), sin contabilizar.</summary>
    private async Task<Guid> CompletedReceivablesBatchAsync(decimal amount = 20m)
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var batch = CompletedBatch(ImportType.InitialReceivables);
        db.ImportBatches.Add(batch);
        await db.SaveChangesAsync();
        db.SalesReceivables.Add(SalesReceivable.CreateInitialBalance(_tenant, _company, _branch, _partner,
            "FAC-" + Guid.NewGuid().ToString("N")[..6], Cutoff.AddDays(-5), Cutoff.AddDays(25), amount, batch.Id, _user));
        await db.SaveChangesAsync();
        return batch.Id;
    }

    /// <summary>Lote de saldos de CxP validado y aún sin confirmar (en curso).</summary>
    private async Task<Guid> ValidatedPayablesBatchAsync()
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var batch = ImportBatch.Create(_tenant, _company, ImportType.InitialPayables, _user, "CxP pendiente");
        batch.AttachFile("x.xlsx", "x.xlsx", 1, _user);
        batch.MarkUploaded(_user);
        batch.BeginValidating(_user);
        batch.CompleteValidation(1, 1, 0, 0, _user);
        db.ImportBatches.Add(batch);
        await db.SaveChangesAsync();
        return batch.Id;
    }

    private async Task ShouldNotCloseAsync(params string[] blockerCodes)
    {
        var result = await CloseAsync();

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(CloseInitialLoadCommandHandler.BlockedCode, result.Error);
        (await CompanyAsync()).IsInitialLoadClosed.Should().BeFalse("un cierre rechazado no deja rastro");
        var reconciliation = (await ReconcileAsync()).Value!;
        reconciliation.CanCloseImplementation.Should().BeFalse();
        reconciliation.IsClosed.Should().BeFalse();
        reconciliation.Blockers.Select(b => b.Code).Should().Contain(blockerCodes);
        foreach (var blocker in reconciliation.Blockers)
            result.Error.Should().Contain(blocker.Message);
    }

    private static void ShouldBeClosed<T>(Result<T> result)
    {
        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(InitialLoadClosedGuard.Code, result.Error);
        result.Error.Should().Be(Company.InitialLoadClosedMessage);
    }

    [Fact]
    public async Task Cierre_sin_blockers_es_definitivo_idempotente_y_visible_en_la_conciliacion()
    {
        await PublishedAsync();
        (await ReconcileAsync()).Value!.CanCloseImplementation.Should().BeTrue();

        var closed = await CloseAsync();

        closed.IsSuccess.Should().BeTrue(closed.Error);
        closed.Value!.AlreadyClosed.Should().BeFalse();
        closed.Value.ClosedBy.Should().Be(_user);
        var company = await CompanyAsync();
        company.InitialLoadClosedAt.Should().BeCloseTo(closed.Value.ClosedAt, TimeSpan.FromMilliseconds(1));
        company.InitialLoadClosedBy.Should().Be(_user);

        var again = await CloseAsync();
        again.IsSuccess.Should().BeTrue(again.Error);
        again.Value!.AlreadyClosed.Should().BeTrue();
        again.Value.ClosedAt.Should().Be(company.InitialLoadClosedAt!.Value, "el segundo cierre conserva el primero");

        var reconciliation = (await ReconcileAsync()).Value!;
        reconciliation.IsClosed.Should().BeTrue();
        reconciliation.ClosedAt.Should().Be(company.InitialLoadClosedAt);
        reconciliation.ClosedBy.Should().Be(_user);
        reconciliation.Blockers.Should().BeEmpty();
        reconciliation.CanCloseImplementation.Should().BeFalse("ya está cerrada");
        reconciliation.OpeningJournalEntry.State.Should().Be(OpeningJournalEntryState.Posted, "el historial no se toca");
    }

    [Fact]
    public async Task ASI_vigente_publicado_es_obligatorio_aun_sin_lotes()
    {
        (await ReconcileAsync()).Value!.Batches.Should().BeEmpty();

        await ShouldNotCloseAsync(GetOpeningBalanceReconciliationHandler.OpeningAsiMissingCode);
        (await ReconcileAsync()).Value!.Blockers.Should().ContainSingle();
    }

    [Theory]
    [InlineData("posting_pending")]
    [InlineData("batch_in_progress")]
    [InlineData("asi_reversed")]
    [InlineData("bridge_without_asi")]
    public async Task Cada_blocker_impide_cerrar(string scenario)
    {
        string[] expected;
        switch (scenario)
        {
            case "posting_pending":
                await PublishedAsync();
                await CompletedReceivablesBatchAsync();
                expected = ["OPENING_POSTING_PENDING"];
                break;
            case "batch_in_progress":
                await PublishedAsync();
                await ValidatedPayablesBatchAsync();
                expected = [GetOpeningBalanceReconciliationHandler.OpeningBatchInProgressCode];
                break;
            case "asi_reversed":
                var published = await PublishedAsync();
                (await ReverseAsync(published.Id)).IsSuccess.Should().BeTrue();
                expected =
                [
                    GetOpeningBalanceReconciliationHandler.OpeningAsiReversedNotReplacedCode,
                    GetOpeningBalanceReconciliationHandler.OpeningBridgeNotClearedCode,
                ];
                break;
            default:
                await LoadBridgeAsync();
                expected =
                [
                    GetOpeningBalanceReconciliationHandler.OpeningAsiMissingCode,
                    GetOpeningBalanceReconciliationHandler.OpeningBridgeNotClearedCode,
                ];
                break;
        }

        await ShouldNotCloseAsync(expected);
    }

    [Fact]
    public async Task Lote_en_curso_bloquea_hasta_cancelarlo()
    {
        await PublishedAsync();
        var batch = await ValidatedPayablesBatchAsync();
        await ShouldNotCloseAsync(GetOpeningBalanceReconciliationHandler.OpeningBatchInProgressCode);
        (await ReconcileAsync()).Value!.Blockers.Should().ContainSingle(b => b.ImportBatchId == batch);

        await using (var scope = _services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var tracked = await db.ImportBatches.SingleAsync(b => b.Id == batch);
            tracked.Cancel(_user);
            await db.SaveChangesAsync();
        }

        (await CloseAsync()).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Despues_del_cierre_fecha_IL7B_IL8A_e_IL8B_rechazan_sin_dejar_rastro()
    {
        var published = await ClosedAsync();
        var entriesBefore = await AsiEntriesAsync();
        var sequencesBefore = (await SequencesAsync()).Sum();

        // Fecha de apertura: definitiva, con el motivo del cierre.
        var date = await SendAsync(new SetOpeningBalanceDateCommand(Cutoff.AddDays(-1)));
        date.IsSuccess.Should().BeFalse();
        date.Error.Should().Be(Company.InitialLoadClosedMessage);
        var dateDto = (await SendAsync(new GetOpeningBalanceDateQuery())).Value!;
        dateDto.IsLocked.Should().BeTrue();
        dateDto.LockReason.Should().Be(Company.InitialLoadClosedMessage);
        (await CompanyAsync()).OpeningBalanceDate.Should().Be(Cutoff);

        // IL-7B: un lote confirmado tarde no se contabiliza.
        var late = await CompletedReceivablesBatchAsync();
        ShouldBeClosed(await SendAsync(new PostOpeningBalanceCommand(late)));
        (await QueryAsync(db => db.OpeningBalancePostings.AnyAsync(p => p.ImportBatchId == late)))
            .Should().BeFalse("el rechazo no registra estado contable");

        // IL-8A: ni publicación ni reintento.
        ShouldBeClosed(await PublishAsync(await ValidLinesAsync()));

        // IL-8B: ni reverso.
        ShouldBeClosed(await ReverseAsync(published.Id));

        (await AsiEntriesAsync()).Should().Be(entriesBefore);
        (await SequencesAsync()).Sum().Should().Be(sequencesBefore, "ningún número consumido");
        (await VersionsAsync()).Should().ContainSingle(v => v.IsCurrent && v.Status == OpeningBalancePostingStatus.Posted);
        (await CompanyAsync()).IsInitialLoadClosed.Should().BeTrue();
    }
}
