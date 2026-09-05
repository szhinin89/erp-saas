using ERP.Application.Common;
using ERP.Application.Modules.Accounting.DTOs;
using ERP.Application.Modules.Accounting.Posting.Translators;
using ERP.Application.Modules.Accounting.UseCases.JournalEntries;
using ERP.Application.Modules.Retentions.Exceptions;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Retentions.Enums;
using ERP.Domain.Modules.Retentions.Events;
using FluentAssertions;
using MediatR;
using Moq;

namespace ERP.Application.Tests.Accounting;

/// <summary>
/// RETENTIONS-EXPENSES-INTEGRATION-01D-3 — mismo criterio que
/// <see cref="RetentionDocumentIssuedPostingTranslatorTests"/>/<c>ExpenseDocumentCancelledPostingTranslator</c>:
/// localiza el asiento Posted original de la retención (SourceModule="Retentions",
/// SourceEventType="DocumentIssued") y delega el reverso en <see cref="ReverseJournalEntryCommand"/>
/// — nunca resuelve cuentas nuevas. Posting ESTRICTO (a diferencia de
/// <c>PurchaseInvoiceCancelledPostingTranslator</c>): si no encuentra el asiento o el reverso falla,
/// LANZA <see cref="RetentionPostingFailedException"/> en vez de solo loguear un warning.
/// </summary>
public sealed class RetentionDocumentCancelledPostingTranslatorTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid SubjectId = Guid.NewGuid();
    private static readonly Guid CreatedBy = Guid.NewGuid();

    private static RetentionDocumentCancelledEvent Event(
        Guid? retentionDocumentId = null,
        Guid? sourceDocumentId = null,
        decimal totalRetained = 4.50m,
        string cancelReason = "Cancelado junto con el gasto origen: Documento duplicado"
    ) =>
        new(
            TenantId,
            retentionDocumentId ?? Guid.NewGuid(),
            CompanyId,
            RetentionSourceDocumentType.ExpenseDocument,
            sourceDocumentId ?? Guid.NewGuid(),
            SubjectId,
            "001-001-000000001",
            totalRetained,
            cancelReason
        );

    private static JournalEntry PostedEntry(Guid sourceEventId, string sourceEventType = "DocumentIssued")
    {
        var entry = JournalEntry.Create(
            TenantId,
            CompanyId,
            new DateOnly(2026, 8, 27),
            Guid.NewGuid(),
            2026,
            "Retentions",
            sourceEventType,
            sourceEventId,
            "Retención de IVA",
            CreatedBy
        );
        entry.AddLine(Guid.NewGuid(), null, 4.50m, 0m);
        entry.AddLine(Guid.NewGuid(), null, 0m, 4.50m);
        entry.Post(CreatedBy, 1);
        return entry;
    }

    private sealed class Mocks
    {
        public Mock<IJournalEntryRepository> JournalEntryRepo { get; } = new();
        public Mock<IMediator> Mediator { get; } = new();

        public RetentionDocumentCancelledPostingTranslator BuildTranslator() =>
            new(JournalEntryRepo.Object, Mediator.Object);
    }

    [Fact]
    public async Task Localiza_asiento_Posted_por_SourceModule_Retentions_y_DocumentIssued()
    {
        var m = new Mocks();
        var retentionId = Guid.NewGuid();
        var original = PostedEntry(retentionId);
        m.JournalEntryRepo
            .Setup(r => r.GetBySourceAsync(TenantId, CompanyId, "Retentions", retentionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<JournalEntry> { original });

        ReverseJournalEntryCommand? sent = null;
        m.Mediator
            .Setup(x => x.Send(It.IsAny<ReverseJournalEntryCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<JournalEntryDto>>, CancellationToken>((cmd, _) => sent = (ReverseJournalEntryCommand)cmd)
            .ReturnsAsync(Result<JournalEntryDto>.Success(DummyDto(original.Id)));

        await m.BuildTranslator().Handle(Event(retentionId), CancellationToken.None);

        sent.Should().NotBeNull();
        sent!.JournalEntryId.Should().Be(original.Id);
        sent.Reason.Should().Contain("001-001-000000001").And.Contain("Cancelado junto con el gasto origen");
    }

    /// <summary>
    /// PURCHASES-RETENTIONS-CANCEL-05D — el translator localiza el asiento por
    /// SourceModule="Retentions"+RetentionDocumentId únicamente; nunca inspecciona
    /// <c>SourceDocumentType</c> del evento, así que reversa igual para una retención originada en
    /// <c>PurchaseInvoice</c> que en <c>ExpenseDocument</c> — sin necesidad de un translator
    /// separado para Compras (confirmado por diseño en PURCHASES-WITHHOLDING-RETENTIONS-AUDIT-05A).
    /// </summary>
    [Fact]
    public async Task Reversa_igual_para_una_retencion_originada_en_PurchaseInvoice()
    {
        var m = new Mocks();
        var retentionId = Guid.NewGuid();
        var original = PostedEntry(retentionId);
        m.JournalEntryRepo
            .Setup(r => r.GetBySourceAsync(TenantId, CompanyId, "Retentions", retentionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<JournalEntry> { original });

        ReverseJournalEntryCommand? sent = null;
        m.Mediator
            .Setup(x => x.Send(It.IsAny<ReverseJournalEntryCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<JournalEntryDto>>, CancellationToken>((cmd, _) => sent = (ReverseJournalEntryCommand)cmd)
            .ReturnsAsync(Result<JournalEntryDto>.Success(DummyDto(original.Id)));

        var purchaseEvent = new RetentionDocumentCancelledEvent(
            TenantId, retentionId, CompanyId, RetentionSourceDocumentType.PurchaseInvoice,
            Guid.NewGuid(), SubjectId, "001-001-000000005", 30m, "Anulación de prueba"
        );

        await m.BuildTranslator().Handle(purchaseEvent, CancellationToken.None);

        sent.Should().NotBeNull();
        sent!.JournalEntryId.Should().Be(original.Id);
    }

    [Fact]
    public async Task No_encontrar_el_asiento_original_lanza_en_vez_de_loguear()
    {
        var m = new Mocks();
        m.JournalEntryRepo
            .Setup(r => r.GetBySourceAsync(TenantId, CompanyId, "Retentions", It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<JournalEntry>());

        var act = async () => await m.BuildTranslator().Handle(Event(), CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<RetentionPostingFailedException>();
        thrown.Which.Code.Should().Be("JOURNAL_ENTRY_NOT_FOUND");
    }

    [Fact]
    public async Task Ignora_asientos_de_otro_SourceEventType_del_mismo_SourceEventId()
    {
        var m = new Mocks();
        var retentionId = Guid.NewGuid();
        var notTheIssuance = PostedEntry(retentionId, sourceEventType: "SomethingElse");
        m.JournalEntryRepo
            .Setup(r => r.GetBySourceAsync(TenantId, CompanyId, "Retentions", retentionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<JournalEntry> { notTheIssuance });

        var act = async () => await m.BuildTranslator().Handle(Event(retentionId), CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<RetentionPostingFailedException>();
        thrown.Which.Code.Should().Be("JOURNAL_ENTRY_NOT_FOUND");
    }

    [Fact]
    public async Task Si_el_reverso_falla_lanza_RetentionPostingFailedException_en_vez_de_loguear()
    {
        var m = new Mocks();
        var retentionId = Guid.NewGuid();
        var original = PostedEntry(retentionId);
        m.JournalEntryRepo
            .Setup(r => r.GetBySourceAsync(TenantId, CompanyId, "Retentions", retentionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<JournalEntry> { original });
        m.Mediator
            .Setup(x => x.Send(It.IsAny<ReverseJournalEntryCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<JournalEntryDto>.ValidationFailure("El período contable está cerrado.", "PERIOD_CLOSED"));

        var act = async () => await m.BuildTranslator().Handle(Event(retentionId), CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<RetentionPostingFailedException>();
        thrown.Which.Code.Should().Be("PERIOD_CLOSED");
    }

    private static JournalEntryDto DummyDto(Guid originalId) =>
        new(
            Guid.NewGuid(),
            new DateOnly(2026, 9, 3),
            Guid.NewGuid(),
            2026,
            "Accounting",
            "Reversal",
            originalId,
            "Reverso",
            "Posted",
            2,
            DateTime.UtcNow,
            originalId,
            null,
            null,
            null
        );
}
