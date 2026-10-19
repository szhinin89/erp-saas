using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using ERP.API.Tests.Support;
using ERP.Application.Setup;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ERP.API.Tests.Integration;

[CollectionDefinition("FirstRunHttp", DisableParallelization = true)]
public sealed class FirstRunHttpCollection { }

[Collection("FirstRunHttp")]
[Trait("Category", "PostgreSql")]
public sealed class FirstRunHttpTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine").WithDatabase("first_run_http_isolated")
        .WithUsername("synthetic_http_owner").WithPassword("SyntheticHttpContainer1!").Build();
    private readonly List<FirstRunHttpProcess> _apis = new();
    private readonly List<string> _secrets = new() { "synthetic-invalid-token", "SyntheticHttpInstall1!" };
    private string ConnectionString => new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString())
        { Pooling = false, IncludeErrorDetail = false }.ConnectionString;

    public Task InitializeAsync() => _postgres.StartAsync();
    public async Task DisposeAsync()
    {
        var failures = new List<Exception>();
        foreach (var api in _apis)
            try { await api.DisposeAsync(); }
            catch (Exception exception) { failures.Add(exception); }
        try { await _postgres.DisposeAsync(); }
        catch (Exception exception) { failures.Add(exception); }
        if (failures.Count != 0) throw new AggregateException("Isolated HTTP test cleanup failed.", failures);
    }

    [Fact]
    public async Task Missing_token_uses_the_official_validation_error_contract()
    {
        var api = await StartApi();
        var absent = Payload(await Token(api));
        absent.Remove("setupToken");
        using var response = await api.Client.PostAsJsonAsync("/api/v1/setup/admin", absent);
        using var json = await ReadSafe(response, envelope: false);
        var envelope = json.RootElement.TryGetProperty("code", out _)
            && json.RootElement.TryGetProperty("severity", out _)
            && json.RootElement.TryGetProperty("message", out _)
            && json.RootElement.TryGetProperty("meta", out _);
        Assert.True(response.StatusCode == HttpStatusCode.UnprocessableEntity && envelope,
            $"Missing token returned HTTP {(int)response.StatusCode}, official envelope present={envelope}; expected 422 with the official error envelope.");
        Assert.Equal("VALIDATION_ERROR", json.RootElement.GetProperty("code").GetString());
        Assert.Equal("error", json.RootElement.GetProperty("severity").GetString());
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("message").GetProperty("user").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("meta").GetProperty("timestamp").GetString()));
        Assert.Equal(response.Headers.GetValues("X-Correlation-Id").Single(),
            json.RootElement.GetProperty("meta").GetProperty("correlationId").GetString());
        Assert.NotEmpty(json.RootElement.GetProperty("data").GetProperty("errors")
            .GetProperty("setupToken").EnumerateArray());
    }

    [Fact]
    public async Task Pending_invalid_validation_and_expired_tokens_do_not_install_or_leak()
    {
        var api = await StartApi();
        var token = await Token(api);
        await Status(api, false);
        await Post(api, Payload("synthetic-invalid-token"), HttpStatusCode.Unauthorized);
        var invalidForm = Payload(token);
        invalidForm["username"] = "x";
        await Post(api, invalidForm, HttpStatusCode.UnprocessableEntity); // FluentValidation middleware.
        await Sql("UPDATE system_setup_state SET setup_token_expiry_utc = now() - interval '1 second'");
        await Post(api, Payload(token), HttpStatusCode.BadRequest);
        await Status(api, false);
        Assert.True((await Counts()).All(count => count == 0));
        Assert.False(await CanAcquireLock());
    }

    [Fact]
    public async Task Transactional_failure_concurrent_HTTP_installation_contender_and_restart()
    {
        var api = await StartApi();
        var token = await Token(api);
        await Status(api, false);
        var contender = new FirstRunHttpProcess(ConnectionString);
        _apis.Add(contender);
        Assert.True(await contender.RejectedAsContenderAsync());
        Assert.True(contender.CapturedTokens().Length == 0);
        await Status(api, false);
        Assert.True(SetupTokenCrypto.Matches(token, (string)(await Scalar("SELECT setup_token_hash FROM system_setup_state"))!));

        // Test-only instrumentation: xmin may be a savepoint's subxid, not the root transaction ID.
        await Sql("""
            CREATE TABLE first_run_http_tx_probe (root_xid xid8 NOT NULL, table_name text NOT NULL);
            CREATE FUNCTION first_run_http_record_tx() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                INSERT INTO first_run_http_tx_probe VALUES (pg_current_xact_id(), TG_TABLE_SCHEMA || '.' || TG_TABLE_NAME);
                RETURN NULL;
            END $$;
            """);
        foreach (var table in new[] { "identity_users", "tenants", "company", "access.global_user_roles",
            "company_user_memberships", "company_user_branches", "system_setup_state" })
            await Sql($"CREATE TRIGGER http_gate_tx AFTER INSERT OR UPDATE ON {table} FOR EACH ROW EXECUTE FUNCTION first_run_http_record_tx()");

        // Constraint exists only in this ephemeral DB. Failure occurs at account persistence after provisioning flushes.
        await Sql("ALTER TABLE identity_users ADD CONSTRAINT http_gate_reject CHECK (username <> 'http.rollback')");
        await Post(api, Payload(token, "http.rollback"), HttpStatusCode.Conflict);
        Assert.True((await Counts()).All(count => count == 0));
        Assert.Equal(0L, await Scalar("SELECT count(*) FROM first_run_http_tx_probe"));
        await Status(api, false);
        Assert.False(await CanAcquireLock());

        var first = Post(api, Payload(token), expected: null);
        var second = Post(api, Payload(token), expected: null);
        var outcomes = await Task.WhenAll(first, second);
        Assert.Equal(1, outcomes.Count(code => code == HttpStatusCode.OK));
        Assert.Equal(1, outcomes.Count(code => code == HttpStatusCode.Conflict));
        await Status(api, true);
        Assert.True((await Counts()).All(count => count == 1));
        Assert.Equal(1L, await Scalar("SELECT count(DISTINCT root_xid) FROM first_run_http_tx_probe"));
        Assert.Equal(7L, await Scalar("SELECT count(DISTINCT table_name) FROM first_run_http_tx_probe"));
        Assert.True(await CanAcquireLock());
        Assert.Equal(DBNull.Value, await Scalar("SELECT setup_token_hash FROM system_setup_state"));
        await Post(api, Payload(token), HttpStatusCode.Conflict);

        await api.DisposeAsync();
        _apis.Remove(api);
        var restarted = await StartApi();
        Assert.True(restarted.CapturedTokens().Length == 0);
        await Status(restarted, true);
        await Post(restarted, Payload(token), HttpStatusCode.Conflict);
        Assert.True((await Counts()).All(count => count == 1));
        Assert.Equal(DBNull.Value, await Scalar("SELECT setup_token_hash FROM system_setup_state"));
    }

    private async Task<FirstRunHttpProcess> StartApi()
    {
        var api = new FirstRunHttpProcess(ConnectionString);
        _apis.Add(api);
        await api.WaitReadyAsync();
        return api;
    }

    private async Task<string> Token(FirstRunHttpProcess api)
    {
        var tokens = api.CapturedTokens();
        Assert.True(tokens.Length == 1);
        var hash = (string)(await Scalar("SELECT setup_token_hash FROM system_setup_state"))!;
        Assert.True(SetupTokenCrypto.Matches(tokens[0], hash));
        _secrets.Add(tokens[0]);
        _secrets.Add(hash);
        return tokens[0];
    }

    private static Dictionary<string, object?> Payload(string token, string username = "http.synthetic.owner") => new()
    {
        ["username"] = username, ["firstName"] = "Synthetic", ["lastName"] = "Http",
        ["email"] = "synthetic.http@example.invalid", ["password"] = "SyntheticHttpInstall1!", ["setupToken"] = token,
    };

    private async Task Status(FirstRunHttpProcess api, bool initialized)
    {
        using var response = await api.Client.GetAsync("/api/v1/setup/status");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadSafe(response, true);
        Assert.Equal(initialized, json.RootElement.GetProperty("data").GetProperty("isInitialized").GetBoolean());
    }

    private async Task<HttpStatusCode> Post(FirstRunHttpProcess api, object payload, HttpStatusCode? expected, bool envelope = true)
    {
        using var response = await api.Client.PostAsJsonAsync("/api/v1/setup/admin", payload);
        if (expected is not null) Assert.Equal(expected.Value, response.StatusCode);
        using var json = await ReadSafe(response, envelope);
        return response.StatusCode;
    }

    private async Task<JsonDocument> ReadSafe(HttpResponseMessage response, bool envelope)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.Headers.Contains("X-Correlation-Id"));
        var exposed = body + string.Join("\n", response.Headers.Concat(response.Content.Headers)
            .SelectMany(header => header.Value));
        Assert.False(Regex.IsMatch(exposed, "[A-Fa-f0-9]{64}"));
        Assert.False(Regex.IsMatch(exposed, @"\$2[aby]\$[0-9]{2}\$[./A-Za-z0-9]{53}"));
        foreach (var secret in _secrets) Assert.False(exposed.Contains(secret, StringComparison.Ordinal));
        Assert.False(body.Contains("setupTokenHash", StringComparison.OrdinalIgnoreCase));
        Assert.False(body.Contains("passwordHash", StringComparison.OrdinalIgnoreCase));
        var json = JsonDocument.Parse(body);
        if (envelope)
        {
            Assert.True(json.RootElement.TryGetProperty("code", out _));
            Assert.True(json.RootElement.GetProperty("meta").TryGetProperty("correlationId", out _));
        }
        return json;
    }

    private async Task<long[]> Counts()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            SELECT (SELECT count(*) FROM identity_users), (SELECT count(*) FROM tenants),
                (SELECT count(*) FROM company), (SELECT count(*) FROM access.global_user_roles),
                (SELECT count(*) FROM company_user_memberships), (SELECT count(*) FROM company_user_branches)
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return Enumerable.Range(0, 6).Select(reader.GetInt64).ToArray();
    }

    private async Task<object?> Scalar(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return await command.ExecuteScalarAsync();
    }
    private async Task Sql(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
    private async Task<bool> CanAcquireLock() => Equals(await Scalar("SELECT pg_try_advisory_lock(1514686021, 1)"), true);
}
