using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Interfaces;
using ERP.Domain.Modules.ElectronicDocuments.ValueObjects;
using FluentAssertions;

namespace ERP.Application.Tests.ElectronicDocuments;

/// <summary>
/// R12 Fase B — elegibilidad: solo Rejected, tipos 01/04/07, origen procesable, procedencia registrada
/// y todos los errores dentro de 35/36/47/48/52/58/67. Todo lo demás queda bloqueado preventivamente.
/// </summary>
public sealed class ElectronicDocumentRetransmissionPolicyTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly string Key = new('7', 49);

    internal static ElectronicDocument RejectedDocument(
        ElectronicDocumentType type = ElectronicDocumentType.Invoice,
        bool atReception = false
    )
    {
        var document = ElectronicDocument.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            type,
            type == ElectronicDocumentType.Retention ? "Retentions" : "Sales",
            Guid.NewGuid(),
            UserId
        );
        document.MarkXmlGenerated("draft.xml", "1.1.0", "1.1.0", UserId);
        document.MarkSigned("signed.xml", AccessKey.Create(Key), UserId);
        document.SetEnvironment("1");
        document.MarkSent(UserId);
        if (!atReception)
            document.MarkReceived(UserId);
        document.MarkRejected("Rechazado por el SRI", UserId);
        document.ClearDomainEvents();
        return document;
    }

    private static ElectronicDocumentRejectionEvidence Evidence(
        ElectronicDocumentState fromState,
        params (string? Code, string Type)[] messages
    ) =>
        new(
            fromState,
            DateTime.UtcNow,
            messages.Select(m => new SriMessage(m.Code, m.Type, "mensaje", null)).ToList()
        );

    [Theory]
    [InlineData("35")]
    [InlineData("36")]
    [InlineData("47")]
    [InlineData("48")]
    [InlineData("52")]
    [InlineData("58")]
    [InlineData("67")]
    public void Correctable_file_errors_are_eligible(string code)
    {
        var result = ElectronicDocumentRetransmissionPolicy.Evaluate(
            RejectedDocument(),
            ElectronicDocumentSourceLifecycle.Allowed,
            Evidence(ElectronicDocumentState.Received, (code, "ERROR"))
        );

        result.Eligible.Should().BeTrue();
        result.RejectionStage.Should().Be(ElectronicDocumentRejectionStage.Authorization);
        result.RejectionCodes.Should().Equal(code);
    }

    [Theory]
    [InlineData("2")]
    [InlineData("39")]
    [InlineData("43")]
    [InlineData("45")]
    [InlineData("50")]
    [InlineData("56")]
    [InlineData("65")]
    [InlineData("70")]
    public void Other_codes_stay_blocked(string code)
    {
        var result = ElectronicDocumentRetransmissionPolicy.Evaluate(
            RejectedDocument(),
            ElectronicDocumentSourceLifecycle.Allowed,
            Evidence(ElectronicDocumentState.Received, ("52", "ERROR"), (code, "ERROR"))
        );

        result.Eligible.Should().BeFalse();
        result.Blocks.Select(b => b.Code)
            .Should().Contain(ElectronicDocumentRetransmissionPolicy.BlockCodesNotCorrectable);
    }

    [Fact]
    public void Warnings_do_not_decide_but_an_error_without_code_blocks()
    {
        var withWarning = ElectronicDocumentRetransmissionPolicy.Evaluate(
            RejectedDocument(),
            ElectronicDocumentSourceLifecycle.Allowed,
            Evidence(ElectronicDocumentState.Received, ("52", "ERROR"), ("62", "ADVERTENCIA"))
        );
        withWarning.Eligible.Should().BeTrue();

        var uncoded = ElectronicDocumentRetransmissionPolicy.Evaluate(
            RejectedDocument(),
            ElectronicDocumentSourceLifecycle.Allowed,
            Evidence(ElectronicDocumentState.Received, ("52", "ERROR"), (null, "ERROR"))
        );
        uncoded.Eligible.Should().BeFalse();

        var none = ElectronicDocumentRetransmissionPolicy.Evaluate(
            RejectedDocument(),
            ElectronicDocumentSourceLifecycle.Allowed,
            Evidence(ElectronicDocumentState.Received, ("62", "ADVERTENCIA"))
        );
        none.Blocks.Select(b => b.Code)
            .Should().Contain(ElectronicDocumentRetransmissionPolicy.BlockNoErrorCodes);
    }

    [Fact]
    public void Stage_comes_from_the_timeline_not_from_the_code()
    {
        var devuelta = ElectronicDocumentRetransmissionPolicy.Evaluate(
            RejectedDocument(atReception: true),
            ElectronicDocumentSourceLifecycle.Allowed,
            Evidence(ElectronicDocumentState.Sent, ("52", "ERROR"))
        );
        devuelta.RejectionStage.Should().Be(ElectronicDocumentRejectionStage.Reception);

        var unknown = ElectronicDocumentRetransmissionPolicy.Evaluate(
            RejectedDocument(),
            ElectronicDocumentSourceLifecycle.Allowed,
            null
        );
        unknown.Eligible.Should().BeFalse();
        unknown.Blocks.Select(b => b.Code)
            .Should().Contain(ElectronicDocumentRetransmissionPolicy.BlockUnknownOrigin);
    }

    [Fact]
    public void Source_not_processable_or_without_guard_blocks()
    {
        var cancelled = ElectronicDocumentRetransmissionPolicy.Evaluate(
            RejectedDocument(),
            ElectronicDocumentSourceLifecycle.Denied("La factura está anulada."),
            Evidence(ElectronicDocumentState.Received, ("52", "ERROR"))
        );
        cancelled.Eligible.Should().BeFalse();
        cancelled.Blocks.Should().Contain(b => b.Message == "La factura está anulada.");

        var noGuard = ElectronicDocumentRetransmissionPolicy.Evaluate(
            RejectedDocument(),
            null,
            Evidence(ElectronicDocumentState.Received, ("52", "ERROR"))
        );
        noGuard.Eligible.Should().BeFalse();
    }

    [Fact]
    public void Credit_note_follows_the_same_eligibility_as_the_invoice()
    {
        var authorization = ElectronicDocumentRetransmissionPolicy.Evaluate(
            RejectedDocument(ElectronicDocumentType.CreditNote),
            ElectronicDocumentSourceLifecycle.Allowed,
            Evidence(ElectronicDocumentState.Received, ("52", "ERROR"))
        );
        authorization.Eligible.Should().BeTrue();
        authorization.RejectionStage.Should().Be(ElectronicDocumentRejectionStage.Authorization);

        var reception = ElectronicDocumentRetransmissionPolicy.Evaluate(
            RejectedDocument(ElectronicDocumentType.CreditNote, atReception: true),
            ElectronicDocumentSourceLifecycle.Allowed,
            Evidence(ElectronicDocumentState.Sent, ("35", "ERROR"))
        );
        reception.Eligible.Should().BeTrue();
        reception.RejectionStage.Should().Be(ElectronicDocumentRejectionStage.Reception);

        var blockedCode = ElectronicDocumentRetransmissionPolicy.Evaluate(
            RejectedDocument(ElectronicDocumentType.CreditNote),
            ElectronicDocumentSourceLifecycle.Allowed,
            Evidence(ElectronicDocumentState.Received, ("39", "ERROR"))
        );
        blockedCode.Blocks.Select(b => b.Code)
            .Should().Contain(ElectronicDocumentRetransmissionPolicy.BlockCodesNotCorrectable);

        var cancelledReturn = ElectronicDocumentRetransmissionPolicy.Evaluate(
            RejectedDocument(ElectronicDocumentType.CreditNote),
            ElectronicDocumentSourceLifecycle.Denied("La devolución está anulada."),
            Evidence(ElectronicDocumentState.Received, ("52", "ERROR"))
        );
        cancelledReturn.Blocks.Select(b => b.Code)
            .Should().Contain(ElectronicDocumentRetransmissionPolicy.BlockSourceNotProcessable);
    }

    [Fact]
    public void Non_rejected_and_unsupported_types_are_not_eligible()
    {
        var authorized = ElectronicDocument.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ElectronicDocumentType.Invoice,
            "Sales",
            Guid.NewGuid(),
            UserId
        );
        ElectronicDocumentRetransmissionPolicy.Evaluate(authorized, null, null)
            .Blocks.Single().Code.Should().Be(ElectronicDocumentRetransmissionPolicy.BlockNotRejected);

        var debitNote = ElectronicDocumentRetransmissionPolicy.Evaluate(
            RejectedDocument(ElectronicDocumentType.DebitNote),
            ElectronicDocumentSourceLifecycle.Allowed,
            Evidence(ElectronicDocumentState.Received, ("52", "ERROR"))
        );
        debitNote.Blocks.Select(b => b.Code)
            .Should().Contain(ElectronicDocumentRetransmissionPolicy.BlockUnsupportedType);
    }
}
