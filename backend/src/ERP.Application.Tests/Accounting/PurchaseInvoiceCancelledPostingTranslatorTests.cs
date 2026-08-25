using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Posting.Translators;
using ERP.Application.Modules.Accounting.UseCases.JournalEntries;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Purchases.Events;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;

namespace ERP.Application.Tests.Accounting;

/// <summary>
/// ACCOUNTING-REVERSALS-05 — <see cref="PurchaseInvoiceCancelledPostingTranslator"/>: a diferencia
/// de las devoluciones (que publican un PostingFact nuevo), anular una PurchaseInvoice ya
/// contabilizada debe reversar (JournalEntry.Reverse, vía ReverseJournalEntryCommand) el asiento
/// original localizado por SourceModule="Purchases"/SourceEventType="InvoiceReceived".
/// </summary>
public sealed class PurchaseInvoiceCancelledPostingTranslatorTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid CreatedBy = Guid.NewGuid();

    private static PurchaseInvoiceCancelledEvent Event(Guid invoiceId) =>
        new(TenantId, invoiceId, SupplierId, "001-001-000000009", 115m, "Compra duplicada");

    private static JournalEntry PostedEntry(Guid sourceEventId, string sourceEventType = "InvoiceReceived")
    {
        var entry = JournalEntry.Create(
            TenantId,
            CompanyId,
            new DateOnly(2026, 8, 1),
            Guid.NewGuid(),
            2026,
            "Purchases",
            sourceEventType,
            sourceEventId,
            "Asiento de compra",
            CreatedBy
        );
        entry.AddLine(Guid.NewGuid(), null, 100m, 0m);
        entry.AddLine(Guid.NewGuid(), null, 0m, 100m);
        entry.Post(CreatedBy, 1);
        return entry;
    }

    private sealed class Mocks
    {
        public Mock<IJournalEntryRepository> JournalEntries { get; } = new();
        public Mock<IMediator> Mediator { get; } = new();
        public Mock<ICurrentCompany> Company { get; } = new();
        public Mock<ILogger<PurchaseInvoiceCancelledPostingTranslator>> Logger { get; } = new();

        public Mocks() => Company.Setup(c => c.CompanyId).Returns(CompanyId);

        public PurchaseInvoiceCancelledPostingTranslator BuildTranslator() =>
            new(JournalEntries.Object, Mediator.Object, Company.Object, Logger.Object);

        public void VerifyWarningLogged(Times times) =>
            Logger.Verify(
                l =>
                    l.Log(
                        LogLevel.Warning,
                        It.IsAny<EventId>(),
                        It.IsAny<It.IsAnyType>(),
                        It.IsAny<Exception>(),
                        It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                    ),
                times
            );
    }

    [Fact]
    public async Task Factura_con_asiento_Posted_envia_ReverseJournalEntryCommand_para_ese_asiento()
    {
        var invoiceId = Guid.NewGuid();
        var original = PostedEntry(invoiceId);
        var m = new Mocks();
        m.JournalEntries
            .Setup(r =>
                r.GetBySourceAsync(TenantId, CompanyId, "Purchases", invoiceId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new List<JournalEntry> { original });

        ReverseJournalEntryCommand? sent = null;
        m.Mediator
            .Setup(x => x.Send(It.IsAny<ReverseJournalEntryCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<ERP.Application.Modules.Accounting.DTOs.JournalEntryDto>>, CancellationToken>(
                (cmd, _) => sent = (ReverseJournalEntryCommand)cmd
            )
            .ReturnsAsync(
                Result<ERP.Application.Modules.Accounting.DTOs.JournalEntryDto>.Success(
                    new ERP.Application.Modules.Accounting.DTOs.JournalEntryDto(
                        Guid.NewGuid(),
                        original.EntryDate,
                        original.AccountingPeriodId,
                        original.FiscalYear,
                        "Accounting",
                        "Reversal",
                        original.Id,
                        "Reverso",
                        "Posted",
                        2,
                        DateTime.UtcNow,
                        original.Id,
                        null,
                        null,
                        null
                    )
                )
            );

        var translator = m.BuildTranslator();
        await translator.Handle(Event(invoiceId), CancellationToken.None);

        sent.Should().NotBeNull();
        sent!.JournalEntryId.Should().Be(original.Id);
        sent.Reason.Should().Contain("001-001-000000009").And.Contain("Compra duplicada");
        m.VerifyWarningLogged(Times.Never());
    }

    [Fact]
    public async Task Sin_asiento_Posted_no_envia_comando_ni_genera_warning()
    {
        var invoiceId = Guid.NewGuid();
        var m = new Mocks();
        m.JournalEntries
            .Setup(r =>
                r.GetBySourceAsync(TenantId, CompanyId, "Purchases", invoiceId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new List<JournalEntry>()); // nunca se contabilizó

        var translator = m.BuildTranslator();
        var act = async () => await translator.Handle(Event(invoiceId), CancellationToken.None);

        await act.Should().NotThrowAsync();
        m.Mediator.Verify(
            x => x.Send(It.IsAny<ReverseJournalEntryCommand>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        m.VerifyWarningLogged(Times.Never());
    }

    [Fact]
    public async Task Ignora_asientos_de_otro_SourceEventType_del_mismo_SourceEventId()
    {
        // Un reverso previo del mismo Id (poco probable pero defensivo) no debe confundirse con
        // el asiento original — solo SourceEventType="InvoiceReceived" es candidato válido.
        var invoiceId = Guid.NewGuid();
        var notTheInvoice = PostedEntry(invoiceId, sourceEventType: "SomethingElse");
        var m = new Mocks();
        m.JournalEntries
            .Setup(r =>
                r.GetBySourceAsync(TenantId, CompanyId, "Purchases", invoiceId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new List<JournalEntry> { notTheInvoice });

        var translator = m.BuildTranslator();
        await translator.Handle(Event(invoiceId), CancellationToken.None);

        m.Mediator.Verify(
            x => x.Send(It.IsAny<ReverseJournalEntryCommand>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Reverso_fallido_genera_warning_y_no_lanza_excepcion()
    {
        var invoiceId = Guid.NewGuid();
        var original = PostedEntry(invoiceId);
        var m = new Mocks();
        m.JournalEntries
            .Setup(r =>
                r.GetBySourceAsync(TenantId, CompanyId, "Purchases", invoiceId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new List<JournalEntry> { original });
        m.Mediator
            .Setup(x => x.Send(It.IsAny<ReverseJournalEntryCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<ERP.Application.Modules.Accounting.DTOs.JournalEntryDto>.ValidationFailure(
                    "El período contable está cerrado.",
                    "PERIOD_NOT_OPEN"
                )
            );

        var translator = m.BuildTranslator();
        var act = async () => await translator.Handle(Event(invoiceId), CancellationToken.None);

        await act.Should().NotThrowAsync();
        m.VerifyWarningLogged(Times.Once());
    }
}
