using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace ERP.API.Tests.Integration;

/// <summary>
/// ZH-COLLECTIONS-CUSTOMER-SCOPE-01 — HTTP real + PostgreSQL real. Un cobro del cliente A nunca se
/// aplica a una CxC del cliente B, y una cuota informada debe ser de esa CxC. Cualquier línea
/// inválida rechaza el cobro completo: saldos intactos, sin Payment, sin Outbox, sin asiento.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class ReceivableCollectionCustomerScopeTests
    : IClassFixture<FinancialCommandIdempotencyFixture>
{
    private const string CollectionsUrl = "/api/v1/finance/collections";
    private readonly FinancialCommandIdempotencyFixture _f;

    public ReceivableCollectionCustomerScopeTests(FinancialCommandIdempotencyFixture f) => _f = f;

    private object Collection(Guid customerId, params (Guid Receivable, Guid? Installment, decimal Amount)[] lines) =>
        new
        {
            customerId,
            amount = lines.Sum(l => l.Amount),
            paymentDate = _f.Today,
            paymentMethodId = _f.CashMethodId,
            reference = (string?)null,
            lines = lines.Select(l => new
            {
                documentId = l.Receivable,
                installmentId = l.Installment,
                appliedAmount = l.Amount,
            }),
            clientRequestId = Guid.NewGuid(),
        };

    private static async Task<(HttpStatusCode Status, string Code, string Body)> PostAsync(HttpClient client, object body)
    {
        var http = await client.PostAsJsonAsync(CollectionsUrl, body);
        var text = await http.Content.ReadAsStringAsync();
        var code = JsonDocument.Parse(text).RootElement.TryGetProperty("code", out var c) ? c.GetString() ?? "" : "";
        return (http.StatusCode, code, text);
    }

    /// <summary>Saldos de las CxC + cobros, asientos y outbox del tenant: nada debe moverse.</summary>
    private async Task<object> SnapshotAsync(params Guid[] receivables)
    {
        var paid = new List<decimal>();
        foreach (var r in receivables)
            paid.Add((await _f.CountEffectsAsync(receivableId: r)).ReceivablePaid);
        var effects = await _f.CountEffectsAsync();
        return new
        {
            Paid = string.Join("|", paid),
            Collections = await _f.CountCollectionsAsync(),
            effects.JournalEntries,
            effects.OutboxMessages,
        };
    }

    [Fact]
    public async Task Cliente_A_cobrando_la_CxC_del_cliente_B_se_rechaza_sin_efectos()
    {
        var op = await _f.CreateOperatorAsync();
        var customerB = await _f.CreateCustomerAsync();
        var receivableOfB = await _f.CreateReceivableAsync(100m, customerId: customerB);
        var client = _f.CreateClient(op.UserId);
        var before = await SnapshotAsync(receivableOfB);

        var response = await PostAsync(client, Collection(_f.CustomerId, (receivableOfB, null, 40m)));

        response.Status.Should().Be(HttpStatusCode.UnprocessableEntity, response.Body);
        response.Code.Should().Be("VALIDATION_ERROR");
        response.Body.Should().Contain("no pertenece al cliente del cobro");
        (await SnapshotAsync(receivableOfB)).Should().BeEquivalentTo(before);
    }

    [Fact]
    public async Task Varias_lineas_con_una_CxC_de_otro_cliente_hacen_rollback_total()
    {
        var op = await _f.CreateOperatorAsync();
        var customerB = await _f.CreateCustomerAsync();
        var ownFirst = await _f.CreateReceivableAsync(100m);
        var foreign = await _f.CreateReceivableAsync(100m, customerId: customerB);
        var ownLast = await _f.CreateReceivableAsync(100m);
        var client = _f.CreateClient(op.UserId);
        var before = await SnapshotAsync(ownFirst, foreign, ownLast);

        var response = await PostAsync(client,
            Collection(_f.CustomerId, (ownFirst, null, 30m), (foreign, null, 30m), (ownLast, null, 30m)));

        response.Status.Should().Be(HttpStatusCode.UnprocessableEntity, response.Body);
        (await SnapshotAsync(ownFirst, foreign, ownLast)).Should().BeEquivalentTo(before,
            "ninguna CxC del cobro se modifica, ni siquiera las propias");
    }

    [Fact]
    public async Task Cuota_de_otra_CxC_se_rechaza_sin_efectos()
    {
        var op = await _f.CreateOperatorAsync();
        var target = await _f.CreateReceivableAsync(100m, installments: 2);
        var other = await _f.CreateReceivableAsync(100m, installments: 2);
        var otherInstallment = (await _f.InstallmentIdsAsync(other))[0];
        var client = _f.CreateClient(op.UserId);
        var before = await SnapshotAsync(target, other);

        var response = await PostAsync(client, Collection(_f.CustomerId, (target, otherInstallment, 50m)));

        response.Status.Should().Be(HttpStatusCode.UnprocessableEntity, response.Body);
        response.Body.Should().Contain("no pertenece a la cuenta por cobrar");
        (await SnapshotAsync(target, other)).Should().BeEquivalentTo(before);
    }

    [Fact]
    public async Task Camino_feliz_con_cuota_propia_sigue_aplicando_el_cobro()
    {
        var op = await _f.CreateOperatorAsync();
        var target = await _f.CreateReceivableAsync(100m, installments: 2);
        var ownInstallment = (await _f.InstallmentIdsAsync(target))[1];
        var client = _f.CreateClient(op.UserId);
        var collectionsBefore = await _f.CountCollectionsAsync();

        var response = await PostAsync(client, Collection(_f.CustomerId, (target, ownInstallment, 50m)));

        response.Status.Should().Be(HttpStatusCode.Created, response.Body);
        (await _f.CountEffectsAsync(receivableId: target)).ReceivablePaid.Should().Be(50m);
        (await _f.CountCollectionsAsync()).Should().Be(collectionsBefore + 1);
    }
}
