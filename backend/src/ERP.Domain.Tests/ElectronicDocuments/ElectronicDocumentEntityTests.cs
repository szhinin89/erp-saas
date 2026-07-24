using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.ValueObjects;
using FluentAssertions;

namespace ERP.Domain.Tests.ElectronicDocuments;

public sealed class ElectronicDocumentEntityTests
{
    private static ElectronicDocument NewDraft() => ElectronicDocument.Create(
        tenantId: Guid.NewGuid(),
        companyId: Guid.NewGuid(),
        documentType: ElectronicDocumentType.Invoice,
        sourceModule: "Sales",
        sourceEntityId: Guid.NewGuid(),
        createdBy: Guid.NewGuid());

    [Fact]
    public void MarkXmlGenerated_from_draft_transitions_and_sets_paths()
    {
        var document = NewDraft();

        document.MarkXmlGenerated("electronic-documents/x/invoice/y/draft.xml", "1.1.0", "1.1.0", Guid.NewGuid());

        document.CurrentState.Should().Be(ElectronicDocumentState.XmlGenerated);
        document.XmlDraftPath.Should().Be("electronic-documents/x/invoice/y/draft.xml");
        document.XmlVersion.Should().Be("1.1.0");
        document.SchemaVersion.Should().Be("1.1.0");
    }

    [Fact]
    public void MarkXmlGenerated_twice_throws_because_state_is_no_longer_draft()
    {
        var document = NewDraft();
        document.MarkXmlGenerated("path/draft.xml", "1.1.0", "1.1.0", Guid.NewGuid());

        var act = () => document.MarkXmlGenerated("path/draft2.xml", "1.1.0", "1.1.0", Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkSigned_before_xml_generated_throws()
    {
        var document = NewDraft();

        var act = () => document.MarkSigned("path/signed.xml", AccessKey.Create(new string('1', 49)), Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkSigned_after_xml_generated_transitions_and_sets_access_key()
    {
        var document = NewDraft();
        document.MarkXmlGenerated("path/draft.xml", "1.1.0", "1.1.0", Guid.NewGuid());
        var accessKey = AccessKey.Create(new string('7', 49));

        document.MarkSigned("path/signed.xml", accessKey, Guid.NewGuid());

        document.CurrentState.Should().Be(ElectronicDocumentState.Signed);
        document.SignedXmlPath.Should().Be("path/signed.xml");
        document.AccessKey.Should().Be(accessKey);
    }

    [Fact]
    public void Full_pipeline_never_leaves_authorized_path_set_this_phase()
    {
        var document = NewDraft();
        document.MarkXmlGenerated("path/draft.xml", "1.1.0", "1.1.0", Guid.NewGuid());
        document.MarkSigned("path/signed.xml", AccessKey.Create(new string('3', 49)), Guid.NewGuid());

        document.AuthorizedXmlPath.Should().BeNull();
        document.AuthorizationNumber.Should().BeNull();
    }

    private static ElectronicDocument SignedDocument()
    {
        var document = NewDraft();
        document.MarkXmlGenerated("path/draft.xml", "1.1.0", "1.1.0", Guid.NewGuid());
        document.MarkSigned("path/signed.xml", AccessKey.Create(new string('3', 49)), Guid.NewGuid());
        return document;
    }

    // ── Fase 10: nuevas transiciones (Sent/Received/Authorized/Rejected/DeadLetter/Cancelled) ──

    [Fact]
    public void MarkSent_from_signed_transitions_to_sent()
    {
        var document = SignedDocument();

        document.MarkSent(Guid.NewGuid());

        document.CurrentState.Should().Be(ElectronicDocumentState.Sent);
    }

    [Fact]
    public void MarkSent_from_draft_throws()
    {
        var document = NewDraft();

        var act = () => document.MarkSent(Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkReceived_from_sent_transitions_to_received()
    {
        var document = SignedDocument();
        document.MarkSent(Guid.NewGuid());

        document.MarkReceived(Guid.NewGuid());

        document.CurrentState.Should().Be(ElectronicDocumentState.Received);
    }

    [Fact]
    public void MarkReceived_before_sent_throws()
    {
        var document = SignedDocument();

        var act = () => document.MarkReceived(Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkAuthorized_from_sent_sets_authorization_data_and_transitions()
    {
        var document = SignedDocument();
        document.MarkSent(Guid.NewGuid());
        var authNumber = AuthorizationNumber.Create(new string('9', 49));
        var authDate = DateTime.UtcNow;

        document.MarkAuthorized(authNumber, authDate, "path/authorized.xml", Guid.NewGuid());

        document.CurrentState.Should().Be(ElectronicDocumentState.Authorized);
        document.AuthorizationNumber.Should().Be(authNumber);
        document.AuthorizationDate.Should().Be(authDate);
        document.AuthorizedXmlPath.Should().Be("path/authorized.xml");
    }

    [Fact]
    public void MarkAuthorized_from_received_also_transitions()
    {
        var document = SignedDocument();
        document.MarkSent(Guid.NewGuid());
        document.MarkReceived(Guid.NewGuid());

        document.MarkAuthorized(AuthorizationNumber.Create(new string('9', 49)), DateTime.UtcNow, null, Guid.NewGuid());

        document.CurrentState.Should().Be(ElectronicDocumentState.Authorized);
    }

    [Fact]
    public void MarkAuthorized_before_sent_throws()
    {
        var document = SignedDocument();

        var act = () => document.MarkAuthorized(AuthorizationNumber.Create(new string('9', 49)), DateTime.UtcNow, null, Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkRejected_from_sent_transitions_and_requires_a_reason()
    {
        var document = SignedDocument();
        document.MarkSent(Guid.NewGuid());

        document.MarkRejected("Clave de acceso duplicada.", Guid.NewGuid());

        document.CurrentState.Should().Be(ElectronicDocumentState.Rejected);
    }

    [Fact]
    public void MarkRejected_with_blank_reason_throws()
    {
        var document = SignedDocument();
        document.MarkSent(Guid.NewGuid());

        var act = () => document.MarkRejected("   ", Guid.NewGuid());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkRejected_before_sent_throws()
    {
        var document = SignedDocument();

        var act = () => document.MarkRejected("motivo", Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkDeadLetter_from_sent_transitions()
    {
        var document = SignedDocument();
        document.MarkSent(Guid.NewGuid());

        document.MarkDeadLetter("El SRI no respondió tras varios reintentos.", Guid.NewGuid());

        document.CurrentState.Should().Be(ElectronicDocumentState.DeadLetter);
    }

    [Fact]
    public void MarkDeadLetter_from_signed_transitions_and_records_pre_dead_letter_state()
    {
        var document = SignedDocument();

        document.MarkDeadLetter("Reintentos agotados tras 5 intentos.", Guid.NewGuid());

        document.CurrentState.Should().Be(ElectronicDocumentState.DeadLetter);
        document.PreDeadLetterState.Should().Be(ElectronicDocumentState.Signed);
    }

    [Fact]
    public void MarkDeadLetter_from_received_records_received_as_pre_dead_letter_state()
    {
        var document = SignedDocument();
        document.MarkSent(Guid.NewGuid());
        document.MarkReceived(Guid.NewGuid());

        document.MarkDeadLetter("Timeout de autorización.", Guid.NewGuid());

        document.PreDeadLetterState.Should().Be(ElectronicDocumentState.Received);
    }

    [Fact]
    public void MarkDeadLetter_before_signed_throws()
    {
        var document = NewDraft();
        document.MarkXmlGenerated("path/draft.xml", "1.1.0", "1.1.0", Guid.NewGuid());

        var act = () => document.MarkDeadLetter("motivo", Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkRetryAttempted_from_signed_increments_retry_count()
    {
        var document = SignedDocument();

        document.MarkRetryAttempted(Guid.NewGuid());

        document.RetryCount.Should().Be(1);
        document.LastAttemptUtc.Should().NotBeNull();
        document.CurrentState.Should().Be(ElectronicDocumentState.Signed);
    }

    [Fact]
    public void MarkRetryAttempted_from_draft_throws()
    {
        var document = NewDraft();

        var act = () => document.MarkRetryAttempted(Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Reactivate_from_dead_letter_restores_pre_dead_letter_state()
    {
        var document = SignedDocument();
        document.MarkSent(Guid.NewGuid());
        document.MarkReceived(Guid.NewGuid());
        document.MarkDeadLetter("Timeout de autorización.", Guid.NewGuid());

        document.Reactivate(Guid.NewGuid());

        document.CurrentState.Should().Be(ElectronicDocumentState.Received);
        document.PreDeadLetterState.Should().BeNull();
    }

    [Fact]
    public void Reactivate_outside_dead_letter_throws()
    {
        var document = SignedDocument();

        var act = () => document.Reactivate(Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkCancelled_from_authorized_transitions()
    {
        var document = SignedDocument();
        document.MarkSent(Guid.NewGuid());
        document.MarkAuthorized(AuthorizationNumber.Create(new string('9', 49)), DateTime.UtcNow, null, Guid.NewGuid());

        document.MarkCancelled("Anulación solicitada por el cliente.", Guid.NewGuid());

        document.CurrentState.Should().Be(ElectronicDocumentState.Cancelled);
    }

    [Fact]
    public void MarkCancelled_before_authorized_throws()
    {
        var document = SignedDocument();
        document.MarkSent(Guid.NewGuid());

        var act = () => document.MarkCancelled("motivo", Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>();
    }
}
