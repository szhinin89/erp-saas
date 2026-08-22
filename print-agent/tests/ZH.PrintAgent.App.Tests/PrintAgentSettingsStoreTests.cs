namespace ZH.PrintAgent.App.Tests;

public sealed class PrintAgentSettingsStoreTests
{
    [Fact]
    public async Task EnsureCreatedAsync_seeds_setup_not_completed_when_inherited_key_is_sentinel()
    {
        var directory = CreateTempDirectory();
        var settingsPath = Path.Combine(directory, "settings.json");
        var inherited = new PrintAgentOptions { ApiKey = PrintAgentStartupValidator.SampleProductionApiKey };

        await PrintAgentSettingsStore.EnsureCreatedAsync(settingsPath, inherited);
        var loaded = await PrintAgentSettingsStore.LoadAsync(settingsPath);

        Assert.True(File.Exists(settingsPath));
        Assert.NotNull(loaded);
        Assert.False(loaded.SetupCompleted);
    }

    [Fact]
    public async Task EnsureCreatedAsync_seeds_setup_completed_when_inherited_key_is_real()
    {
        var directory = CreateTempDirectory();
        var settingsPath = Path.Combine(directory, "settings.json");
        var inherited = new PrintAgentOptions { ApiKey = "already-configured-secret" };

        await PrintAgentSettingsStore.EnsureCreatedAsync(settingsPath, inherited);
        var loaded = await PrintAgentSettingsStore.LoadAsync(settingsPath);

        Assert.NotNull(loaded);
        Assert.True(loaded.SetupCompleted);
    }

    [Fact]
    public async Task EnsureCreatedAsync_does_not_overwrite_an_existing_file()
    {
        var directory = CreateTempDirectory();
        var settingsPath = Path.Combine(directory, "settings.json");
        await PrintAgentSettingsStore.SaveAsync(settingsPath, new PrintAgentOptions { ApiKey = "first-key" });

        await PrintAgentSettingsStore.EnsureCreatedAsync(settingsPath, new PrintAgentOptions { ApiKey = "second-key" });
        var loaded = await PrintAgentSettingsStore.LoadAsync(settingsPath);

        Assert.Equal("first-key", loaded!.ApiKey);
    }

    [Fact]
    public async Task SaveAsync_keeps_a_backup_of_the_previous_version()
    {
        var directory = CreateTempDirectory();
        var settingsPath = Path.Combine(directory, "settings.json");

        await PrintAgentSettingsStore.SaveAsync(settingsPath, new PrintAgentOptions { ApiKey = "v1" });
        await PrintAgentSettingsStore.SaveAsync(settingsPath, new PrintAgentOptions { ApiKey = "v2" });

        Assert.True(File.Exists(settingsPath + ".bak"));
        var backup = await PrintAgentSettingsStore.LoadAsync(settingsPath + ".bak");
        Assert.Equal("v1", backup!.ApiKey);
    }

    [Fact]
    public void GenerateApiKey_produces_unique_url_safe_keys()
    {
        var first = PrintAgentSettingsStore.GenerateApiKey();
        var second = PrintAgentSettingsStore.GenerateApiKey();

        Assert.NotEqual(first, second);
        Assert.True(first.Length >= 32);
        Assert.DoesNotContain('+', first);
        Assert.DoesNotContain('/', first);
        Assert.DoesNotContain('=', first);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "zh-print-agent-settings-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
