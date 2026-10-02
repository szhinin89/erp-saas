using ERP.Domain.Exceptions;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Events;
using ERP.Domain.Modules.ElectronicDocuments.ValueObjects;
using FluentAssertions;

namespace ERP.Domain.Tests.ElectronicDocuments;

/// <summary>
/// ZH-RETENTION-ELECTRONIC-LIFECYCLE-01A (ADR-036) — estados Dispatching (reclamo de transmisión
/// persistido antes de llamar al SRI) y Discarded (terminal, solo con certeza de que nunca hubo
/// intento externo).
/// </summary>
public sealed class ElectronicDocumentDispatchLifecycleTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private static ElectronicDocument NewDraft() =>
        ElectronicDocument.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ElectronicDocumentType.Retention,
            "Retentions",
            Guid.NewGuid(),
            UserId
        );

    private static ElectronicDocument Signed()
    {
        var document = NewDraft();
        document.MarkXmlGenerated("draft.xml", "2.0.0", "2.0.0", UserId);
        document.MarkSigned("signed.xml", AccessKey.Create(new string('4', 49)), UserId);
        return document;
    }

    private static ElectronicDocument Dispatching()
    {
        var document = Signed();
        document.MarkDispatching(UserId);
        return document;
    }

    [Fact]
    public void MarkDispatching_from_signed_transitions_and_raises_event()
    {
        var document = Signed();

        document.MarkDispatching(UserId);

        document.CurrentState.Should().Be(ElectronicDocumentState.Dispatching);
        document.LastAttemptUtc.Should().NotBeNull();
        document.DomainEvents.OfType<ElectronicDocumentDispatchingEvent>().Should().ContainSingle()
            .Which.FromState.Should().Be(ElectronicDocumentState.Signed);
    }

    [Theory]
    [InlineData(ElectronicDocumentState.Draft)]
    [InlineData(ElectronicDocumentState.Dispatching)]
    public void MarkDispatching_outside_signed_throws(ElectronicDocumentState state)
    {
        var document = state == ElectronicDocumentState.Draft ? NewDraft() : Dispatching();

        var act = () => document.MarkDispatching(UserId);

        act.Should().Throw<DomainRuleViolationException>();
    }

    [Fact]
    public void Dispatching_can_reach_sent_received_and_authorized()
    {
        var document = Dispatching();

        document.MarkSent(UserId);
        document.MarkReceived(UserId);
        document.MarkAuthorized(AuthorizationNumber.Create(new string('4', 49)), DateTime.UtcNow, null, UserId);

        document.CurrentState.Should().Be(ElectronicDocumentState.Authorized);
    }

    [Fact]
    public void Dispatching_resolves_directly_by_authorization_query_without_resending()
    {
        var authorized = Dispatching();
        authorized.MarkAuthorized(AuthorizationNumber.Create(new string('4', 49)), DateTime.UtcNow, null, UserId);
        var rejected = Dispatching();
        rejected.MarkRejected("NO AUTORIZADO", UserId);

        authorized.CurrentState.Should().Be(ElectronicDocumentState.Authorized);
        rejected.CurrentState.Should().Be(ElectronicDocumentState.Rejected);
    }

    [Fact]
    public void Historical_signed_resolves_by_authorization_query()
    {
        var document = Signed();

        document.MarkAuthorized(AuthorizationNumber.Create(new string('4', 49)), DateTime.UtcNow, null, UserId);

        document.CurrentState.Should().Be(ElectronicDocumentState.Authorized);
    }

    [Fact]
    public void Dispatching_counts_query_attempts_and_dead_letters_then_reactivates_back_to_dispatching()
    {
        var document = Dispatching();

        document.MarkRetryAttempted(UserId);
        document.MarkDeadLetter("Reintentos agotados", UserId);
        document.PreDeadLetterState.Should().Be(ElectronicDocumentState.Dispatching);
        document.Reactivate(UserId);

        document.RetryCount.Should().Be(1);
        document.CurrentState.Should().Be(ElectronicDocumentState.Dispatching);
    }

    [Fact]
    public void Discard_from_draft_is_terminal_records_reason_and_raises_event()
    {
        var document = NewDraft();

        document.MarkDiscarded("Anulación automática por anulación de compra.", UserId);

        document.CurrentState.Should().Be(ElectronicDocumentState.Discarded);
        document.LastError.Should().Be("Anulación automática por anulación de compra.");
        document.DomainEvents.OfType<ElectronicDocumentDiscardedEvent>().Should().ContainSingle()
            .Which.FromState.Should().Be(ElectronicDocumentState.Draft);
        document.CanBeDiscarded.Should().BeFalse();
        ((Action)(() => document.MarkFailed("x", UserId))).Should().Throw<DomainRuleViolationException>();
        ((Action)(() => document.MarkXmlGenerated("d.xml", "2.0.0", "2.0.0", UserId))).Should().Throw<DomainRuleViolationException>();
        ((Action)(() => document.Reactivate(UserId))).Should().Throw<DomainRuleViolationException>();
    }

    [Fact]
    public void Discard_from_failed_and_from_dead_letter_of_failed_is_allowed()
    {
        var failed = NewDraft();
        failed.MarkFailed("XSD", UserId);
        var deadLetter = NewDraft();
        deadLetter.MarkFailed("XSD", UserId);
        deadLetter.MarkDeadLetter("Agotado", UserId);

        failed.MarkDiscarded("origen anulado", UserId);
        deadLetter.MarkDiscarded("origen anulado", UserId);

        failed.CurrentState.Should().Be(ElectronicDocumentState.Discarded);
        deadLetter.CurrentState.Should().Be(ElectronicDocumentState.Discarded);
        deadLetter.PreDeadLetterState.Should().BeNull();
    }

    [Fact]
    public void Discard_is_never_allowed_once_the_document_could_have_reached_the_sri()
    {
        var signed = Signed();
        var dispatching = Dispatching();
        var deadLetterOfSigned = Signed();
        deadLetterOfSigned.MarkDeadLetter("Agotado", UserId);

        foreach (var document in new[] { signed, dispatching, deadLetterOfSigned })
        {
            document.CanBeDiscarded.Should().BeFalse();
            var act = () => document.MarkDiscarded("origen anulado", UserId);
            act.Should().Throw<DomainRuleViolationException>();
        }
    }

    [Fact]
    public void Discard_requires_a_reason()
    {
        var act = () => NewDraft().MarkDiscarded(" ", UserId);

        act.Should().Throw<ArgumentException>();
    }
}
