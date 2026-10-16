using ERP.Application.Modules.Accounting.Posting.Translators;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Retentions.Enums;
using ERP.Domain.Modules.Retentions.Events;
using FluentAssertions;

namespace ERP.Application.Tests.Accounting;

/// <summary>
/// ACCOUNTING-CONTINUITY-01 — la retención emitida registra su propia obligación durable
/// (Retentions/DocumentIssued), independiente de la de su origen, con los importes por componente.
/// </summary>
public sealed class RetentionDocumentIssuedPostingTranslatorTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();

    private static RetentionDocumentIssuedEvent Event(
        Guid retentionId,
        decimal vat,
        decimal income
    ) =>
        new(
            TenantId,
            retentionId,
            CompanyId,
            RetentionSourceDocumentType.PurchaseInvoice,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "001-001-000000001",
            vat,
            income,
            vat + income,
            new DateOnly(2026, 9, 3)
        );

    [Fact]
    public async Task Registra_obligacion_Retentions_DocumentIssued_con_importes_por_componente()
    {
        var h = new AccountingContinuityTestHarness(TenantId, CompanyId);
        var retentionId = Guid.NewGuid();

        await new RetentionDocumentIssuedPostingTranslator(h.Recorder).Handle(
            Event(retentionId, 4.50m, 1.75m),
            CancellationToken.None
        );

        var posting = h.Added.Single();
        posting.Status.Should().Be(AccountingPostingStatus.Pending);
        posting.SourceModule.Should().Be("Retentions");
        posting.FactType.Should().Be("DocumentIssued");
        posting.SourceEventId.Should().Be(retentionId);
        posting.EntryDate.Should().Be(new DateOnly(2026, 9, 3));
        var payload = h.SinglePayload();
        payload.RetainedAmount.Should().Be(6.25m);
        payload.RetainedVatAmount.Should().Be(4.50m);
        payload.RetainedIncomeAmount.Should().Be(1.75m);
        payload.GrandTotal.Should().Be(6.25m);
    }

    [Fact]
    public async Task Retencion_solo_IVA_transporta_componente_renta_en_cero()
    {
        var h = new AccountingContinuityTestHarness(TenantId, CompanyId);

        await new RetentionDocumentIssuedPostingTranslator(h.Recorder).Handle(
            Event(Guid.NewGuid(), 4.50m, 0m),
            CancellationToken.None
        );

        var payload = h.SinglePayload();
        payload.RetainedVatAmount.Should().Be(4.50m);
        payload.RetainedIncomeAmount.Should().Be(0m);
    }
}
