using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Continuity;
using ERP.Application.Modules.Accounting.DTOs;
using ERP.Application.Modules.Accounting.Posting.Translators;
using ERP.Application.Modules.Accounting.UseCases.JournalEntries;
using ERP.Application.Modules.Retentions.Exceptions;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Retentions.Enums;
using ERP.Domain.Modules.Retentions.Events;
using FluentAssertions;
using MediatR;
using Moq;
using static ERP.Application.Tests.Accounting.PurchaseInvoiceCancelledPostingTranslatorTests;

namespace ERP.Application.Tests.Accounting;

/// <summary>
/// RETENTIONS-EXPENSES-INTEGRATION-01D-3 + ACCOUNTING-CONTINUITY-01 — anular una retención:
/// asiento Posted → reverso; obligación no publicada → Canceled; sin asiento ni obligación → nada
/// (antes lanzaba JOURNAL_ENTRY_NOT_FOUND y bloqueaba la anulación); reverso fallido → bloquea.
/// </summary>
public sealed class RetentionDocumentCancelledPostingTranslatorTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();

    private static RetentionDocumentCancelledEvent Event(Guid retentionId) =>
        new(
            TenantId,
            retentionId,
            CompanyId,
            RetentionSourceDocumentType.PurchaseInvoice,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "001-001-000000001",
            4.50m,
            "Compra anulada"
        );

    private sealed class Mocks
    {
        public Mock<IJournalEntryRepository> JournalEntries { get; } = new();
        public Mock<IAccountingPostingRepository> Postings { get; } = new();
        public Mock<IMediator> Mediator { get; } = new();

        public RetentionDocumentCancelledPostingTranslator Build() =>
            new(
                new AccountingPostingCancellation(
                    Postings.Object,
                    JournalEntries.Object,
                    Mock.Of<ICurrentUser>()
                ),
                Mediator.Object
            );

        public void Setup(Guid retentionId, ERP.Domain.Modules.Accounting.Entities.AccountingPosting? posting, params ERP.Domain.Modules.Accounting.Entities.JournalEntry[] entries)
        {
            JournalEntries
                .Setup(r => r.GetBySourceAsync(TenantId, CompanyId, "Retentions", retentionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(entries.ToList());
            Postings
                .Setup(r =>
                    r.LockByKeyAsync(TenantId, CompanyId, "Retentions", "DocumentIssued", retentionId, It.IsAny<CancellationToken>())
                )
                .ReturnsAsync(posting);
        }
    }

    [Fact]
    public async Task Localiza_asiento_Posted_Retentions_DocumentIssued_y_lo_reversa()
    {
        var retentionId = Guid.NewGuid();
        var original = PostedEntry(TenantId, CompanyId, "Retentions", "DocumentIssued", retentionId);
        var m = new Mocks();
        m.Setup(retentionId, null, original);
        ReverseJournalEntryCommand? sent = null;
        m.Mediator.Setup(x => x.Send(It.IsAny<ReverseJournalEntryCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<JournalEntryDto>>, CancellationToken>((cmd, _) => sent = (ReverseJournalEntryCommand)cmd)
            .ReturnsAsync(Result<JournalEntryDto>.Success(null!));

        await m.Build().Handle(Event(retentionId), CancellationToken.None);

        sent!.JournalEntryId.Should().Be(original.Id);
        sent.Reason.Should().Contain("001-001-000000001");
    }

    [Fact]
    public async Task Sin_asiento_ni_obligacion_no_bloquea_la_anulacion()
    {
        var retentionId = Guid.NewGuid();
        var m = new Mocks();
        m.Setup(retentionId, null);

        await m.Build().Handle(Event(retentionId), CancellationToken.None);

        m.Mediator.Verify(x => x.Send(It.IsAny<ReverseJournalEntryCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Obligacion_Pending_sin_asiento_queda_Canceled()
    {
        var retentionId = Guid.NewGuid();
        var obligation = Obligation(TenantId, CompanyId, "Retentions", "DocumentIssued", retentionId);
        var m = new Mocks();
        m.Setup(retentionId, obligation);

        await m.Build().Handle(Event(retentionId), CancellationToken.None);

        obligation.Status.Should().Be(AccountingPostingStatus.Canceled);
        m.Mediator.Verify(x => x.Send(It.IsAny<ReverseJournalEntryCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Si_el_reverso_falla_lanza_RetentionPostingFailedException()
    {
        var retentionId = Guid.NewGuid();
        var m = new Mocks();
        m.Setup(retentionId, null, PostedEntry(TenantId, CompanyId, "Retentions", "DocumentIssued", retentionId));
        m.Mediator.Setup(x => x.Send(It.IsAny<ReverseJournalEntryCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ReverseFailure());

        var act = () => m.Build().Handle(Event(retentionId), CancellationToken.None);

        (await act.Should().ThrowAsync<RetentionPostingFailedException>()).Which.Code.Should().Be("PERIOD_CLOSED");
    }
}
