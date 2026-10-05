using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.Integration;

/// <summary>
/// ZH-COLLECTIONS-RECEIVABLE-CONCURRENCY-01 — HTTP real + PostgreSQL real. Cobros DISTINTOS
/// (ClientRequestId distinto) sobre la misma CxC se serializan con SELECT … FOR UPDATE: Σ aplicaciones
/// confirmadas ≤ monto original y el saldo persistido coincide exactamente con esas aplicaciones.
/// El perdedor reevalúa contra el saldo ya consumido y se rechaza con DOMAIN_RULE_VIOLATION sin ningún
/// efecto (ni cobro, ni asiento, ni outbox). Todo se verifica desde un DbContext independiente.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class ReceivableCollectionConcurrencyTests
    : IClassFixture<FinancialCommandIdempotencyFixture>
{
    private const string CollectionsUrl = "/api/v1/finance/collections";
    private const string DomainRuleViolation = "DOMAIN_RULE_VIOLATION";

    private readonly FinancialCommandIdempotencyFixture _f;

    public ReceivableCollectionConcurrencyTests(FinancialCommandIdempotencyFixture f) => _f = f;

    // ── helpers ──────────────────────────────────────────────────────────

    private sealed record Response(HttpStatusCode Status, string Code, Guid? Id);

    /// <summary>Estado persistido de la CxC + Σ de aplicaciones de cobros confirmados sobre ella.</summary>
    private sealed record ReceivableState(
        decimal Paid,
        decimal Balance,
        string Status,
        decimal AppliedByCollections,
        int Collections
    );

    private static async Task<Response> PostAsync(HttpClient client, object body)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var http = await client.PostAsJsonAsync(CollectionsUrl, body, timeout.Token);
        var json = JsonDocument
            .Parse(await http.Content.ReadAsStringAsync(timeout.Token))
            .RootElement;
        var code = json.TryGetProperty("code", out var c) ? c.GetString() ?? "" : "";
        Guid? id =
            json.TryGetProperty("data", out var d)
            && d.ValueKind == JsonValueKind.Object
            && d.TryGetProperty("id", out var i)
                ? i.GetGuid()
                : null;
        return new Response(http.StatusCode, code, id);
    }

    private static Task<Response[]> PostConcurrentlyAsync(
        HttpClient client,
        params object[] bodies
    ) => Task.WhenAll(bodies.Select(b => PostAsync(client, b)));

    private object Collection(
        Guid receivableId,
        decimal amount,
        Guid? clientRequestId = null,
        Guid? cashRegisterId = null
    ) =>
        new
        {
            customerId = _f.CustomerId,
            amount,
            paymentDate = _f.Today,
            paymentMethodId = _f.CashMethodId,
            reference = (string?)null,
            lines = new[]
            {
                new
                {
                    documentId = receivableId,
                    installmentId = (Guid?)null,
                    appliedAmount = amount,
                },
            },
            cashRegisterId,
            clientRequestId = clientRequestId ?? Guid.NewGuid(),
        };

    private async Task<ReceivableState> StateAsync(Guid receivableId)
    {
        using var scope = _f.CreateDbScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var receivable = await db
            .SalesReceivables.IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(r => r.Id == receivableId);
        var lines = await db
            .Payments.IgnoreQueryFilters()
            .Where(p =>
                p.Status == PaymentStatus.Applied && p.Direction == PaymentDirection.Collection
            )
            .SelectMany(p => p.Lines)
            .Where(l => l.ReceivableId == receivableId)
            .Select(l => new { l.PaymentId, l.AppliedAmount })
            .ToListAsync();
        return new ReceivableState(
            receivable.PaidAmount,
            receivable.BalanceDue,
            receivable.Status,
            lines.Sum(l => l.AppliedAmount),
            lines.Select(l => l.PaymentId).Distinct().Count()
        );
    }

    private static void AssertConsistent(ReceivableState state, decimal original)
    {
        state
            .AppliedByCollections.Should()
            .BeLessThanOrEqualTo(original, "Σ aplicaciones nunca supera el monto original");
        state
            .Paid.Should()
            .Be(
                state.AppliedByCollections,
                "el saldo persistido coincide con las aplicaciones confirmadas"
            );
        state.Balance.Should().Be(original - state.AppliedByCollections);
    }

    // ── cobros distintos concurrentes ────────────────────────────────────

    [Fact]
    public async Task Dos_cobros_distintos_que_caben_60_mas_40_se_aplican_ambos_y_el_saldo_queda_en_cero()
    {
        var op = await _f.CreateOperatorAsync();
        var receivable = await _f.CreateReceivableAsync(100m);
        var client = _f.CreateClient(op.UserId);
        var before = await _f.CountEffectsAsync(receivableId: receivable);

        var responses = await PostConcurrentlyAsync(
            client,
            Collection(receivable, 60m),
            Collection(receivable, 40m)
        );

        responses.Should().OnlyContain(r => r.Status == HttpStatusCode.Created);
        responses.Select(r => r.Id).Distinct().Should().HaveCount(2);
        var state = await StateAsync(receivable);
        AssertConsistent(state, 100m);
        state.Paid.Should().Be(100m);
        state.Balance.Should().Be(0m);
        state.Collections.Should().Be(2);
        var after = await _f.CountEffectsAsync(receivableId: receivable);
        after.JournalEntries.Should().Be(before.JournalEntries + 2);
        after.OutboxMessages.Should().Be(before.OutboxMessages + 2);
    }

    [Fact]
    public async Task Dos_cobros_distintos_que_no_caben_80_mas_80_aplica_uno_y_el_otro_se_rechaza_sin_efectos()
    {
        var op = await _f.CreateOperatorAsync();
        var receivable = await _f.CreateReceivableAsync(100m);
        var client = _f.CreateClient(op.UserId);
        var before = await _f.CountEffectsAsync(receivableId: receivable);

        var responses = await PostConcurrentlyAsync(
            client,
            Collection(receivable, 80m),
            Collection(receivable, 80m)
        );

        responses.Count(r => r.Status == HttpStatusCode.Created).Should().Be(1);
        var loser = responses.Single(r => r.Status != HttpStatusCode.Created);
        loser.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        loser.Code.Should().Be(DomainRuleViolation);

        var state = await StateAsync(receivable);
        AssertConsistent(state, 100m);
        state.Paid.Should().Be(80m);
        state.Balance.Should().Be(20m, "nunca 160 cobrados sobre 100");
        state.Collections.Should().Be(1);
        state.Status.Should().Be("pending");
        var after = await _f.CountEffectsAsync(receivableId: receivable);
        after.JournalEntries.Should().Be(before.JournalEntries + 1);
        after.OutboxMessages.Should().Be(before.OutboxMessages + 1);
    }

    [Fact]
    public async Task Siete_cobros_concurrentes_de_20_sobre_100_aplican_exactamente_cinco()
    {
        var op = await _f.CreateOperatorAsync();
        var receivable = await _f.CreateReceivableAsync(100m);
        var client = _f.CreateClient(op.UserId);
        var before = await _f.CountEffectsAsync(receivableId: receivable);

        var responses = await PostConcurrentlyAsync(
            client,
            Enumerable.Range(0, 7).Select(_ => Collection(receivable, 20m)).ToArray()
        );

        responses.Count(r => r.Status == HttpStatusCode.Created).Should().Be(5);
        responses
            .Where(r => r.Status != HttpStatusCode.Created)
            .Should()
            .HaveCount(2)
            .And.OnlyContain(r =>
                r.Status == HttpStatusCode.UnprocessableEntity && r.Code == DomainRuleViolation
            );
        var state = await StateAsync(receivable);
        AssertConsistent(state, 100m);
        state.Paid.Should().Be(100m);
        state.Collections.Should().Be(5);
        var after = await _f.CountEffectsAsync(receivableId: receivable);
        after.JournalEntries.Should().Be(before.JournalEntries + 5);
        after.OutboxMessages.Should().Be(before.OutboxMessages + 5);
    }

    // ── interacción con ClientRequestId ─────────────────────────────────

    [Fact]
    public async Task Misma_intencion_concurrente_produce_un_solo_cobro()
    {
        var op = await _f.CreateOperatorAsync();
        var receivable = await _f.CreateReceivableAsync(100m);
        var client = _f.CreateClient(op.UserId);
        var body = Collection(receivable, 60m, Guid.NewGuid());

        var responses = await PostConcurrentlyAsync(client, Enumerable.Repeat(body, 5).ToArray());

        responses.Should().OnlyContain(r => r.Status == HttpStatusCode.Created);
        responses.Select(r => r.Id).Distinct().Should().ContainSingle();
        var state = await StateAsync(receivable);
        AssertConsistent(state, 100m);
        state.Paid.Should().Be(60m);
        state.Collections.Should().Be(1);
    }

    [Fact]
    public async Task El_perdedor_reintentado_con_su_misma_clave_se_reevalua_y_la_clave_no_queda_consumida()
    {
        var op = await _f.CreateOperatorAsync();
        var receivable = await _f.CreateReceivableAsync(100m);
        var client = _f.CreateClient(op.UserId);
        var keyA = Guid.NewGuid();
        var keyB = Guid.NewGuid();

        var responses = await PostConcurrentlyAsync(
            client,
            Collection(receivable, 80m, keyA),
            Collection(receivable, 80m, keyB)
        );
        var winnerIndex = Array.FindIndex(responses, r => r.Status == HttpStatusCode.Created);
        winnerIndex.Should().BeGreaterThanOrEqualTo(0);
        var (winnerKey, loserKey) = winnerIndex == 0 ? (keyA, keyB) : (keyB, keyA);
        var before = await _f.CountEffectsAsync(receivableId: receivable);

        // Mismo request del perdedor: no hay documento con su clave → se reevalúa (sigue sin caber).
        var retry = await PostAsync(client, Collection(receivable, 80m, loserKey));
        retry.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        retry.Code.Should().Be(DomainRuleViolation);
        (await _f.CountEffectsAsync(receivableId: receivable))
            .Should()
            .Be(before, "el reintento rechazado no deja efectos");

        // El ganador reintentado responde su mismo cobro (replay) sin efectos nuevos.
        var replay = await PostAsync(client, Collection(receivable, 80m, winnerKey));
        replay.Status.Should().Be(HttpStatusCode.Created);
        replay.Id.Should().Be(responses[winnerIndex].Id);
        (await _f.CountEffectsAsync(receivableId: receivable)).Should().Be(before);

        // La clave del perdedor nunca quedó consumida: una intención que sí cabe se registra con ella.
        var fitting = await PostAsync(client, Collection(receivable, 20m, loserKey));
        fitting.Status.Should().Be(HttpStatusCode.Created);
        var state = await StateAsync(receivable);
        AssertConsistent(state, 100m);
        state.Paid.Should().Be(100m);
        state.Collections.Should().Be(2);
    }

    // ── rollback, destino caja y alcance ────────────────────────────────

    [Fact]
    public async Task Un_cobro_rechazado_bajo_lock_no_deja_la_CxC_bloqueada()
    {
        var op = await _f.CreateOperatorAsync();
        var receivable = await _f.CreateReceivableAsync(100m);
        var client = _f.CreateClient(op.UserId);

        // 150 no cabe nunca: se rechaza DESPUÉS de tomar el lock (rollback) mientras 30 compite.
        var responses = await PostConcurrentlyAsync(
            client,
            Collection(receivable, 150m),
            Collection(receivable, 30m)
        );
        responses
            .Select(r => r.Status)
            .Should()
            .BeEquivalentTo(new[] { HttpStatusCode.Created, HttpStatusCode.UnprocessableEntity });

        // Un cobro posterior no espera ningún lock residual (PostAsync falla por timeout si lo hiciera).
        var next = await PostAsync(client, Collection(receivable, 70m));
        next.Status.Should().Be(HttpStatusCode.Created);
        var state = await StateAsync(receivable);
        AssertConsistent(state, 100m);
        state.Paid.Should().Be(100m);
        state.Collections.Should().Be(2);
    }

    [Fact]
    public async Task Cobros_concurrentes_con_destino_caja_respetan_el_saldo_y_no_mueven_efectivo_de_la_sesion()
    {
        var op = await _f.CreateOperatorAsync();
        var receivable = await _f.CreateReceivableAsync(100m);
        var client = _f.CreateClient(op.UserId);
        var before = await _f.CountEffectsAsync(
            receivableId: receivable,
            sessionId: op.CashSessionId
        );

        var responses = await PostConcurrentlyAsync(
            client,
            Collection(receivable, 60m, cashRegisterId: op.CashRegisterId),
            Collection(receivable, 60m, cashRegisterId: op.CashRegisterId)
        );

        responses.Count(r => r.Status == HttpStatusCode.Created).Should().Be(1);
        responses
            .Single(r => r.Status != HttpStatusCode.Created)
            .Code.Should()
            .Be(DomainRuleViolation);
        var state = await StateAsync(receivable);
        AssertConsistent(state, 100m);
        state.Paid.Should().Be(60m);
        var after = await _f.CountEffectsAsync(
            receivableId: receivable,
            sessionId: op.CashSessionId
        );
        after.CashMovements.Should().Be(before.CashMovements, "el cobro no crea CashMovement");
        after.JournalEntries.Should().Be(before.JournalEntries + 1);
    }

    [Fact]
    public async Task CxC_de_otra_empresa_no_se_bloquea_ni_se_cobra_fail_closed()
    {
        var op = await _f.CreateOperatorAsync();
        var foreign = await _f.CreateReceivableAsync(100m, foreignCompany: true);
        var client = _f.CreateClient(op.UserId);
        var before = await _f.CountEffectsAsync(receivableId: foreign);

        var responses = await PostConcurrentlyAsync(
            client,
            Collection(foreign, 60m),
            Collection(foreign, 40m)
        );

        responses.Should().OnlyContain(r => r.Status == HttpStatusCode.NotFound);
        (await _f.CountEffectsAsync(receivableId: foreign)).Should().Be(before);
        var state = await StateAsync(foreign);
        state.Paid.Should().Be(0m);
        state.Collections.Should().Be(0);
    }
}
