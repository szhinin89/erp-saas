using ERP.Domain.Exceptions;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Events;
using ERP.Domain.Modules.ElectronicDocuments.ValueObjects;
using FluentAssertions;

namespace ERP.Domain.Tests.ElectronicDocuments;

/// <summary>
/// R12 Fase B — única salida de Rejected: el reclamo de la retransmisión corregida conserva la clave,
/// versiona la evidencia, inicia un intento nuevo y marca como históricas las NO AUTORIZADO previas.
/// </summary>
public sealed class ElectronicDocumentCorrectedRetransmissionTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly string Key = new('7', 49);
    private static readonly DateTime NatDate = new(2026, 10, 30, 15, 0, 0, DateTimeKind.Utc);

    private static ElectronicDocument Rejected()
    {
        var document = ElectronicDocument.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ElectronicDocumentType.Invoice,
            "Sales",
            Guid.NewGuid(),
            UserId
        );
        document.MarkXmlGenerated("draft.xml", "1.1.0", "1.1.0", UserId);
        document.MarkSigned("signed.xml", AccessKey.Create(Key), UserId);
        document.MarkSent(UserId);
        document.MarkReceived(UserId);
        document.MarkRetryAttempted(UserId);
        document.MarkRejected("[52] Error en diferencias", UserId);
        document.ClearDomainEvents();
        return document;
    }

    private static void Claim(ElectronicDocument document, string key = "", DateTime? watermark = null, string suffix = "a1") =>
        document.ClaimCorrectedRetransmission(
            $"attempts/{suffix}/draft.xml",
            $"attempts/{suffix}/signed.xml",
            "1.1.0",
            "1.1.0",
            AccessKey.Create(key == "" ? Key : key),
            watermark,
            "Corrección de redondeo en impuestos",
            UserId
        );

    [Fact]
    public void Claim_from_rejected_starts_a_new_attempt_with_the_same_key()
    {
        var document = Rejected();

        Claim(document, watermark: NatDate);

        document.CurrentState.Should().Be(ElectronicDocumentState.Signed);
        document.AccessKey!.Value.Should().Be(Key);
        document.TransmissionAttempt.Should().Be(2);
        document.TransmissionAttemptStartedUtc.Should().NotBeNull();
        document.SupersededSriDecisionUtc.Should().Be(NatDate);
        document.RetryCount.Should().Be(0);
        document.LastError.Should().BeNull();
        document.SignedXmlPath.Should().Be("attempts/a1/signed.xml");
        document.XmlDraftPath.Should().Be("attempts/a1/draft.xml");
        document.RequiresSriReconciliation.Should().BeTrue();
        var claimed = document.DomainEvents.OfType<ElectronicDocumentRetransmissionClaimedEvent>().Single();
        claimed.FromState.Should().Be(ElectronicDocumentState.Rejected);
        claimed.ToState.Should().Be(ElectronicDocumentState.Signed);
        claimed.TransmissionAttempt.Should().Be(2);
    }

    [Theory]
    [InlineData(ElectronicDocumentState.Signed)]
    [InlineData(ElectronicDocumentState.Received)]
    [InlineData(ElectronicDocumentState.Authorized)]
    public void Claim_is_only_allowed_from_rejected(ElectronicDocumentState state)
    {
        var document = Rejected();
        Claim(document);
        if (state is ElectronicDocumentState.Received or ElectronicDocumentState.Authorized)
        {
            document.MarkSent(UserId);
            document.MarkReceived(UserId);
        }
        if (state == ElectronicDocumentState.Authorized)
            document.MarkAuthorized(AuthorizationNumber.Create(Key), DateTime.UtcNow, null, UserId);

        var act = () => Claim(document, suffix: "a2");

        act.Should().Throw<DomainRuleViolationException>();
        document.CurrentState.Should().Be(state);
    }

    [Fact]
    public void Claim_with_a_different_access_key_is_rejected()
    {
        var document = Rejected();

        var act = () => Claim(document, key: new string('8', 49));

        act.Should().Throw<DomainRuleViolationException>().WithMessage("*misma clave*");
        document.CurrentState.Should().Be(ElectronicDocumentState.Rejected);
        document.TransmissionAttempt.Should().Be(1);
    }

    [Fact]
    public void Claim_never_overwrites_the_previous_evidence_paths()
    {
        var document = Rejected();

        var act = () =>
            document.ClaimCorrectedRetransmission(
                "draft.xml",
                "signed.xml",
                "1.1.0",
                "1.1.0",
                AccessKey.Create(Key),
                null,
                "Motivo",
                UserId
            );

        act.Should().Throw<DomainRuleViolationException>().WithMessage("*versión nueva*");
    }

    [Fact]
    public void Claim_requires_a_reason()
    {
        var document = Rejected();

        var act = () =>
            document.ClaimCorrectedRetransmission(
                "a/draft.xml",
                "a/signed.xml",
                "1.1.0",
                "1.1.0",
                AccessKey.Create(Key),
                null,
                " ",
                UserId
            );

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Watermark_never_moves_backwards_across_attempts()
    {
        var document = Rejected();
        Claim(document, watermark: NatDate);
        document.MarkSent(UserId);
        document.MarkReceived(UserId);
        document.MarkRejected("[67] Fecha inválida", UserId);

        Claim(document, watermark: NatDate.AddHours(-1), suffix: "a2");

        document.TransmissionAttempt.Should().Be(3);
        document.SupersededSriDecisionUtc.Should().Be(NatDate);
    }

    [Fact]
    public void Only_decisions_after_the_watermark_belong_to_the_current_attempt()
    {
        var original = Rejected();
        original.IsCurrentAttemptSriDecision(null).Should().BeTrue("sin retransmisión el comportamiento v1.0 no cambia");
        original.IsCurrentAttemptSriDecision(NatDate).Should().BeTrue();

        var document = Rejected();
        Claim(document, watermark: NatDate);

        document.IsCurrentAttemptSriDecision(NatDate.AddMinutes(-5)).Should().BeFalse();
        document.IsCurrentAttemptSriDecision(NatDate).Should().BeFalse("una decisión igual a la marca es histórica");
        document.IsCurrentAttemptSriDecision(NatDate.AddSeconds(1)).Should().BeTrue();
        document.IsCurrentAttemptSriDecision(null).Should().BeFalse("sin fecha nunca cierra el intento");
    }

    [Fact]
    public void Without_previous_decisions_any_dated_decision_belongs_to_the_retransmission()
    {
        var document = Rejected();
        Claim(document, watermark: null);

        document.IsCurrentAttemptSriDecision(NatDate).Should().BeTrue();
        document.IsCurrentAttemptSriDecision(null).Should().BeFalse();
    }
}
