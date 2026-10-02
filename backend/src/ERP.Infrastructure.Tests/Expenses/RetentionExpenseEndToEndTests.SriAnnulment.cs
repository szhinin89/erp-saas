using ERP.Application.Common;
using ERP.Application.Modules.Expenses.Services;
using ERP.Application.Modules.Expenses.UseCases.Documents;
using ERP.Application.Modules.Retentions.Services;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.Expenses.Enums;
using ERP.Domain.Modules.Payables.Exceptions;
using ERP.Domain.Modules.Retentions.Entities;
using ERP.Domain.Modules.Retentions.Enums;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Expenses;
using ERP.Infrastructure.Tests.TestData;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Tests.Expenses;

/// <summary>
/// ZH-RETENTION-SRI-ANNULMENT-01/01B (test 10) — anulación ante el SRI de la retención AUTORIZADA de un
/// Gasto contra PostgreSQL real: mismo ciclo que Compras (solicitud sin reversos, CxP retenida,
/// verificación en ConsultaComprobante, finalización por el flujo oficial de anulación de gasto
/// exactamente una vez solo con ANULADO informado por el SRI).
/// </summary>
public sealed partial class RetentionExpenseEndToEndTests
{
    private readonly SriStatusQueryDouble _expenseSriStatus = new();

    private IRetentionAnnulmentService ExpenseAnnulments(ErpDbContext db)
    {
        var company = new FixedCurrentCompany(_companyId);
        return RetentionElectronicTestWiring.AnnulmentService(
            db,
            company,
            _expenseSriStatus,
            new ExpenseRetentionOriginCancellation(BuildCancelHandler(db), new ExpenseDocumentRepository(db, company))
        );
    }

    private async Task<(Guid ExpenseId, Guid RetentionId, RetentionAnnulmentRequest Request)> RequestExpenseAnnulmentAsync(string number)
    {
        var (expenseId, retentionId) = await ConfirmExpenseWithRetentionAsync(number, new SriBoundaryDouble(_companyId));
        var (db, _) = BuildWiredContext();
        var result = await BuildCancelHandler(db)
            .Handle(new CancelExpenseDocumentCommand(expenseId, "RETQA anular gasto", RequestSriAnnulment: true), CancellationToken.None);
        result.IsSuccess.Should().BeTrue(result.Error);
        result.Code.Should().Be(ApiResponseCodes.Retentions.AnnulmentRequested);

        await using var verify = CreateContext();
        var request = await verify.RetentionAnnulmentRequests.AsNoTracking().SingleAsync(r => r.RetentionDocumentId == retentionId);
        return (expenseId, retentionId, request);
    }

    private async Task<Result<RetentionAnnulmentRequest>> SubmitAndVerifyExpenseAnnulmentAsync(Guid requestId, SriFiscalStatus sriSays)
    {
        var (db, _) = BuildWiredContext();
        var service = ExpenseAnnulments(db);
        (await service.MarkSubmittedAsync(_tenantId, _companyId, requestId, new DateOnly(2026, 8, 20), null, null, _createdBy))
            .IsSuccess.Should().BeTrue();
        _expenseSriStatus.FiscalStatus = sriSays;
        return await service.VerifyWithSriAsync(_tenantId, _companyId, requestId, _createdBy);
    }

    [Fact]
    public async Task Gasto_con_retencion_autorizada_inicia_la_anulacion_SRI_sin_reversos()
    {
        var (expenseId, retentionId, request) = await RequestExpenseAnnulmentAsync("RETQA-AN1");

        request.Status.Should().Be(RetentionAnnulmentStatus.PendingSubmission);
        await using var verify = CreateContext();
        (await verify.ExpenseDocuments.AsNoTracking().SingleAsync(x => x.Id == expenseId)).Status.Should().Be(ExpenseStatus.Confirmed);
        (await verify.RetentionDocuments.AsNoTracking().SingleAsync(x => x.Id == retentionId)).Status.Should().Be(RetentionStatus.Issued);
        var payable = await verify.AccountsPayables.AsNoTracking().Include(x => x.Installments).SingleAsync(x => x.OriginId == expenseId);
        payable.RetainedAmount.Should().Be(10.5m);
        payable.AnnulmentHoldRequestId.Should().Be(request.Id);
        (await verify.JournalEntries.AsNoTracking().SingleAsync(x => x.SourceModule == "Retentions" && x.SourceEventId == retentionId))
            .Status.Should().NotBe(JournalEntryStatus.Reversed);
        (await ReadRetentionElectronicAsync(retentionId))!.CurrentState.Should().Be(ElectronicDocumentState.AnnulmentPending);

        var tracked = await verify.AccountsPayables.Include(x => x.Installments).SingleAsync(x => x.OriginId == expenseId);
        ((Action)(() => tracked.ApplySupplierCredit(5m, _createdBy))).Should().Throw<RetentionAnnulmentPendingException>();
    }

    [Fact]
    public async Task Test10_Gasto_ANULADO_informado_por_el_SRI_se_anula_por_su_flujo_oficial_una_vez()
    {
        var (expenseId, retentionId, request) = await RequestExpenseAnnulmentAsync("RETQA-AN2");

        var resolved = await SubmitAndVerifyExpenseAnnulmentAsync(request.Id, SriFiscalStatus.Annulled);
        var (db, _) = BuildWiredContext();
        await ExpenseAnnulments(db).FinalizeAsync(_tenantId, _companyId, request.Id, _createdBy);

        resolved.Code.Should().Be(ApiResponseCodes.Retentions.AnnulmentFinalized);
        await AssertRetentionReversedOnceAsync(expenseId);
        await using var verify = CreateContext();
        (await verify.ExpenseDocuments.AsNoTracking().SingleAsync(x => x.Id == expenseId)).Status.Should().Be(ExpenseStatus.Cancelled);
        (await verify.JournalEntries.AsNoTracking().SingleAsync(x => x.SourceModule == "Expenses" && x.SourceEventId == expenseId))
            .Status.Should().Be(JournalEntryStatus.Reversed);
        (await ReadRetentionElectronicAsync(retentionId))!.CurrentState.Should().Be(ElectronicDocumentState.Cancelled);
    }

    [Fact]
    public async Task Test10_Gasto_PENDIENTE_DE_ANULAR_sigue_vigente_con_la_CxP_retenida()
    {
        var (expenseId, retentionId, request) = await RequestExpenseAnnulmentAsync("RETQA-AN3");

        var verified = await SubmitAndVerifyExpenseAnnulmentAsync(request.Id, SriFiscalStatus.PendingAnnulment);

        verified.Code.Should().Be(ApiResponseCodes.Retentions.SriAnnulmentPending);
        await using var verify = CreateContext();
        (await verify.ExpenseDocuments.AsNoTracking().SingleAsync(x => x.Id == expenseId)).Status.Should().Be(ExpenseStatus.Confirmed);
        (await verify.RetentionDocuments.AsNoTracking().SingleAsync(x => x.Id == retentionId)).Status.Should().Be(RetentionStatus.Issued);
        var payable = await verify.AccountsPayables.AsNoTracking().Include(x => x.Installments).SingleAsync(x => x.OriginId == expenseId);
        payable.RetainedAmount.Should().Be(10.5m);
        payable.AnnulmentHoldRequestId.Should().Be(request.Id, "la CxP sigue retenida mientras el SRI no resuelva");
        (await ReadRetentionElectronicAsync(retentionId))!.CurrentState.Should().Be(ElectronicDocumentState.AnnulmentPending);
        (await verify.JournalEntries.AsNoTracking().SingleAsync(x => x.SourceModule == "Expenses" && x.SourceEventId == expenseId))
            .Status.Should().NotBe(JournalEntryStatus.Reversed);
    }

    [Fact]
    public async Task Test10_Gasto_AUTORIZADO_permite_desistir_y_libera_la_CxP_sin_tocar_el_gasto()
    {
        var (expenseId, retentionId, request) = await RequestExpenseAnnulmentAsync("RETQA-AN4");

        (await SubmitAndVerifyExpenseAnnulmentAsync(request.Id, SriFiscalStatus.Authorized)).Code
            .Should().Be(ApiResponseCodes.Retentions.SriStillAuthorized);
        var (db, _) = BuildWiredContext();
        (await ExpenseAnnulments(db).AbandonAsync(_tenantId, _companyId, request.Id, "El receptor no aceptó", _createdBy))
            .IsSuccess.Should().BeTrue();

        await using var verify = CreateContext();
        (await verify.ExpenseDocuments.AsNoTracking().SingleAsync(x => x.Id == expenseId)).Status.Should().Be(ExpenseStatus.Confirmed);
        (await verify.AccountsPayables.AsNoTracking().SingleAsync(x => x.OriginId == expenseId)).AnnulmentHoldRequestId.Should().BeNull();
        (await ReadRetentionElectronicAsync(retentionId))!.CurrentState.Should().Be(ElectronicDocumentState.Authorized);
    }
}
