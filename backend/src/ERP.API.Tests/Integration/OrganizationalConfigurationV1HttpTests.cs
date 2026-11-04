using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ERP.Domain.Configuration.Constants;
using FluentAssertions;

namespace ERP.API.Tests.Integration;

/// <summary>Configuration-01: API real y PostgreSQL del fixture desechable, sin datos externos.</summary>
public sealed class OrganizationalConfigurationV1HttpTests
    : IClassFixture<CompanySettingsIsolationFixture>
{
    private readonly CompanySettingsIsolationFixture _fixture;

    public OrganizationalConfigurationV1HttpTests(CompanySettingsIsolationFixture fixture) =>
        _fixture = fixture;

    [Fact]
    public async Task Defaults_persisten_historial_es_consultable_y_acceso_es_restringido()
    {
        const string defaults = "/api/v1/org-config/company/invoice-defaults";
        var key = OrgSettingKeys.Invoice.DefaultDocTypeCode;
        var history = $"/api/v1/configuration/change-log?key={Uri.EscapeDataString(key)}";
        using var client = _fixture.CreateClient(_fixture.CompanyAId);
        var payload = new
        {
            defaultDocTypeCode = "01",
            defaultSriPaymentMethodCode = "01",
            defaultPaymentTermId = (Guid?)null,
        };
        var saved = await client.PutAsJsonAsync(defaults, payload);
        saved.StatusCode.Should().Be(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());

        using var reopened = _fixture.CreateClient(_fixture.CompanyAId);
        var read = await reopened.GetAsync(defaults);
        read.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await read.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("data").GetProperty("defaultDocTypeCode")
            .GetString().Should().Be("01");

        var response = await reopened.GetAsync(history);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var log = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = log.RootElement.GetProperty("data");
        data.GetProperty("totalCount").GetInt32().Should().Be(1);
        var entry = data.GetProperty("items")[0];
        entry.GetProperty("newValue").GetString().Should().Be("01");
        entry.GetProperty("changedBy").GetGuid().Should().Be(_fixture.UserId);
        entry.GetProperty("scopeId").GetGuid().Should().Be(_fixture.CompanyAId);

        // Mismo valor no agrega un cambio; un request inválido tampoco altera lo persistido.
        (await reopened.PutAsJsonAsync(defaults, payload)).StatusCode.Should().Be(HttpStatusCode.OK);
        var invalid = await reopened.PutAsJsonAsync(defaults, new { defaultDocTypeCode = new string('x', 100) });
        invalid.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using var afterInvalid = JsonDocument.Parse(await reopened.GetStringAsync(defaults));
        afterInvalid.RootElement.GetProperty("data").GetProperty("defaultDocTypeCode")
            .GetString().Should().Be("01");
        using var unchangedLog = JsonDocument.Parse(await reopened.GetStringAsync(history));
        unchangedLog.RootElement.GetProperty("data").GetProperty("totalCount")
            .GetInt32().Should().Be(1);

        using var foreign = _fixture.CreateClient(_fixture.CompanyBId);
        (await foreign.GetAsync(history)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using var anonymous = _fixture.CreateClient(_fixture.CompanyAId);
        anonymous.DefaultRequestHeaders.Authorization = null;
        (await anonymous.GetAsync(history)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
