using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Continuity;
using ERP.Application.Modules.Accounting.DTOs;
using ERP.Application.Modules.Accounting.Posting.Translators;
using ERP.Application.Modules.Accounting.UseCases.JournalEntries;
using ERP.Application.Modules.Purchases.Exceptions;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Purchases.Events;
using FluentAssertions;
using MediatR;
using Moq;

namespace ERP.Application.Tests.Accounting;

/// <summary>
/// ACCOUNTING-REVERSALS-05 + ACCOUNTING-CONTINUITY-01 — anular una compra: asiento Posted → reverso
/// existente; obligación Pending/Failed sin asiento → Canceled; nada publicado ni pendiente → nada;
/// reverso fallido → excepción que bloquea la anulación (antes: warning y asiento vigente).
/// </summary>
public sealed class PurchaseInvoiceCancelledPostingTranslatorTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid CreatedBy = Guid.NewGuid();

    private static PurchaseInvoiceCancelledEvent Event(Guid invoiceId) =>
        new(TenantId, invoiceId, Guid.NewGuid(), "001-001-000000009", 115m, "Compra duplicada");

    internal static JournalEntry PostedEntry(
        Guid tenantId,
        Guid companyId,
        string sourceModule,
        string sourceEventType,
        Guid sourceEventId
    )
    {
        var entry = JournalEntry.Create(
            tenantId,
            companyId,
            new DateOnly(2026, 8, 1),
            Guid.NewGuid(),
            2026,
            sourceModule,
            sourceEventType,
            sourceEventId,
            "Asiento",
            CreatedBy
        );
        entry.AddLine(Guid.NewGuid(), null, 100m, 0m);
        entry.AddLine(Guid.NewGuid(), null, 0m, 100m);
        entry.Post(CreatedBy, 1);
        return entry;
    }

    internal static AccountingPosting Obligation(
        Guid tenantId,
        Guid companyId,
        string sourceModule,
        string factType,
        Guid sourceEventId
    ) =>
        AccountingPosting.CreatePending(
            tenantId,
            companyId,
            sourceModule,
            factType,
            sourceEventId,
            null,
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 1),
            AccountingPostingPayload.CurrentVersion,
            "{}",
            CreatedBy,
            null
        );

    internal static Result<JournalEntryDto> ReverseFailure() =>
        Result<JournalEntryDto>.ValidationFailure("El período está cerrado.", "PERIOD_CLOSED");

    private sealed class Mocks
    {
        public Mock<IJournalEntryRepository> JournalEntries { get; } = new();
        public Mock<IAccountingPostingRepository> Postings { get; } = new();
        public Mock<IMediator> Mediator { get; } = new();

        public PurchaseInvoiceCancelledPostingTranslator Build() =>
            new(
                new AccountingPostingCancellation(
                    Postings.Object,
                    JournalEntries.Object,
                    Mock.Of<ICurrentUser>(u => u.UserId == CreatedBy)
                ),
                Mediator.Object,
                Mock.Of<ICurrentCompany>(c => c.CompanyId == CompanyId)
            );

        public void Ledger(Guid invoiceId, params JournalEntry[] entries) =>
            JournalEntries
                .Setup(r => r.GetBySourceAsync(TenantId, CompanyId, "Purchases", invoiceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(entries.ToList());

        public void Locked(Guid invoiceId, AccountingPosting? posting) =>
            Postings
                .Setup(r =>
                    r.LockByKeyAsync(TenantId, CompanyId, "Purchases", "InvoiceReceived", invoiceId, It.IsAny<CancellationToken>())
                )
                .ReturnsAsync(posting);
    }

    [Fact]
    public async Task Asiento_Posted_envia_ReverseJournalEntryCommand_para_ese_asiento()
    {
        var invoiceId = Guid.NewGuid();
        var original = PostedEntry(TenantId, CompanyId, "Purchases", "InvoiceReceived", invoiceId);
        var m = new Mocks();
        m.Ledger(invoiceId, original);
        m.Locked(invoiceId, null);
        ReverseJournalEntryCommand? sent = null;
        m.Mediator.Setup(x => x.Send(It.IsAny<ReverseJournalEntryCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<JournalEntryDto>>, CancellationToken>((cmd, _) => sent = (ReverseJournalEntryCommand)cmd)
            .ReturnsAsync(Result<JournalEntryDto>.Success(null!));

        await m.Build().Handle(Event(invoiceId), CancellationToken.None);

        sent.Should().NotBeNull();
        sent!.JournalEntryId.Should().Be(original.Id);
        sent.Reason.Should().Contain("001-001-000000009").And.Contain("Compra duplicada");
    }

    [Fact]
    public async Task Obligacion_Failed_sin_asiento_queda_Canceled_sin_reverso()
    {
        var invoiceId = Guid.NewGuid();
        var obligation = Obligation(TenantId, CompanyId, "Purchases", "InvoiceReceived", invoiceId);
        obligation.RecordFailed("RULE_NOT_FOUND", "Sin regla", AccountingPostingAttemptTrigger.Initial, CreatedBy, null, DateTime.UtcNow, DateTime.UtcNow);
        var m = new Mocks();
        m.Ledger(invoiceId);
        m.Locked(invoiceId, obligation);

        await m.Build().Handle(Event(invoiceId), CancellationToken.None);

        obligation.Status.Should().Be(AccountingPostingStatus.Canceled);
        m.Mediator.Verify(x => x.Send(It.IsAny<ReverseJournalEntryCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Sin_asiento_ni_obligacion_no_hace_nada()
    {
        var invoiceId = Guid.NewGuid();
        var m = new Mocks();
        m.Ledger(invoiceId);
        m.Locked(invoiceId, null);

        await m.Build().Handle(Event(invoiceId), CancellationToken.None);

        m.Mediator.Verify(x => x.Send(It.IsAny<ReverseJournalEntryCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Ignora_asientos_de_otro_SourceEventType_del_mismo_SourceEventId()
    {
        var invoiceId = Guid.NewGuid();
        var m = new Mocks();
        m.Ledger(invoiceId, PostedEntry(TenantId, CompanyId, "Purchases", "SomethingElse", invoiceId));
        m.Locked(invoiceId, null);

        await m.Build().Handle(Event(invoiceId), CancellationToken.None);

        m.Mediator.Verify(x => x.Send(It.IsAny<ReverseJournalEntryCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Reverso_fallido_lanza_y_bloquea_la_anulacion()
    {
        var invoiceId = Guid.NewGuid();
        var m = new Mocks();
        m.Ledger(invoiceId, PostedEntry(TenantId, CompanyId, "Purchases", "InvoiceReceived", invoiceId));
        m.Locked(invoiceId, null);
        m.Mediator.Setup(x => x.Send(It.IsAny<ReverseJournalEntryCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ReverseFailure());

        var act = () => m.Build().Handle(Event(invoiceId), CancellationToken.None);

        (await act.Should().ThrowAsync<PurchasePostingFailedException>()).Which.Code.Should().Be("PERIOD_CLOSED");
    }

    [Fact]
    public async Task Obligacion_Pending_con_asiento_ya_publicado_se_enlaza_y_se_reversa()
    {
        var invoiceId = Guid.NewGuid();
        var original = PostedEntry(TenantId, CompanyId, "Purchases", "InvoiceReceived", invoiceId);
        var obligation = Obligation(TenantId, CompanyId, "Purchases", "InvoiceReceived", invoiceId);
        var m = new Mocks();
        m.Ledger(invoiceId, original);
        m.Locked(invoiceId, obligation);
        m.Mediator.Setup(x => x.Send(It.IsAny<ReverseJournalEntryCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<JournalEntryDto>.Success(null!));

        await m.Build().Handle(Event(invoiceId), CancellationToken.None);

        obligation.Status.Should().Be(AccountingPostingStatus.Posted);
        obligation.JournalEntryId.Should().Be(original.Id);
        m.Mediator.Verify(x => x.Send(It.IsAny<ReverseJournalEntryCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
