using ERP.Application.Setup;
using ERP.Domain.Setup;
using ERP.Infrastructure.Setup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ERP.Infrastructure.Tests.Setup;

[CollectionDefinition("FirstRunConsole", DisableParallelization = true)]
public sealed class FirstRunConsoleCollection { }

// Only isolated test tokens; output is discarded and never included in assertions or logs.
[Collection("FirstRunConsole")]
public sealed class FirstRunSetupServiceTests
{
    [Theory]
    [InlineData(false, 15)]
    [InlineData(true, 7)]
    public async Task Missing_state_or_token_is_issued_and_printed_only_after_save(bool existing, int minutes)
    {
        var state = existing ? SystemSetupState.CreateNew() : null;
        var repo = Repository(state);
        repo.Setup(x => x.AddAsync(It.IsAny<SystemSetupState>(), It.IsAny<CancellationToken>()))
            .Callback<SystemSetupState, CancellationToken>((value, _) => state = value)
            .Returns(Task.CompletedTask);
        using var output = new StringWriter();
        repo.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => Assert.True(output.GetStringBuilder().Length == 0))
            .Returns(Task.CompletedTask);
        var before = DateTime.UtcNow;
        await WithConsole(output, () => Service(repo, minutes).EnsureSetupTokenAsync());
        Assert.NotNull(state);
        Assert.InRange(state.SetupTokenExpiryUtc!.Value, before.AddMinutes(minutes), DateTime.UtcNow.AddMinutes(minutes));
        AssertPrintedHashMatches(output, state);
        repo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(15)]
    [InlineData(-15)]
    public async Task Startup_replaces_active_or_expired_token(int previousMinutes)
    {
        var state = SystemSetupState.CreateNew();
        state.IssueSetupToken("synthetic-previous-hash", DateTime.UtcNow.AddMinutes(previousMinutes));
        var repo = Repository(state);
        using var output = new StringWriter();
        await WithConsole(output, () => Service(repo).EnsureSetupTokenAsync());
        Assert.True(state.SetupTokenHash != "synthetic-previous-hash");
        Assert.True(state.HasActiveSetupToken(DateTime.UtcNow));
        Assert.False(state.HasActiveSetupToken(state.SetupTokenExpiryUtc!.Value));
        AssertPrintedHashMatches(output, state);
    }

    [Fact]
    public async Task Initialized_state_does_not_save_or_print()
    {
        var state = SystemSetupState.CreateNew();
        state.MarkInitialized("synthetic-owner");
        var repo = Repository(state);
        using var output = new StringWriter();
        await WithConsole(output, () => Service(repo).EnsureSetupTokenAsync());
        Assert.True(output.GetStringBuilder().Length == 0);
        Assert.Null(state.SetupTokenHash);
        repo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        repo.Verify(x => x.AddAsync(It.IsAny<SystemSetupState>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Persistence_failure_does_not_print()
    {
        var repo = Repository(SystemSetupState.CreateNew());
        repo.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Synthetic persistence failure"));
        using var output = new StringWriter();
        await WithConsole(output, async () =>
            await Assert.ThrowsAsync<InvalidOperationException>(() => Service(repo).EnsureSetupTokenAsync()));
        Assert.True(output.GetStringBuilder().Length == 0);
    }

    private static Mock<ISystemSetupRepository> Repository(SystemSetupState? state)
    {
        var repo = new Mock<ISystemSetupRepository>(MockBehavior.Strict);
        repo.Setup(x => x.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(state);
        repo.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        return repo;
    }

    private static FirstRunSetupService Service(Mock<ISystemSetupRepository> repo, int minutes = 15) =>
        new(repo.Object, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Setup:TokenExpiryMinutes"] = minutes.ToString(),
        }).Build(), NullLogger<FirstRunSetupService>.Instance, new IsolatedSession());

    private sealed class IsolatedSession : IFirstRunSession
    {
        public Task RunStartupAsync(Func<CancellationToken, Task> operation, CancellationToken ct) => operation(ct);
        public Task<T> RunInstallationAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct) => operation(ct);
    }

    private static void AssertPrintedHashMatches(StringWriter output, SystemSetupState state)
    {
        var lines = output.ToString().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var candidates = lines.Where(line => line.Length == SetupTokenCrypto.RawTokenByteLength * 2
            && line.All(Uri.IsHexDigit)).ToArray();
        Assert.True(candidates.Length == 1);
        Assert.True(SetupTokenCrypto.Matches(candidates[0], state.SetupTokenHash!));
        Assert.True(candidates[0] != state.SetupTokenHash);
    }

    private static async Task WithConsole(StringWriter output, Func<Task> action)
    {
        var original = Console.Out;
        try
        {
            Console.SetOut(output);
            await action();
        }
        finally
        {
            Console.SetOut(original);
        }
    }
}
