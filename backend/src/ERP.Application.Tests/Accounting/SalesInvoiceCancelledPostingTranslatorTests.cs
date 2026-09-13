using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Posting.Translators;
using ERP.Application.Modules.Accounting.UseCases.JournalEntries;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Sales.Events;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;

namespace ERP.Application.Tests.Accounting;

/// <summary>
/// SALES-CASH-VS-RECEIVABLE-POSTING-SPLIT-AND-CANCEL-REVERSAL-01 Lote 3 —
/// <see cref="SalesInvoiceCancelledPostingTranslator"/>: mismo criterio que
/// <c>PurchaseInvoiceCancelledPostingTranslator</c>, pero reversando DOS FactType independientes
/// del mismo InvoiceId ("InvoiceIssued" y "CostOfGoodsSold") en vez de uno solo.
/// </summary>
public sealed class SalesInvoiceCancelledPostingTranslatorTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid CreatedBy = Guid.NewGuid();

    private static SalesInvoiceCancelledEvent Event(Guid invoiceId) =>
        new(TenantId, invoiceId, CustomerId, "001-001-000000009", 115m, "Cliente se arrepintió", CompanyId);

    private static JournalEntry PostedEntry(Guid sourceEventId, string sourceEventType) =>
        BuildEntry(sourceEventId, sourceEventType, post: true);

    private static JournalEntry BuildEntry(
        Guid sourceEventId,
        string sourceEventType,
        bool post
    )
    {
        var entry = JournalEntry.Create(
            TenantId,
            CompanyId,
            new DateOnly(2026, 9, 13),
            Guid.NewGuid(),
            2026,
            "Sales",
            sourceEventType,
            sourceEventId,
            "Asiento de venta",
            CreatedBy
        );
        entry.AddLine(Guid.NewGuid(), null, 100m, 0m);
        entry.AddLine(Guid.NewGuid(), null, 0m, 100m);
        if (post)
            entry.Post(CreatedBy, 1);
        return entry;
    }

    private sealed class Mocks
    {
        public Mock<IJournalEntryRepository> JournalEntries { get; } = new();
        public Mock<IMediator> Mediator { get; } = new();
        public Mock<ILogger<SalesInvoiceCancelledPostingTranslator>> Logger { get; } = new();

        public SalesInvoiceCancelledPostingTranslator BuildTranslator() =>
            new(JournalEntries.Object, Mediator.Object, Logger.Object);

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

    private static Result<ERP.Application.Modules.Accounting.DTOs.JournalEntryDto> SuccessResult(
        JournalEntry original
    ) =>
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
        );

    [Fact]
    public async Task Con_InvoiceIssued_y_CostOfGoodsSold_Posted_reversa_ambos()
    {
        var invoiceId = Guid.NewGuid();
        var invoiceIssued = PostedEntry(invoiceId, "InvoiceIssued");
        var cogs = PostedEntry(invoiceId, "CostOfGoodsSold");
        var m = new Mocks();
        m.JournalEntries
            .Setup(r =>
                r.GetBySourceAsync(TenantId, CompanyId, "Sales", invoiceId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new List<JournalEntry> { invoiceIssued, cogs });

        var sentCommands = new List<ReverseJournalEntryCommand>();
        m.Mediator
            .Setup(x => x.Send(It.IsAny<ReverseJournalEntryCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<ERP.Application.Modules.Accounting.DTOs.JournalEntryDto>>, CancellationToken>(
                (cmd, _) => sentCommands.Add((ReverseJournalEntryCommand)cmd)
            )
            .ReturnsAsync(SuccessResult(invoiceIssued));

        var translator = m.BuildTranslator();
        await translator.Handle(Event(invoiceId), CancellationToken.None);

        sentCommands.Should().HaveCount(2);
        sentCommands.Select(c => c.JournalEntryId)
            .Should()
            .BeEquivalentTo(new[] { invoiceIssued.Id, cogs.Id });
        sentCommands.Should().OnlyContain(c => c.Reason.Contains("001-001-000000009"));
        m.VerifyWarningLogged(Times.Never());
    }

    [Fact]
    public async Task Sin_CostOfGoodsSold_reversa_solo_InvoiceIssued_sin_error()
    {
        // Venta sin líneas de inventario — SalesInvoiceCogsPostingTranslator nunca publicó
        // PostingFact, así que no existe ningún JournalEntry "CostOfGoodsSold" que reversar.
        var invoiceId = Guid.NewGuid();
        var invoiceIssued = PostedEntry(invoiceId, "InvoiceIssued");
        var m = new Mocks();
        m.JournalEntries
            .Setup(r =>
                r.GetBySourceAsync(TenantId, CompanyId, "Sales", invoiceId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new List<JournalEntry> { invoiceIssued });
        m.Mediator
            .Setup(x => x.Send(It.IsAny<ReverseJournalEntryCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessResult(invoiceIssued));

        var translator = m.BuildTranslator();
        var act = async () => await translator.Handle(Event(invoiceId), CancellationToken.None);

        await act.Should().NotThrowAsync();
        m.Mediator.Verify(
            x => x.Send(It.IsAny<ReverseJournalEntryCommand>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
        m.VerifyWarningLogged(Times.Never());
    }

    [Fact]
    public async Task Sin_ningun_asiento_Posted_no_envia_comandos_ni_genera_warning()
    {
        var invoiceId = Guid.NewGuid();
        var m = new Mocks();
        m.JournalEntries
            .Setup(r =>
                r.GetBySourceAsync(TenantId, CompanyId, "Sales", invoiceId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new List<JournalEntry>());

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
    public async Task Asiento_ya_Reversed_no_se_reversa_de_nuevo_idempotente()
    {
        // Idempotencia: si el mismo evento se reprocesara (reintento), el asiento ya no está en
        // estado Posted (Reverse() ya lo dejó en Reversed) — el filtro Status == Posted ya no lo
        // encuentra, así que no se envía un segundo ReverseJournalEntryCommand.
        var invoiceId = Guid.NewGuid();
        var alreadyReversed = BuildEntry(invoiceId, "InvoiceIssued", post: true);
        alreadyReversed.Reverse(CreatedBy, 2, "Ya reversado antes");

        var m = new Mocks();
        m.JournalEntries
            .Setup(r =>
                r.GetBySourceAsync(TenantId, CompanyId, "Sales", invoiceId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new List<JournalEntry> { alreadyReversed });

        var translator = m.BuildTranslator();
        await translator.Handle(Event(invoiceId), CancellationToken.None);

        m.Mediator.Verify(
            x => x.Send(It.IsAny<ReverseJournalEntryCommand>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Reverso_fallido_de_un_FactType_no_impide_el_otro_y_genera_warning()
    {
        var invoiceId = Guid.NewGuid();
        var invoiceIssued = PostedEntry(invoiceId, "InvoiceIssued");
        var cogs = PostedEntry(invoiceId, "CostOfGoodsSold");
        var m = new Mocks();
        m.JournalEntries
            .Setup(r =>
                r.GetBySourceAsync(TenantId, CompanyId, "Sales", invoiceId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new List<JournalEntry> { invoiceIssued, cogs });

        m.Mediator
            .Setup(x =>
                x.Send(
                    It.Is<ReverseJournalEntryCommand>(c => c.JournalEntryId == invoiceIssued.Id),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(SuccessResult(invoiceIssued));
        m.Mediator
            .Setup(x =>
                x.Send(
                    It.Is<ReverseJournalEntryCommand>(c => c.JournalEntryId == cogs.Id),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                Result<ERP.Application.Modules.Accounting.DTOs.JournalEntryDto>.ValidationFailure(
                    "El período contable está cerrado.",
                    "PERIOD_NOT_OPEN"
                )
            );

        var translator = m.BuildTranslator();
        var act = async () => await translator.Handle(Event(invoiceId), CancellationToken.None);

        await act.Should().NotThrowAsync();
        m.Mediator.Verify(
            x => x.Send(It.IsAny<ReverseJournalEntryCommand>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2)
        );
        m.VerifyWarningLogged(Times.Once());
    }
}
