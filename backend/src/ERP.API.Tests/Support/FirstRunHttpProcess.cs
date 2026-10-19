using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using ERP.API.Hangfire;

namespace ERP.API.Tests.Support;

/// <summary>Real API executable, empty content root and allowlisted environment; no host config/secrets.</summary>
internal sealed class FirstRunHttpProcess : IAsyncDisposable
{
    private readonly Process _process;
    private readonly string _directory;
    private readonly StringBuilder _stdout = new();
    private readonly StringBuilder _stderr = new();
    private bool _disposed;
    public HttpClient Client { get; }

    public FirstRunHttpProcess(string connectionString)
    {
        _directory = Directory.CreateTempSubdirectory("erp-first-run-http-").FullName;
        using var reservation = new TcpListener(IPAddress.Loopback, 0);
        reservation.Start();
        var address = $"http://127.0.0.1:{((IPEndPoint)reservation.LocalEndpoint).Port}";
        reservation.Stop();
        var runtime = new DirectoryInfo(RuntimeEnvironment.GetRuntimeDirectory());
        var dotnetRoot = runtime.Parent!.Parent!.Parent!.FullName;
        var executable = Path.Combine(dotnetRoot, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
        var assembly = typeof(Program).Assembly.Location;
        if (!File.Exists(executable) || !File.Exists(Path.ChangeExtension(assembly, ".runtimeconfig.json")))
            throw new InvalidOperationException("API runtime artifacts are missing; build the focal test project first.");
        var start = new ProcessStartInfo(executable)
        {
            WorkingDirectory = _directory, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
        };
        start.Environment.Clear();
        foreach (var name in new[] { "SystemRoot", "WINDIR" })
            if (Environment.GetEnvironmentVariable(name) is { } value) start.Environment[name] = value;
        start.Environment["PATH"] = dotnetRoot + Path.PathSeparator + Environment.GetFolderPath(Environment.SpecialFolder.System);
        start.Environment["DOTNET_ROOT"] = dotnetRoot;
        start.Environment["DOTNET_ENVIRONMENT"] = "Testing";
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Testing";
        start.Environment["DOTNET_NOLOGO"] = "1";
        foreach (var name in new[] { "TEMP", "TMP", "HOME", "USERPROFILE", "APPDATA", "LOCALAPPDATA", "DOTNET_CLI_HOME" })
            start.Environment[name] = _directory;
        var settings = new Dictionary<string, string>
        {
            ["environment"] = "Testing", ["contentRoot"] = _directory, ["urls"] = address,
            ["ConnectionStrings:DefaultConnection"] = connectionString,
            ["Jwt:SecretKey"] = "SyntheticFirstRunHttpKey-NotARealCredential-OnlyTests!",
            ["Jwt:Issuer"] = "SyntheticHttp", ["Jwt:Audience"] = "SyntheticHttp",
            ["Hangfire:Server:Enabled"] = "false", ["Hangfire:StorageConnectionString"] = connectionString,
            ["Hangfire:Workers:isolated"] = "1",
            ["Redis:ConnectionString"] = "", ["ConnectionStrings:Redis"] = "",
            ["HealthChecks:EnableRedis"] = "false", ["HealthChecks:SriProbeUrl"] = "",
            ["Observability:EnablePrometheus"] = "false", ["InstallData:Enabled"] = "false",
            ["E2E:SeedEnabled"] = "false", ["FileStorage:BasePath"] = Path.Combine(_directory, "files"),
            ["Logging:LogLevel:Default"] = "None", ["Serilog:MinimumLevel:Default"] = "Fatal",
            ["Snowflake:WorkerId"] = "23",
        };
        foreach (var id in HangfireRecurringJobs.All.Keys)
        {
            settings[$"Hangfire:Jobs:{id}:Enabled"] = "false";
            settings[$"Hangfire:Jobs:{id}:Queue"] = "isolated";
            settings[$"Hangfire:Jobs:{id}:Cron"] = "* * * * *";
            settings[$"Hangfire:Jobs:{id}:BatchSize"] = "3";
        }
        start.ArgumentList.Add(assembly);
        foreach (var (key, value) in settings)
        {
            start.ArgumentList.Add("--" + key);
            start.ArgumentList.Add(value);
        }
        _process = new Process { StartInfo = start };
        _process.OutputDataReceived += (_, args) => { if (args.Data is not null) lock (_stdout) _stdout.AppendLine(args.Data); };
        _process.ErrorDataReceived += (_, args) => { if (args.Data is not null) lock (_stderr) _stderr.AppendLine(args.Data); };
        if (!_process.Start()) throw new InvalidOperationException("Isolated API process did not start.");
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
        Client = new HttpClient(new HttpClientHandler { UseProxy = false }) { BaseAddress = new Uri(address), Timeout = TimeSpan.FromSeconds(60) };
    }

    public async Task WaitReadyAsync()
    {
        for (var attempt = 0; attempt < 240; attempt++)
        {
            if (_process.HasExited) throw new InvalidOperationException("Isolated API exited before ready. " + SafeDiagnostic());
            try
            {
                using var response = await Client.GetAsync("/health/live");
                if (response.IsSuccessStatusCode) return;
            }
            catch (HttpRequestException) { }
            await Task.Delay(250);
        }
        throw new TimeoutException("Isolated API was not ready. " + SafeDiagnostic());
    }

    public string[] CapturedTokens()
    {
        lock (_stdout) return _stdout.ToString().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(line => Regex.IsMatch(line, "^[A-Fa-f0-9]{64}$")).ToArray();
    }

    public async Task<bool> RejectedAsContenderAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await _process.WaitForExitAsync(timeout.Token);
        _process.WaitForExit(); // Flush asynchronous pipe readers.
        lock (_stderr) return _process.ExitCode != 0 && _stderr.ToString().Contains("Another API owns pending first-run", StringComparison.Ordinal);
    }

    private string SafeDiagnostic()
    {
        string text;
        lock (_stderr) text = _stderr.ToString();
        // Never echo stdout (installation banner). Redact token-shaped material in exceptions too.
        return Regex.Replace(text, "[A-Fa-f0-9]{64}", "[REDACTED]");
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        Client.Dispose();
        if (!_process.HasExited) _process.Kill(entireProcessTree: true);
        await _process.WaitForExitAsync();
        _process.WaitForExit();
        _process.Dispose();
        lock (_stdout) _stdout.Clear();
        lock (_stderr) _stderr.Clear();
        var full = Path.GetFullPath(_directory);
        var temporaryRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase)
            || !Path.GetFileName(full).StartsWith("erp-first-run-http-", StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing cleanup outside the isolated test directory.");
        Directory.Delete(full, recursive: true);
    }
}
