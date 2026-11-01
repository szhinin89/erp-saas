using ERP.Application.Common.Interfaces.SRI;
using ERP.Infrastructure.Services.Sri.Health;
using FluentAssertions;

namespace ERP.Infrastructure.Tests.Services.Sri.Health;

/// <summary>
/// Los decoradores Observed* envuelven adaptadores FROZEN: deben devolver exactamente el mismo
/// resultado, pasar los mismos argumentos, propagar excepciones intactas y solo registrar evidencia
/// técnica (sin clave de acceso ni XML).
/// </summary>
public sealed class ObservedSriClientDecoratorTests
{
    private const string Url =
        "https://celcer.sri.gob.ec/comprobantes-electronicos-ws/RecepcionComprobantesOffline?wsdl";

    private sealed record Recorded(
        string? WsdlUrl,
        SriServiceKind Service,
        SriObservationOutcome Outcome,
        TimeSpan? Elapsed
    );

    private sealed class CapturingRecorder : ISriServiceHealthRecorder
    {
        public List<Recorded> Items { get; } = [];

        public void Record(string? wsdlUrl, SriServiceKind service, SriObservationOutcome outcome, TimeSpan? elapsed) =>
            Items.Add(new Recorded(wsdlUrl, service, outcome, elapsed));
    }

    private sealed class StubReception : ISriReceptionClient
    {
        public SriReceptionResult? Result { get; init; }
        public Exception? Throws { get; init; }
        public (byte[] Xml, string Url, CancellationToken Ct)? Received { get; private set; }

        public Task<SriReceptionResult> SendAsync(byte[] signedXmlBytes, string wsdlUrl, CancellationToken ct = default)
        {
            Received = (signedXmlBytes, wsdlUrl, ct);
            return Throws is null ? Task.FromResult(Result!) : Task.FromException<SriReceptionResult>(Throws);
        }
    }

    private sealed class StubAuthorization : ISriAuthorizationClient
    {
        public SriAuthorizationResult? CheckResult { get; init; }
        public SriAuthorizationHistoryResult? HistoryResult { get; init; }
        public string? ReceivedAccessKey { get; private set; }

        public Task<SriAuthorizationResult> CheckAsync(string accessKey, string wsdlUrl, CancellationToken ct = default)
        {
            ReceivedAccessKey = accessKey;
            return Task.FromResult(CheckResult!);
        }

        public Task<SriAuthorizationHistoryResult> QueryDecisionsAsync(string accessKey, string wsdlUrl, CancellationToken ct = default)
        {
            ReceivedAccessKey = accessKey;
            return Task.FromResult(HistoryResult!);
        }
    }

    private sealed class StubChecker(bool reachable) : ISriConnectivityChecker
    {
        public Task<bool> PingAsync(string wsdlUrl, CancellationToken ct = default) => Task.FromResult(reachable);
    }

    private readonly CapturingRecorder _recorder = new();

    [Theory]
    [InlineData("RECIBIDA", SriObservationOutcome.Responded)]
    [InlineData("DEVUELTA", SriObservationOutcome.Responded)]
    [InlineData("ERROR_CONEXION", SriObservationOutcome.TransportFailure)]
    [InlineData("ERROR_SOAP_FAULT", SriObservationOutcome.InvalidResponse)]
    [InlineData("ERROR_RESPUESTA_INVALIDA", SriObservationOutcome.InvalidResponse)]
    [InlineData("ALGO_NUEVO", SriObservationOutcome.Undetermined)]
    public async Task Reception_returns_the_same_result_and_records_its_classification(
        string status,
        SriObservationOutcome expected
    )
    {
        var original = new SriReceptionResult { Status = status, Errors = ["detalle"] };
        var inner = new StubReception { Result = original };
        var xml = new byte[] { 1, 2, 3 };
        using var cts = new CancellationTokenSource();
        var sut = new ObservedSriReceptionClient(inner, _recorder, TimeProvider.System);

        var result = await sut.SendAsync(xml, Url, cts.Token);

        result.Should().BeSameAs(original);
        inner.Received!.Value.Xml.Should().BeSameAs(xml);
        inner.Received!.Value.Url.Should().Be(Url);
        inner.Received!.Value.Ct.Should().Be(cts.Token);
        _recorder.Items.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { WsdlUrl = Url, Service = SriServiceKind.Reception, Outcome = expected });
        _recorder.Items[0].Elapsed.Should().NotBeNull();
    }

    [Fact]
    public async Task Reception_propagates_exceptions_unchanged_and_records_nothing()
    {
        var boom = new OperationCanceledException();
        var sut = new ObservedSriReceptionClient(new StubReception { Throws = boom }, _recorder, TimeProvider.System);

        var act = () => sut.SendAsync([1], Url);

        (await act.Should().ThrowAsync<OperationCanceledException>()).Which.Should().BeSameAs(boom);
        _recorder.Items.Should().BeEmpty();
    }

    [Theory]
    [InlineData("AUTORIZADO", SriObservationOutcome.Responded)]
    [InlineData("NO AUTORIZADO", SriObservationOutcome.Responded)]
    [InlineData("TIMEOUT", SriObservationOutcome.RespondedWithoutDecision)]
    [InlineData("ERROR_CONEXION", SriObservationOutcome.TransportFailure)]
    [InlineData("ERROR_SOAP_FAULT", SriObservationOutcome.InvalidResponse)]
    [InlineData("SIN_RESPUESTA", SriObservationOutcome.Undetermined)]
    public async Task Authorization_check_returns_the_same_result_without_polling_duration(
        string status,
        SriObservationOutcome expected
    )
    {
        var original = new SriAuthorizationResult { Status = status };
        var inner = new StubAuthorization { CheckResult = original };
        var sut = new ObservedSriAuthorizationClient(inner, _recorder, TimeProvider.System);

        var result = await sut.CheckAsync("clave-49", Url);

        result.Should().BeSameAs(original);
        inner.ReceivedAccessKey.Should().Be("clave-49");
        var recorded = _recorder.Items.Should().ContainSingle().Subject;
        recorded.Service.Should().Be(SriServiceKind.Authorization);
        recorded.Outcome.Should().Be(expected);
        recorded.Elapsed.Should().BeNull("la duración incluye la espera inicial y el polling deliberado");
    }

    [Fact]
    public async Task Decisions_query_success_is_a_valid_response_even_without_authorizations()
    {
        var original = new SriAuthorizationHistoryResult(true, [], null);
        var sut = new ObservedSriAuthorizationClient(
            new StubAuthorization { HistoryResult = original }, _recorder, TimeProvider.System);

        var result = await sut.QueryDecisionsAsync("clave-49", Url);

        result.Should().BeSameAs(original);
        _recorder.Items.Should().ContainSingle().Which.Outcome.Should().Be(SriObservationOutcome.Responded);
    }

    [Fact]
    public async Task Decisions_query_failure_is_undetermined_without_duration()
    {
        var original = SriAuthorizationHistoryResult.Failed("No se pudo contactar al servicio de autorización del SRI.");
        var sut = new ObservedSriAuthorizationClient(
            new StubAuthorization { HistoryResult = original }, _recorder, TimeProvider.System);

        var result = await sut.QueryDecisionsAsync("clave-49", Url);

        result.Should().BeSameAs(original);
        var recorded = _recorder.Items.Should().ContainSingle().Subject;
        recorded.Outcome.Should().Be(SriObservationOutcome.Undetermined);
        recorded.Elapsed.Should().BeNull();
    }

    [Theory]
    [InlineData(true, SriObservationOutcome.ProbeReachable)]
    [InlineData(false, SriObservationOutcome.ProbeUnreachable)]
    public async Task Ping_returns_the_same_value_and_records_probe_evidence(
        bool reachable,
        SriObservationOutcome expected
    )
    {
        var sut = new ObservedSriConnectivityChecker(new StubChecker(reachable), _recorder);

        (await sut.PingAsync(Url)).Should().Be(reachable);

        _recorder.Items.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { Service = SriServiceKind.Reception, Outcome = expected, Elapsed = (TimeSpan?)null });
    }
}
