using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.Integration;

[Trait("Category", "PostgreSql")]
public sealed class SalesAuthorizationRetryA8HttpTests(SalesReturnFlowFixture fixture)
    : IClassFixture<SalesReturnFlowFixture>
{
    private readonly SalesReturnFlowFixture _f = fixture;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };
    private async Task<Guid> CreateDraftAsync()
    {
        var userId = await _f.CreateUserWithBranchAccessAsync();
        _f.SetActiveContext(userId);
        var cashRegisterId = await _f.CreateCashRegisterAsync();
        var open = await _f.Client.PostAsJsonAsync(
            "/api/v1/cash-sessions/open",
            new { cashRegisterId, openingAmount = 100m }
        );
        open.StatusCode.Should().Be(HttpStatusCode.Created, await open.Content.ReadAsStringAsync());

        const decimal quantity = 2m;
        const decimal unitPrice = 50m;
        var vat = Math.Round(
            quantity * unitPrice * _f.VatPercentage / 100m,
            2,
            MidpointRounding.AwayFromZero
        );
        var create = await _f.Client.PostAsJsonAsync(
            "/api/v1/sales",
            new
            {
                customerId = _f.CustomerId,
                issueDate = DateOnly
                    .FromDateTime(DateTime.UtcNow.AddDays(-1))
                    .ToString("yyyy-MM-dd"),
                paymentTermId = _f.CreditPaymentTermId,
                lines = new[]
                {
                    new
                    {
                        itemId = _f.ItemId,
                        description = "Producto anulación",
                        quantity,
                        unitPrice,
                        vatCode = _f.VatCode,
                        warehouseId = _f.WarehouseId,
                    },
                },
                payments = new[]
                {
                    new
                    {
                        paymentMethodId = _f.CreditPaymentMethodId,
                        amount = quantity * unitPrice + vat,
                    },
                },
            }
        );
        create
            .StatusCode.Should()
            .Be(HttpStatusCode.Created, await create.Content.ReadAsStringAsync());
        var draft = (
            await create.Content.ReadFromJsonAsync<SrEnvelope<SrSalesInvoiceResponseDto>>(
                JsonOptions
            )
        )!.Data!;

        return draft.Id;
    }
    private async Task<JsonElement> AuthorizeAsync(Guid id)
    {
        using var response = await _f.Client.PostAsync($"/api/v1/sales/{id}/authorize", null);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        var root = JsonDocument.Parse(body).RootElement.Clone();
        root.GetProperty("code").GetString().Should().Be("OK");
        var data = root.GetProperty("data");
        data.GetProperty("id").GetGuid().Should().Be(id);
        data.GetProperty("status").GetString().Should().Be("Authorized");
        return data;
    }

    private async Task<string> EffectsAsync(Guid id)
    {
        using var scope = _f.CreateDbScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var invoice = await db.SalesInvoices.SingleAsync(i => i.Id == id);
        var session = await db.CashSessions.SingleAsync(s => s.Id == invoice.CashSessionId);
        var stock = await db.CurrentStocks.SingleAsync(s => s.ProductId == _f.ItemId && s.WarehouseId == _f.WarehouseId);
        return JsonSerializer.Serialize(new
        {
            stock.Quantity, stock.TotalStockValue, session.CurrentBalance,
            Movements = await db.StockMovements.Where(m => m.SourceDocId == id).Select(m => m.Id).OrderBy(x => x).ToArrayAsync(),
            Receivables = await db.SalesReceivables.Where(r => r.InvoiceId == id).Select(r => r.Id).OrderBy(x => x).ToArrayAsync(),
            Cash = await db.CashMovements.Where(m => m.ReferenceId == id).Select(m => m.Id).OrderBy(x => x).ToArrayAsync(),
            Entries = await db.JournalEntries.Where(e => e.SourceEventId == id).Select(e => e.Id).OrderBy(x => x).ToArrayAsync(),
            Outbox = await db.OutboxMessages.Select(m => m.Id).OrderBy(x => x).ToArrayAsync(),
            Electronic = await db.ElectronicDocuments.Where(d => d.SourceEntityId == id).Select(d => d.Id).OrderBy(x => x).ToArrayAsync(),
        });
    }

    [Fact]
    public async Task A8_http_lost_response_retries_return_200_same_document_and_unchanged_effects()
    {
        var id = await CreateDraftAsync();
        // Commit has completed, but this response is discarded by the caller.
        await AuthorizeAsync(id);
        var effects = await EffectsAsync(id);
        var firstRetry = await AuthorizeAsync(id);
        for (var i = 0; i < 5; i++)
            (await AuthorizeAsync(id)).GetRawText().Should().Be(firstRetry.GetRawText());
        (await EffectsAsync(id)).Should().Be(effects);
    }

    [Fact]
    public async Task A8_http_concurrent_authorize_requests_all_return_same_authorized_document()
    {
        var id = await CreateDraftAsync();
        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => AuthorizeAsync(id)));
        results.Select(r => r.GetProperty("invoiceNumber").GetString()).Distinct().Should().ContainSingle();
        var effects = await EffectsAsync(id);
        await AuthorizeAsync(id);
        (await EffectsAsync(id)).Should().Be(effects);
    }

    [Fact]
    public async Task A8_http_retry_with_foreign_branch_remains_forbidden()
    {
        var id = await CreateDraftAsync();
        await AuthorizeAsync(id);
        var effects = await EffectsAsync(id);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/sales/{id}/authorize");
        request.Headers.Add("X-Branch-Id", Guid.NewGuid().ToString());
        using var response = await _f.Client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
        (await EffectsAsync(id)).Should().Be(effects);
    }
}
