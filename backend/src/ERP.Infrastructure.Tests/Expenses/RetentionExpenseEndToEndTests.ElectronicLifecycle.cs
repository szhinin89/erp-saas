using ERP.Application.Common;
using ERP.Application.Modules.Expenses.UseCases.Documents;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.Expenses.Enums;
using ERP.Domain.Modules.Retentions.Enums;
using ERP.Infrastructure.Tests.TestData;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Tests.Expenses;

/// <summary>
/// ZH-RETENTION-ELECTRONIC-LIFECYCLE-01A (ADR-036) — mismo ciclo electrónico que Compras, desde Gastos,
/// contra PostgreSQL real: la confirmación inicia la transmisión después del commit, y la anulación
/// del gasto se bloquea con el comprobante autorizado (sin Cancelled ni reversos), o descarta el
/// comprobante que nunca salió. Solo se simula la frontera SRI.
/// </summary>
public sealed partial class RetentionExpenseEndToEndTests
{
    private async Task<(Guid ExpenseId, Guid RetentionId)> ConfirmExpenseWithRetentionAsync(
        string number,
        SriBoundaryDouble? sri
    )
    {
        var (db, _) = BuildWiredContext();
        await SeedAccountingChartAsync(db);
        var expenseId = await CreateDraftExpenseAsync(db, _supplierNonExemptId, number);
        // Transmisión post-commit con su propio contexto (ver nota equivalente en Compras).
        await using var electronicDb = CreateContext();
        var transmission = sri is null
            ? null
            : RetentionElectronicTestWiring.Transmission(electronicDb, new FixedCurrentCompany(_companyId), sri);

        var confirm = await BuildConfirmHandler(db, transmission)
            .Handle(new ConfirmExpenseDocumentCommand(expenseId, BuildVatRetentionIntent(15m)), CancellationToken.None);

        confirm.IsSuccess.Should().BeTrue(confirm.Error);
        await using var verify = CreateContext();
        var retention = await verify.RetentionDocuments.AsNoTracking().SingleAsync(x => x.SourceDocumentId == expenseId);
        return (expenseId, retention.Id);
    }

    private async Task<ElectronicDocument?> ReadRetentionElectronicAsync(Guid retentionId)
    {
        await using var db = CreateContext();
        return await db.ElectronicDocuments.AsNoTracking()
            .SingleOrDefaultAsync(d => d.SourceModule == "Retentions" && d.SourceEntityId == retentionId);
    }

    private async Task<Result<ERP.Application.Modules.Expenses.DTOs.ExpenseDocumentDetailDto>> CancelExpenseAsync(Guid expenseId)
    {
        var (db, _) = BuildWiredContext();
        return await BuildCancelHandler(db)
            .Handle(new CancelExpenseDocumentCommand(expenseId, "RETQA anulacion electronica"), CancellationToken.None);
    }

    [Fact]
    public async Task Confirmar_gasto_con_retencion_inicia_la_transmision_electronica_automaticamente()
    {
        var sri = new SriBoundaryDouble(_companyId);

        var (_, retentionId) = await ConfirmExpenseWithRetentionAsync("RETQA-EL1", sri);

        var electronic = await ReadRetentionElectronicAsync(retentionId);
        electronic.Should().NotBeNull("la transmisión arranca sin acción manual (Gastos no tiene botón)");
        electronic!.CurrentState.Should().Be(ElectronicDocumentState.Authorized);
        sri.SendCalls.Should().Be(1);
    }

    [Fact]
    public async Task Retencion_de_gasto_autorizada_bloquea_la_anulacion_del_gasto_sin_reversos()
    {
        var sri = new SriBoundaryDouble(_companyId);
        var (expenseId, retentionId) = await ConfirmExpenseWithRetentionAsync("RETQA-EL2", sri);

        var cancel = await CancelExpenseAsync(expenseId);

        cancel.IsSuccess.Should().BeFalse();
        cancel.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.SourceCancellationRequiresSriAnnulment);
        await using var verify = CreateContext();
        (await verify.ExpenseDocuments.AsNoTracking().SingleAsync(x => x.Id == expenseId)).Status
            .Should().Be(ExpenseStatus.Confirmed);
        (await verify.RetentionDocuments.AsNoTracking().SingleAsync(x => x.Id == retentionId)).Status
            .Should().Be(RetentionStatus.Issued);
        var payable = await verify.AccountsPayables.AsNoTracking().Include(x => x.Installments)
            .SingleAsync(x => x.OriginId == expenseId);
        payable.RetainedAmount.Should().Be(10.5m, "la CxP no se revierte");
        var retentionEntry = await verify.JournalEntries.AsNoTracking()
            .SingleAsync(x => x.SourceModule == "Retentions" && x.SourceEventId == retentionId);
        retentionEntry.Status.Should().NotBe(JournalEntryStatus.Reversed);
        var expenseEntry = await verify.JournalEntries.AsNoTracking()
            .SingleAsync(x => x.SourceModule == "Expenses" && x.SourceEventId == expenseId);
        expenseEntry.Status.Should().NotBe(JournalEntryStatus.Reversed);
        (await ReadRetentionElectronicAsync(retentionId))!.CurrentState.Should().Be(ElectronicDocumentState.Authorized);
    }

    [Fact]
    public async Task Retencion_de_gasto_nunca_transmitida_se_anula_y_su_comprobante_queda_Discarded()
    {
        var (expenseId, retentionId) = await ConfirmExpenseWithRetentionAsync("RETQA-EL3", sri: null);
        await using (var db = CreateContext())
        {
            db.ElectronicDocuments.Add(
                ElectronicDocument.Create(
                    _tenantId, _companyId, ElectronicDocumentType.Retention, "Retentions", retentionId, _createdBy)
            );
            await db.SaveChangesAsync();
        }

        var cancel = await CancelExpenseAsync(expenseId);

        cancel.IsSuccess.Should().BeTrue(cancel.Error);
        await AssertRetentionReversedOnceAsync(expenseId);
        (await ReadRetentionElectronicAsync(retentionId))!.CurrentState.Should().Be(ElectronicDocumentState.Discarded);
    }
}
