using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Continuity;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.Expenses.Enums;
using ERP.Domain.Modules.Retentions.Entities;
using ERP.Infrastructure.Tests.TestData;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Infrastructure.Tests.Expenses;

/// <summary>
/// Regresión — RetentionElectronicRecoveryJob finalizaba la anulación SRI del gasto pero dejaba los
/// reversos contables Pending sin intento inicial (el job no pasa por AccountingPostingDispatchBehavior).
/// Camino del job tal cual: scope propio, usuario Guid.Empty, SIN ThenDispatchAsync; SRI simulado.
/// </summary>
public sealed partial class RetentionExpenseEndToEndTests
{
    private async Task<Result<RetentionAnnulmentRequest>> ExpenseJobVerifyAsync(
        Guid requestId,
        Action<IServiceCollection>? configure = null
    )
    {
        var (db, _) = BuildWiredContext(configure);
        await using (db)
            return await ExpenseAnnulments(db)
                .VerifyWithSriAsync(_tenantId, _companyId, requestId, Guid.Empty);
    }

    private async Task<Result<RetentionAnnulmentRequest>> ExpenseJobFinalizeAsync(Guid requestId)
    {
        var (db, _) = BuildWiredContext();
        await using (db)
            return await ExpenseAnnulments(db)
                .FinalizeAsync(_tenantId, _companyId, requestId, Guid.Empty);
    }

    /// <summary>Reversos del asiento del gasto y del de su retención: Posted, un intento, un asiento cada uno.</summary>
    private async Task AssertExpenseReversalsPostedOnceAsync(Guid expenseId, Guid retentionId)
    {
        await using var db = CreateContext();
        var originals = await db
            .JournalEntries.AsNoTracking()
            .Where(e =>
                (e.SourceModule == "Expenses" && e.SourceEventId == expenseId)
                || (e.SourceModule == "Retentions" && e.SourceEventId == retentionId)
            )
            .Select(e => e.Id)
            .ToListAsync();
        originals.Should().HaveCount(2);
        var reversals = await db
            .AccountingPostings.AsNoTracking()
            .Where(p => p.FactType == "Reversal" && originals.Contains(p.SourceEventId))
            .ToListAsync();
        reversals.Should().HaveCount(2, "se revierten el asiento del gasto y el de la retención");
        reversals
            .Should()
            .OnlyContain(p =>
                p.Status == AccountingPostingStatus.Posted
                && p.Attempts == 1
                && p.JournalEntryId != null
            );
        (
            await db
                .JournalEntries.AsNoTracking()
                .CountAsync(e => e.SourceEventType == "Reversal" && originals.Contains(e.SourceEventId))
        )
            .Should()
            .Be(2);
    }

    /// <summary>Obligaciones de reverso de los asientos del gasto y de su retención, y sus asientos de reverso.</summary>
    private async Task<(List<AccountingPosting> Postings, int Entries)> ExpenseReversalsAsync(
        Guid expenseId,
        Guid retentionId
    )
    {
        await using var db = CreateContext();
        var originals = await db
            .JournalEntries.AsNoTracking()
            .Where(e =>
                (e.SourceModule == "Expenses" && e.SourceEventId == expenseId)
                || (e.SourceModule == "Retentions" && e.SourceEventId == retentionId)
            )
            .Select(e => e.Id)
            .ToListAsync();
        var postings = await db
            .AccountingPostings.AsNoTracking()
            .Where(p => p.FactType == "Reversal" && originals.Contains(p.SourceEventId))
            .ToListAsync();
        var entries = await db
            .JournalEntries.AsNoTracking()
            .CountAsync(e => e.SourceEventType == "Reversal" && originals.Contains(e.SourceEventId));
        return (postings, entries);
    }

    private async Task<RetentionAnnulmentRequest> RequestAndSubmitExpenseAnnulmentAsync(string number)
    {
        var (_, _, request) = await RequestExpenseAnnulmentAsync(number);
        var (submitDb, _) = BuildWiredContext();
        await using (submitDb)
            (
                await ExpenseAnnulments(submitDb)
                    .MarkSubmittedAsync(
                        _tenantId,
                        _companyId,
                        request.Id,
                        new DateOnly(2026, 8, 20),
                        null,
                        null,
                        _createdBy
                    )
            )
                .IsSuccess.Should()
                .BeTrue();
        return request;
    }

    [Fact]
    public async Task Job_Gasto_con_fallo_contable_conserva_la_anulacion_y_deja_los_reversos_Failed_recuperables()
    {
        var request = await RequestAndSubmitExpenseAnnulmentAsync("RETQA-JOB2");
        var expenseId = request.SourceDocumentId;
        var retentionId = request.RetentionDocumentId;
        _expenseSriStatus.FiscalStatus = SriFiscalStatus.Annulled;

        // Falla interna del intento contable posterior al commit: solo el reverso numera asientos aquí.
        var result = await ExpenseJobVerifyAsync(
            request.Id,
            services =>
                services.AddScoped<IJournalEntrySequenceRepository>(_ =>
                    new Moq.Mock<IJournalEntrySequenceRepository>(Moq.MockBehavior.Strict).Object
                )
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Code.Should().Be(ApiResponseCodes.Retentions.AnnulmentFinalized);
        await using (var verify = CreateContext())
        {
            (await verify.ExpenseDocuments.AsNoTracking().SingleAsync(x => x.Id == expenseId))
                .Status.Should()
                .Be(ExpenseStatus.Cancelled, "un error contable no revierte la anulación operativa");
            (await verify.RetentionAnnulmentRequests.AsNoTracking().SingleAsync(x => x.Id == request.Id))
                .FinalizedAtUtc.Should()
                .NotBeNull();
        }
        (await ReadRetentionElectronicAsync(retentionId))!
            .CurrentState.Should()
            .Be(ElectronicDocumentState.Cancelled);
        var (failed, failedEntries) = await ExpenseReversalsAsync(expenseId, retentionId);
        failed.Should().HaveCount(2, "se revierten el asiento del gasto y el de la retención");
        failed
            .Should()
            .OnlyContain(p =>
                p.Status == AccountingPostingStatus.Failed
                && p.Attempts == 1
                && p.ErrorCode == AccountingPostingExecutor.InternalErrorCode
                && p.JournalEntryId == null
            );
        failedEntries.Should().Be(0, "sin asiento de reverso mientras la obligación siga Failed");

        // Sin reintento automático: corridas siguientes del job no agregan intentos.
        for (var run = 0; run < 2; run++)
        {
            await ExpenseJobVerifyAsync(request.Id);
            await ExpenseJobFinalizeAsync(request.Id);
        }
        var (afterJob, afterJobEntries) = await ExpenseReversalsAsync(expenseId, retentionId);
        afterJob
            .Should()
            .OnlyContain(p => p.Status == AccountingPostingStatus.Failed && p.Attempts == 1);
        afterJobEntries.Should().Be(0);

        // Reproceso manual (dos veces): contabiliza una sola vez.
        for (var pass = 0; pass < 2; pass++)
            foreach (var posting in failed)
            {
                var (db, _) = BuildWiredContext();
                await using (db)
                    (
                        await AccountingContinuityTestWiring.ReprocessAsync(
                            db,
                            posting.Id,
                            "Reproceso tras falla interna"
                        )
                    )
                        .Status.Should()
                        .Be(AccountingPostingStatus.Posted);
            }
        var (posted, postedEntries) = await ExpenseReversalsAsync(expenseId, retentionId);
        posted
            .Should()
            .OnlyContain(p =>
                p.Status == AccountingPostingStatus.Posted && p.Attempts == 2 && p.JournalEntryId != null
            );
        postedEntries.Should().Be(2);
        await AssertRetentionReversedOnceAsync(expenseId);
    }

    [Fact]
    public async Task Job_Gasto_ANULADO_anula_el_gasto_y_contabiliza_sus_reversos_sin_duplicar_al_repetir()
    {
        var (expenseId, retentionId, request) = await RequestExpenseAnnulmentAsync("RETQA-JOB1");
        var (submitDb, _) = BuildWiredContext();
        await using (submitDb)
            (
                await ExpenseAnnulments(submitDb)
                    .MarkSubmittedAsync(
                        _tenantId,
                        _companyId,
                        request.Id,
                        new DateOnly(2026, 8, 20),
                        null,
                        null,
                        _createdBy
                    )
            )
                .IsSuccess.Should()
                .BeTrue();
        _expenseSriStatus.FiscalStatus = SriFiscalStatus.Annulled;

        var result = await ExpenseJobVerifyAsync(request.Id);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Code.Should().Be(ApiResponseCodes.Retentions.AnnulmentFinalized);
        await using (var verify = CreateContext())
            (await verify.ExpenseDocuments.AsNoTracking().SingleAsync(x => x.Id == expenseId))
                .Status.Should()
                .Be(ExpenseStatus.Cancelled);
        (await ReadRetentionElectronicAsync(retentionId))!
            .CurrentState.Should()
            .Be(ElectronicDocumentState.Cancelled);
        await AssertRetentionReversedOnceAsync(expenseId);
        await AssertExpenseReversalsPostedOnceAsync(expenseId, retentionId);

        // Corridas siguientes del job: idempotentes, sin nuevos reversos ni intentos.
        for (var run = 0; run < 2; run++)
        {
            (await ExpenseJobVerifyAsync(request.Id)).Code.Should().Be(ApiResponseCodes.Retentions.AnnulmentFinalized);
            (await ExpenseJobFinalizeAsync(request.Id)).Code.Should().Be(ApiResponseCodes.Retentions.AnnulmentFinalized);
        }
        _expenseSriStatus.Calls.Should().Be(1);
        await AssertRetentionReversedOnceAsync(expenseId);
        await AssertExpenseReversalsPostedOnceAsync(expenseId, retentionId);
    }
}
