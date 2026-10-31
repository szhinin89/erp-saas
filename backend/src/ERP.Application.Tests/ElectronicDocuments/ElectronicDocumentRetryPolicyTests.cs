using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.ValueObjects;
using FluentAssertions;

namespace ERP.Application.Tests.ElectronicDocuments;

/// <summary>
/// La política existe una sola vez como predicado; estos tests lo compilan en memoria (el mismo
/// árbol que EF traduce a SQL en <c>GetRetryCandidatesAsync</c>).
/// </summary>
public sealed class ElectronicDocumentRetryPolicyTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private static ElectronicDocument Failed(int attempts)
    {
        var document = ElectronicDocument.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ElectronicDocumentType.Invoice,
            "Sales",
            Guid.NewGuid(),
            UserId
        );
        for (var i = 0; i < attempts; i++)
            document.MarkFailed("Fallo de prueba", UserId);
        return document;
    }

    private static bool Eligible(ElectronicDocument document, DateTime nowUtc) =>
        ElectronicDocumentRetryPolicy.AutomaticRetryCandidate(nowUtc).Compile()(document);

    [Fact]
    public void Eligible_when_no_previous_attempt()
    {
        var draft = Failed(0);
        draft.LastAttemptUtc.Should().BeNull();
        Eligible(draft, DateTime.UtcNow).Should().BeTrue();
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 4)]
    [InlineData(3, 8)]
    [InlineData(4, 16)]
    public void Backoff_follows_1_2_4_8_16_minutes(int attempts, int backoffMinutes)
    {
        var document = Failed(attempts);
        var last = document.LastAttemptUtc!.Value;

        Eligible(document, last.AddMinutes(backoffMinutes).AddSeconds(-1)).Should().BeFalse();
        Eligible(document, last.AddMinutes(backoffMinutes)).Should().BeTrue("exactamente en el borde");
    }

    [Fact]
    public void Not_eligible_when_max_attempts_reached()
    {
        var exhausted = Failed(ERP.Domain.Configuration.Definitions.Modules.ElectronicDocumentRetryMaximum.Default);
        Eligible(exhausted, exhausted.LastAttemptUtc!.Value.AddDays(1)).Should().BeFalse();
    }

    private static ElectronicDocument SignedWithAttempts(int attempts)
    {
        var document = Failed(0);
        document.MarkXmlGenerated("sales/draft.xml", "1.1.0", "1.1.0", UserId);
        document.MarkSigned("sales/signed.xml", AccessKey.Create(new string('1', 49)), UserId);
        for (var i = 0; i < attempts; i++)
            document.MarkRetryAttempted(UserId);
        return document;
    }

    [Fact]
    public void Sri_outcome_pending_stays_eligible_past_the_company_maximum()
    {
        // R11: el fin de la consulta lo decide la ventana de 24 h (issuer), no el máximo de empresa.
        var signed = SignedWithAttempts(ERP.Domain.Configuration.Definitions.Modules.ElectronicDocumentRetryMaximum.Default + 4);
        Eligible(signed, signed.LastAttemptUtc!.Value.AddMinutes(60)).Should().BeTrue();
    }

    [Theory]
    [InlineData(5, 32)]
    [InlineData(6, 60)]
    [InlineData(12, 60)]
    public void Sri_outcome_pending_backoff_is_capped_at_60_minutes(int attempts, int backoffMinutes)
    {
        var signed = SignedWithAttempts(attempts);
        var last = signed.LastAttemptUtc!.Value;

        Eligible(signed, last.AddMinutes(backoffMinutes).AddSeconds(-1)).Should().BeFalse();
        Eligible(signed, last.AddMinutes(backoffMinutes)).Should().BeTrue();
    }

    [Fact]
    public void Sri_outcome_pending_states_are_signed_and_received_only()
    {
        ElectronicDocumentRetryPolicy.SriOutcomePendingStates.Should().BeEquivalentTo(
            new[] { ElectronicDocumentState.Signed, ElectronicDocumentState.Received });
        ElectronicDocumentRetryPolicy.SriProcessingWindow.Should().Be(TimeSpan.FromHours(24));
    }

    [Fact]
    public void Not_eligible_outside_automatic_states()
    {
        var document = Failed(0);
        document.MarkXmlGenerated("sales/draft.xml", "1.1.0", "1.1.0", UserId);
        document.CurrentState.Should().Be(ElectronicDocumentState.XmlGenerated);
        Eligible(document, DateTime.UtcNow.AddDays(1)).Should().BeFalse();
    }

    [Fact]
    public void Automatic_retry_states_are_exactly_the_states_the_job_has_always_picked_up()
    {
        ElectronicDocumentRetryPolicy
            .AutomaticRetryStates.Should()
            .BeEquivalentTo(
                new[]
                {
                    ElectronicDocumentState.Signed,
                    ElectronicDocumentState.Received,
                    ElectronicDocumentState.Draft,
                    ElectronicDocumentState.Failed,
                }
            );
    }
}
