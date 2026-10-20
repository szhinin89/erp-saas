using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Continuity;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Retentions.Entities;
using ERP.Domain.Modules.Retentions.Enums;
using ERP.Infrastructure.Tests.TestData;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Infrastructure.Tests.Modules.Purchases;

/// <summary>
/// Regresión — RetentionElectronicRecoveryJob finalizaba la anulación SRI pero dejaba los reversos
/// contables Pending sin intento inicial: el job no pasa por AccountingPostingDispatchBehavior. Estos
/// tests ejecutan el camino del job tal cual (scope propio, usuario Guid.Empty, SIN ThenDispatchAsync)
/// contra PostgreSQL real y con la consulta SRI simulada.
/// </summary>
public sealed partial class PurchaseRetentionConfirmIntegrationTests
{
    /// <summary>Misma llamada que RetentionElectronicRecoveryJob.VerifySubmittedAnnulmentsAsync.</summary>
    private async Task<Result<RetentionAnnulmentRequest>> JobVerifyAsync(
        Guid requestId,
        Action<IServiceCollection>? configure = null
    )
    {
        await using var scope = CreateWiredContext(configure);
        return await Annulments(scope)
            .VerifyWithSriAsync(_tenantId, _companyId, requestId, Guid.Empty);
    }

    /// <summary>Misma llamada que RetentionElectronicRecoveryJob.FinalizeAcceptedAnnulmentsAsync.</summary>
    private async Task<Result<RetentionAnnulmentRequest>> JobFinalizeAsync(Guid requestId)
    {
        await using var scope = CreateWiredContext();
        return await Annulments(scope).FinalizeAsync(_tenantId, _companyId, requestId, Guid.Empty);
    }

    private async Task<List<(Guid Id, AccountingPostingStatus Status, int Attempts, string? ErrorCode)>> ReversalPostingsAsync()
    {
        await using var db = CreateContext();
        return (
            await db
                .AccountingPostings.AsNoTracking()
                .Where(p => p.CompanyId == _companyId && p.FactType == "Reversal")
                .Select(p => new { p.Id, p.Status, p.Attempts, p.ErrorCode })
                .ToListAsync()
        )
            .Select(p => (p.Id, p.Status, p.Attempts, p.ErrorCode))
            .ToList();
    }

    private async Task<int> ReversalEntriesAsync()
    {
        await using var db = CreateContext();
        return await db
            .JournalEntries.AsNoTracking()
            .CountAsync(e => e.CompanyId == _companyId && e.SourceEventType == "Reversal");
    }

    /// <summary>Reversos de la compra y de su retención publicados una sola vez, con un único intento cada uno.</summary>
    private async Task AssertReversalsPostedOnceAsync(Guid retentionId)
    {
        var reversals = await ReversalPostingsAsync();
        reversals.Should().HaveCount(2, "se revierten el asiento de la compra y el de la retención");
        reversals
            .Should()
            .OnlyContain(r => r.Status == AccountingPostingStatus.Posted && r.Attempts == 1);
        (await ReversalEntriesAsync()).Should().Be(2);
        (await RetentionAccountingAsync(retentionId)).Reversals.Should().Be(1);
    }

    [Fact]
    public async Task Job_ANULADO_anula_la_compra_y_contabiliza_sus_reversos_tras_el_commit()
    {
        var (invoiceId, retentionId) = await ConfirmWithAuthorizedRetentionAsync();
        var stockMovements = await StockMovementCountAsync(invoiceId);
        var request = await RequestAndSubmitAsync(invoiceId, retentionId);
        _sriStatus.FiscalStatus = SriFiscalStatus.Annulled;

        var result = await JobVerifyAsync(request.Id);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Code.Should().Be(ApiResponseCodes.Retentions.AnnulmentFinalized);
        await AssertCancelledExactlyOnceAsync(invoiceId, retentionId, stockMovements);
        (await ReadAnnulmentAsync(retentionId))!.FinalizedAtUtc.Should().NotBeNull();
        await AssertReversalsPostedOnceAsync(retentionId);
    }

    [Fact]
    public async Task Job_finalizacion_pendiente_reintentada_contabiliza_los_reversos()
    {
        var (invoiceId, retentionId) = await ConfirmWithAuthorizedRetentionAsync();
        var request = await RequestAndSubmitAsync(invoiceId, retentionId);
        _sriStatus.FiscalStatus = SriFiscalStatus.Annulled;
        // ANULADO aceptado con la finalización fallida (sin flujo de origen) → queda para el job.
        await using (var db = CreateWiredContext())
            (
                await RetentionElectronicTestWiring
                    .AnnulmentService(db, new FixedCurrentCompany(_companyId), _sriStatus)
                    .VerifyWithSriAsync(_tenantId, _companyId, request.Id, _userId)
            )
                .Code.Should()
                .Be(ApiResponseCodes.Retentions.AnnulmentFinalizationPending);
        (await ReversalPostingsAsync()).Should().BeEmpty();

        var result = await JobFinalizeAsync(request.Id);

        result.Code.Should().Be(ApiResponseCodes.Retentions.AnnulmentFinalized);
        (await ReadAsync(invoiceId)).Status.Should().Be(PurchaseStatus.Cancelled);
        await AssertReversalsPostedOnceAsync(retentionId);
    }

    [Fact]
    public async Task Job_repetido_no_duplica_reversos_ni_intentos()
    {
        var (invoiceId, retentionId) = await ConfirmWithAuthorizedRetentionAsync();
        var stockMovements = await StockMovementCountAsync(invoiceId);
        var request = await RequestAndSubmitAsync(invoiceId, retentionId);
        _sriStatus.FiscalStatus = SriFiscalStatus.Annulled;
        (await JobVerifyAsync(request.Id)).IsSuccess.Should().BeTrue();

        for (var run = 0; run < 3; run++)
        {
            (await JobVerifyAsync(request.Id)).Code.Should().Be(ApiResponseCodes.Retentions.AnnulmentFinalized);
            (await JobFinalizeAsync(request.Id)).Code.Should().Be(ApiResponseCodes.Retentions.AnnulmentFinalized);
        }

        _sriStatus.Calls.Should().Be(1, "una solicitud ya aceptada no se vuelve a consultar");
        await AssertCancelledExactlyOnceAsync(invoiceId, retentionId, stockMovements);
        await AssertReversalsPostedOnceAsync(retentionId);
    }

    [Fact]
    public async Task En_linea_por_MediatR_el_reverso_tiene_un_solo_intento()
    {
        var (invoiceId, retentionId) = await ConfirmWithAuthorizedRetentionAsync();
        var request = await RequestAndSubmitAsync(invoiceId, retentionId);

        // VerifyAsync = servicio + ThenDispatchAsync (equivalente a AccountingPostingDispatchBehavior).
        (await VerifyAnnulledAsync(request.Id)).IsSuccess.Should().BeTrue();

        await AssertReversalsPostedOnceAsync(retentionId);
    }

    [Fact]
    public async Task Job_con_fallo_contable_conserva_la_anulacion_y_deja_los_reversos_Failed_recuperables()
    {
        var (invoiceId, retentionId) = await ConfirmWithAuthorizedRetentionAsync();
        var stockMovements = await StockMovementCountAsync(invoiceId);
        var request = await RequestAndSubmitAsync(invoiceId, retentionId);
        _sriStatus.FiscalStatus = SriFiscalStatus.Annulled;

        // Solo el intento contable del reverso numera asientos: falla interna del motor en ese intento.
        var result = await JobVerifyAsync(
            request.Id,
            services =>
                services.AddScoped<IJournalEntrySequenceRepository>(_ =>
                    new Moq.Mock<IJournalEntrySequenceRepository>(Moq.MockBehavior.Strict).Object
                )
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Code.Should().Be(ApiResponseCodes.Retentions.AnnulmentFinalized);
        var s = await ReadAsync(invoiceId);
        s.Status.Should().Be(PurchaseStatus.Cancelled, "un error contable no revierte la anulación operativa");
        s.Retentions.Single().Status.Should().Be(RetentionStatus.Cancelled);
        (await StockMovementCountAsync(invoiceId)).Should().Be(stockMovements * 2);
        (await ReadElectronicAsync(retentionId))!.CurrentState.Should().Be(ElectronicDocumentState.Cancelled);
        (await ReadAnnulmentAsync(retentionId))!.FinalizedAtUtc.Should().NotBeNull();
        var failed = await ReversalPostingsAsync();
        failed.Should().HaveCount(2);
        failed
            .Should()
            .OnlyContain(r =>
                r.Status == AccountingPostingStatus.Failed
                && r.Attempts == 1
                && r.ErrorCode == AccountingPostingExecutor.InternalErrorCode
            );
        (await ReversalEntriesAsync()).Should().Be(0);

        // Sin reintento automático: otra corrida del job no toca las obligaciones Failed.
        await JobVerifyAsync(request.Id);
        await JobFinalizeAsync(request.Id);
        (await ReversalPostingsAsync())
            .Should()
            .OnlyContain(r => r.Status == AccountingPostingStatus.Failed && r.Attempts == 1);

        // Recuperación manual con el ejecutor común.
        foreach (var (id, _, _, _) in failed)
        {
            await using var db = CreateWiredContext();
            (await AccountingContinuityTestWiring.ReprocessAsync(db, id, "Reproceso tras falla interna"))
                .Status.Should()
                .Be(AccountingPostingStatus.Posted);
        }
        (await ReversalPostingsAsync()).Should().OnlyContain(r => r.Status == AccountingPostingStatus.Posted);
        (await ReversalEntriesAsync()).Should().Be(2);
        (await RetentionAccountingAsync(retentionId)).Reversals.Should().Be(1);
    }
}
