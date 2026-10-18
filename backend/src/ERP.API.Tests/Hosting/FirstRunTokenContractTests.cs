using ERP.Domain.Setup;
using FluentAssertions;

namespace ERP.API.Tests.Hosting;

public sealed class FirstRunTokenContractTests
{
    [Fact]
    public void Token_is_required_and_expires_at_the_deadline()
    {
        var now = DateTime.UtcNow;
        var state = SystemSetupState.CreateNew();
        state.HasActiveSetupToken(now).Should().BeFalse();
        state.IssueSetupToken("synthetic-hash-only", now.AddMinutes(15));
        state.HasActiveSetupToken(now).Should().BeTrue();
        state.HasActiveSetupToken(now.AddMinutes(15)).Should().BeFalse();
    }

    [Fact]
    public void Initialization_invalidates_token_and_prevents_reissue()
    {
        var now = DateTime.UtcNow;
        var state = SystemSetupState.CreateNew();
        state.IssueSetupToken("synthetic-hash-only", now.AddMinutes(15));
        state.MarkInitialized("synthetic-owner");
        state.SetupTokenHash.Should().BeNull();
        state.SetupTokenExpiryUtc.Should().BeNull();
        state.HasActiveSetupToken(now).Should().BeFalse();
        var reissue = () => state.IssueSetupToken("synthetic-hash-only", now.AddMinutes(15));
        reissue.Should().Throw<ERP.Domain.Exceptions.DomainRuleViolationException>();
    }
}
