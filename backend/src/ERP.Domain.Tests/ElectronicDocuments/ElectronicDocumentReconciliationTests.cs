using ERP.Domain.Exceptions;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Events;
using ERP.Domain.Modules.ElectronicDocuments.ValueObjects;
using FluentAssertions;

namespace ERP.Domain.Tests.ElectronicDocuments;

/// <summary>
/// Caso #32 — comprobante con resultado SRI desconocido: requiere conciliación y admite evidencia
/// manual que nunca cambia su estado.
/// </summary>
public sealed class ElectronicDocumentReconciliationTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly string Key = new('7', 49);

    private static ElectronicDocument Signed()
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
        return document;
    }

    private static ElectronicDocument Received()
    {
        var document = Signed();
        document.MarkSent(UserId);
        document.MarkReceived(UserId);
        return document;
    }

    [Fact]
    public void Uncertain_states_and_their_dead_letter_require_reconciliation()
    {
        Signed().RequiresSriReconciliation.Should().BeTrue();
        Received().RequiresSriReconciliation.Should().BeTrue();

        var dispatching = Signed();
        dispatching.MarkDispatching(UserId);
        dispatching.RequiresSriReconciliation.Should().BeTrue();

        var deadLetter = Received();
        deadLetter.MarkDeadLetter("Sin respuesta del SRI.", UserId);
        deadLetter.RequiresSriReconciliation.Should().BeTrue();
    }

    [Fact]
    public void Known_outcomes_and_never_transmitted_documents_do_not_require_reconciliation()
    {
        var authorized = Received();
        authorized.MarkAuthorized(AuthorizationNumber.Create(Key), DateTime.UtcNow, null, UserId);
        authorized.RequiresSriReconciliation.Should().BeFalse();

        var failed = ElectronicDocument.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ElectronicDocumentType.Invoice,
            "Sales",
            Guid.NewGuid(),
            UserId
        );
        failed.MarkFailed("XSD inválido.", UserId);
        failed.MarkDeadLetter("Reintentos agotados.", UserId);
        failed.RequiresSriReconciliation.Should().BeFalse();
    }

    [Fact]
    public void RecordReconciliationEvidence_audits_evidence_without_changing_state()
    {
        var document = Received();
        document.ClearDomainEvents();

        document.RecordReconciliationEvidence(
            " SRI en Línea 2026-10-29 10:15 ",
            " Sin registro de la clave ",
            UserId
        );

        document.CurrentState.Should().Be(ElectronicDocumentState.Received);
        document.AuthorizationNumber.Should().BeNull();
        var evt = document
            .DomainEvents.OfType<ElectronicDocumentReconciliationRecordedEvent>()
            .Should()
            .ContainSingle()
            .Subject;
        evt.State.Should().Be(ElectronicDocumentState.Received);
        evt.Evidence.Should()
            .Be("Referencia: SRI en Línea 2026-10-29 10:15 — Observación: Sin registro de la clave");
    }

    [Fact]
    public void RecordReconciliationEvidence_on_known_outcome_throws()
    {
        var document = Received();
        document.MarkAuthorized(AuthorizationNumber.Create(Key), DateTime.UtcNow, null, UserId);

        var act = () => document.RecordReconciliationEvidence("ref", "obs", UserId);

        act.Should().Throw<DomainRuleViolationException>();
    }

    [Theory]
    [InlineData("", "obs")]
    [InlineData("ref", " ")]
    public void RecordReconciliationEvidence_requires_reference_and_observation(
        string reference,
        string observation
    )
    {
        var act = () => Signed().RecordReconciliationEvidence(reference, observation, UserId);

        act.Should().Throw<ArgumentException>();
    }
}
