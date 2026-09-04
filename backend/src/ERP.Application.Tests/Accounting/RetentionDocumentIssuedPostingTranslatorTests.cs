using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Accounting.Posting.Translators;
using ERP.Application.Modules.Retentions.Exceptions;
using ERP.Domain.Modules.Retentions.Enums;
using ERP.Domain.Modules.Retentions.Events;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Accounting;

/// <summary>
/// RETENTIONS-EXPENSES-INTEGRATION-01D-2 — mismo criterio de posting estricto que
/// <see cref="ExpenseDocumentConfirmedPostingTranslatorTests"/>: un posting fallido LANZA (nunca
/// solo loguea), para que <c>ErpDbContext.SaveChangesAsync</c> revierta toda la confirmación del
/// documento origen (Gasto) + AP + retención.
/// </summary>
public sealed class RetentionDocumentIssuedPostingTranslatorTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid SubjectId = Guid.NewGuid();

    private static RetentionDocumentIssuedEvent Event(
        Guid? retentionDocumentId = null,
        Guid? sourceDocumentId = null,
        decimal totalRetainedVat = 4.50m,
        decimal totalRetainedIncome = 0m,
        decimal? totalRetained = null
    ) =>
        new(
            TenantId,
            retentionDocumentId ?? Guid.NewGuid(),
            CompanyId,
            RetentionSourceDocumentType.ExpenseDocument,
            sourceDocumentId ?? Guid.NewGuid(),
            SubjectId,
            "001-001-000000001",
            totalRetainedVat,
            totalRetainedIncome,
            totalRetained ?? (totalRetainedVat + totalRetainedIncome),
            new DateOnly(2026, 9, 3)
        );

    private sealed class Mocks
    {
        public Mock<IPostingEngine> PostingEngine { get; } = new();

        public RetentionDocumentIssuedPostingTranslator BuildTranslator() => new(PostingEngine.Object);
    }

    [Fact]
    public async Task Evento_valido_construye_PostingFact_con_SourceModule_Retentions()
    {
        var m = new Mocks();
        var retentionId = Guid.NewGuid();
        PostingFact? captured = null;
        m.PostingEngine
            .Setup(e => e.PostAsync(It.IsAny<PostingFact>(), It.IsAny<CancellationToken>()))
            .Callback<PostingFact, CancellationToken>((fact, _) => captured = fact)
            .ReturnsAsync(Result<PostingOutcomeDto>.Success(new PostingOutcomeDto(Guid.NewGuid(), PostingOutcomeStatus.Created)));

        await m.BuildTranslator().Handle(Event(retentionId, totalRetainedVat: 4.50m), CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.TenantId.Should().Be(TenantId);
        captured.CompanyId.Should().Be(CompanyId);
        captured.SourceModule.Should().Be("Retentions");
        captured.FactType.Should().Be("DocumentIssued");
        captured.SourceEventId.Should().Be(retentionId);
        captured.EntryDate.Should().Be(new DateOnly(2026, 9, 3));
        captured.RetainedAmount.Should().Be(4.50m);
        captured.GrandTotal.Should().Be(4.50m);
    }

    [Fact]
    public async Task Posting_exitoso_no_lanza()
    {
        var m = new Mocks();
        m.PostingEngine
            .Setup(e => e.PostAsync(It.IsAny<PostingFact>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PostingOutcomeDto>.Success(new PostingOutcomeDto(Guid.NewGuid(), PostingOutcomeStatus.Created)));

        var act = async () => await m.BuildTranslator().Handle(Event(), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Posting_failure_lanza_RetentionPostingFailedException_en_vez_de_loguear_warning()
    {
        var m = new Mocks();
        m.PostingEngine
            .Setup(e => e.PostAsync(It.IsAny<PostingFact>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PostingOutcomeDto>.ValidationFailure("No existe regla de contabilizacion para Retentions.", "RULE_NOT_FOUND"));

        var act = async () => await m.BuildTranslator().Handle(Event(), CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<RetentionPostingFailedException>();
        thrown.Which.Code.Should().Be("RULE_NOT_FOUND");
        thrown.Which.Message.Should().Be("No existe regla de contabilizacion para Retentions.");
    }
}
