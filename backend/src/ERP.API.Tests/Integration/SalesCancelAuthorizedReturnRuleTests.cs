using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ERP.API.Tests.Support;
using ERP.Domain.Modules.Caja.Enums;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit.Abstractions;

namespace ERP.API.Tests.Integration;

/// <summary>
/// ZH-SALES-CANCEL-AUTHORIZED-RETURN-RULE-01 — una factura de venta con una devolución
/// <c>Authorized</c> (parcial o total, reembolso en efectivo o crédito a CxC) no puede anularse:
/// la devolución ya reingresó Kardex, reembolsó, contabilizó y emitió su Nota de Crédito, y la
/// anulación revierte la factura completa. Mismo criterio que Compras (PI-CANC-01); en Ventas una
/// devolución autorizada es terminal. Una devolución Draft no bloquea (no tiene efectos) y queda
/// inautorizable. HTTP real + PostgreSQL real; estado final desde otro DbContext.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class SalesCancelAuthorizedReturnRuleTests : IClassFixture<SalesReturnFlowFixture>
{
    private const string DomainRuleViolation = "DOMAIN_RULE_VIOLATION";
    private const decimal SoldQuantity = 2m;

    private readonly SalesReturnFlowFixture _f;
    private readonly ITestOutputHelper _out;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public SalesCancelAuthorizedReturnRuleTests(SalesReturnFlowFixture f, ITestOutputHelper o)
    {
        _f = f;
        _out = o;
    }

    // ── helpers ──────────────────────────────────────────────────────────

    private sealed record Response(HttpStatusCode Status, string Code);

    private sealed record Sale(Guid InvoiceId, Guid LineId, bool Credit);

    private sealed record Return(Guid Id, decimal GrandTotal);

    /// <summary>Kardex, reembolso, CxC, contabilidad, Nota de Crédito y outbox de la venta y sus devoluciones.</summary>
    private sealed record State(
        SalesInvoiceStatus InvoiceStatus,
        int AuthorizedReturns,
        int DraftReturns,
        decimal SoldOut,
        decimal ReturnedIn,
        decimal CancelledIn,
        int CashRefunds,
        decimal? ReceivableOriginal,
        string? ReceivableStatus,
        int InvoiceEntries,
        int InvoiceEntriesReversed,
        int ReturnEntries,
        int CreditNotes,
        int OutboxMessages
    );

    private static async Task<Response> ReadAsync(HttpResponseMessage http)
    {
        var text = await http.Content.ReadAsStringAsync();
        var json = string.IsNullOrWhiteSpace(text) ? default : JsonDocument.Parse(text).RootElement;
        var code =
            json.ValueKind == JsonValueKind.Object && json.TryGetProperty("code", out var c)
                ? c.GetString() ?? ""
                : "";
        return new Response(http.StatusCode, code);
    }

    private async Task<Sale> CreateSaleAsync(bool credit = false)
    {
        var userId = await _f.CreateUserWithBranchAccessAsync();
        _f.SetActiveContext(userId);
        var cashRegisterId = await _f.CreateCashRegisterAsync();
        var open = await _f.Client.PostAsJsonAsync(
            "/api/v1/cash-sessions/open",
            new { cashRegisterId, openingAmount = 500m }
        );
        open.StatusCode.Should().Be(HttpStatusCode.Created, await open.Content.ReadAsStringAsync());

        const decimal unitPrice = 50m;
        var vat = Math.Round(
            SoldQuantity * unitPrice * _f.VatPercentage / 100m,
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
                paymentTermId = credit ? _f.CreditPaymentTermId : _f.CashPaymentTermId,
                lines = new[]
                {
                    new
                    {
                        itemId = _f.ItemId,
                        description = "Producto anulación",
                        quantity = SoldQuantity,
                        unitPrice,
                        vatCode = _f.VatCode,
                        warehouseId = _f.WarehouseId,
                    },
                },
                payments = new[]
                {
                    new
                    {
                        paymentMethodId = credit ? _f.CreditPaymentMethodId : _f.PaymentMethodId,
                        amount = SoldQuantity * unitPrice + vat,
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
        var authorize = await _f.Client.PostAsync($"/api/v1/sales/{draft.Id}/authorize", null);
        authorize
            .StatusCode.Should()
            .Be(HttpStatusCode.OK, await authorize.Content.ReadAsStringAsync());
        var invoice = (
            await authorize.Content.ReadFromJsonAsync<SrEnvelope<SrSalesInvoiceResponseDto>>(
                JsonOptions
            )
        )!.Data!;
        return new Sale(invoice.Id, invoice.Lines.Single().Id, credit);
    }

    private async Task<Return> CreateReturnDraftAsync(Sale sale, decimal quantity)
    {
        var response = await _f.Client.PostAsJsonAsync(
            "/api/v1/sales/returns",
            new
            {
                salesInvoiceId = sale.InvoiceId,
                reason = "Devolución",
                lines = new[] { new { invoiceDetailId = sale.LineId, quantity } },
            }
        );
        response
            .StatusCode.Should()
            .Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var draft = (
            await response.Content.ReadFromJsonAsync<SrEnvelope<SalesReturnResponseDto>>(
                JsonOptions
            )
        )!.Data!;
        return new Return(draft.Id, draft.GrandTotal);
    }

    private async Task<Response> AuthorizeReturnAsync(Sale sale, Return salesReturn) =>
        await ReadAsync(
            await _f.Client.PostAsJsonAsync(
                $"/api/v1/sales/returns/{salesReturn.Id}/authorize",
                new
                {
                    refundAllocations = new[]
                    {
                        new
                        {
                            method = sale.Credit ? "ReceivableCredit" : "Cash",
                            amount = salesReturn.GrandTotal,
                        },
                    },
                }
            )
        );

    private async Task<Return> CreateAuthorizedReturnAsync(Sale sale, decimal quantity)
    {
        var salesReturn = await CreateReturnDraftAsync(sale, quantity);
        (await AuthorizeReturnAsync(sale, salesReturn)).Status.Should().Be(HttpStatusCode.OK);
        return salesReturn;
    }

    private async Task<Response> CancelAsync(Sale sale) =>
        await ReadAsync(
            await _f.Client.PostAsJsonAsync(
                $"/api/v1/sales/{sale.InvoiceId}/cancel",
                new { reason = "Anulación" }
            )
        );

    private async Task<State> StateAsync(Sale sale)
    {
        using var scope = _f.CreateDbScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var invoice = await db
            .SalesInvoices.IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(x => x.Id == sale.InvoiceId);
        var returns = await db
            .SalesReturns.IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r => r.SalesInvoiceId == sale.InvoiceId)
            .Select(r => new { r.Id, r.Status })
            .ToListAsync();
        var returnIds = returns.Select(r => r.Id).ToList();
        var invoiceStock = await db
            .StockMovements.IgnoreQueryFilters()
            .Where(m => m.SourceDocId == sale.InvoiceId && m.SourceDocType == "SalesInvoice")
            .Select(m => new { m.MovementType, m.Quantity })
            .ToListAsync();
        var returnedIn =
            await db
                .StockMovements.IgnoreQueryFilters()
                .Where(m =>
                    m.SourceDocId != null
                    && returnIds.Contains(m.SourceDocId.Value)
                    && m.SourceDocType == "SalesReturn"
                )
                .SumAsync(m => (decimal?)m.Quantity)
            ?? 0m;
        var cashRefunds = await db
            .CashMovements.IgnoreQueryFilters()
            .CountAsync(m =>
                m.ReferenceId != null
                && returnIds.Contains(m.ReferenceId.Value)
                && m.MovementType == CashMovementType.SaleRefund
            );
        var receivable = await db
            .SalesReceivables.IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r => r.InvoiceId == sale.InvoiceId)
            .Select(r => new { r.OriginalAmount, r.Status })
            .SingleOrDefaultAsync();
        var invoiceEntries = await db
            .JournalEntries.IgnoreQueryFilters()
            .Where(j => j.SourceEventId == sale.InvoiceId)
            .Select(j => j.Id)
            .ToListAsync();
        var reversed = await db
            .JournalEntries.IgnoreQueryFilters()
            .CountAsync(j =>
                j.OriginalJournalEntryId != null
                && invoiceEntries.Contains(j.OriginalJournalEntryId.Value)
            );
        var returnEntries = await db
            .JournalEntries.IgnoreQueryFilters()
            .CountAsync(j => returnIds.Contains(j.SourceEventId));
        var creditNotes = await db
            .SalesReturns.IgnoreQueryFilters()
            .CountAsync(r =>
                r.SalesInvoiceId == sale.InvoiceId && r.CreditNoteDocumentNumber != null
            );
        return new State(
            invoice.Status,
            returns.Count(r => r.Status == SalesReturnStatus.Authorized),
            returns.Count(r => r.Status == SalesReturnStatus.Draft),
            invoiceStock
                .Where(m => m.MovementType == StockMovementType.SaleExit)
                .Sum(m => Math.Abs(m.Quantity)),
            Math.Abs(returnedIn),
            invoiceStock
                .Where(m => m.MovementType == StockMovementType.SaleReturn)
                .Sum(m => Math.Abs(m.Quantity)),
            cashRefunds,
            receivable?.OriginalAmount,
            receivable?.Status,
            invoiceEntries.Count,
            reversed,
            returnEntries,
            creditNotes,
            await db.OutboxMessages.IgnoreQueryFilters().CountAsync(o => o.TenantId == _f.TenantId)
        );
    }

    /// <summary>
    /// Nunca se revierte más de lo vendido: cantidad devuelta + cantidad revertida por anulación ≤
    /// vendida; una factura anulada no tiene devoluciones autorizadas ni reembolsos, y sus asientos se
    /// reversan exactamente una vez.
    /// </summary>
    private static void AssertNeverOverReversed(State s)
    {
        (s.ReturnedIn + s.CancelledIn)
            .Should()
            .BeLessThanOrEqualTo(s.SoldOut, "Kardex: devuelto + anulado ≤ vendido");
        if (s.InvoiceStatus == SalesInvoiceStatus.Cancelled)
        {
            s.AuthorizedReturns.Should()
                .Be(0, "una factura anulada no puede tener devoluciones autorizadas");
            s.CashRefunds.Should().Be(0, "sin reembolso además de la anulación");
            s.ReturnEntries.Should().Be(0, "sin asiento de devolución además del reverso completo");
            s.InvoiceEntriesReversed.Should()
                .Be(s.InvoiceEntries, "cada asiento de la factura se reversa una vez");
            s.CancelledIn.Should().Be(s.SoldOut);
        }
        else
        {
            s.CancelledIn.Should().Be(0);
            s.InvoiceEntriesReversed.Should().Be(0);
        }
    }

    private void Log(string label, State s, params Response[] responses) =>
        _out.WriteLine(
            $"{label}: {string.Join(" | ", responses.Select(r => $"{(int)r.Status} {r.Code}"))} → factura={s.InvoiceStatus} devAut={s.AuthorizedReturns} devDraft={s.DraftReturns} kardex vendido={s.SoldOut} devuelto={s.ReturnedIn} anulado={s.CancelledIn} reembolsos={s.CashRefunds} cxc={s.ReceivableOriginal}/{s.ReceivableStatus} asientosFactura={s.InvoiceEntries} reversados={s.InvoiceEntriesReversed} asientosDev={s.ReturnEntries} NC={s.CreditNotes}"
        );

    // ── política con devoluciones autorizadas ───────────────────────────

    [Theory]
    [InlineData(1, false)] // A + C: parcial, reembolso en efectivo
    [InlineData(2, false)] // B + C: total, reembolso en efectivo
    [InlineData(1, true)] //  A + D: parcial, crédito a CxC
    [InlineData(2, true)] //  B + D: total, crédito a CxC
    public async Task Factura_con_devolucion_autorizada_no_puede_anularse_y_no_duplica_efectos(
        int returnedQuantity,
        bool credit
    )
    {
        var sale = await CreateSaleAsync(credit);
        await CreateAuthorizedReturnAsync(sale, returnedQuantity);
        var before = await StateAsync(sale);

        var cancel = await CancelAsync(sale);

        var after = await StateAsync(sale);
        Log(
            $"devuelto {returnedQuantity}/{SoldQuantity} {(credit ? "crédito CxC" : "efectivo")}",
            after,
            cancel
        );
        cancel.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        cancel.Code.Should().Be(DomainRuleViolation);
        after.Should().Be(before, "la anulación rechazada no deja ningún efecto");
        after.InvoiceStatus.Should().Be(SalesInvoiceStatus.Authorized);
        after.ReturnedIn.Should().Be(returnedQuantity, "Kardex exacto: solo lo devuelto");
        after.CashRefunds.Should().Be(credit ? 0 : 1, "un único reembolso");
        AssertNeverOverReversed(after);
    }

    [Fact]
    public async Task Factura_sin_devoluciones_se_anula_normalmente()
    {
        var sale = await CreateSaleAsync();

        (await CancelAsync(sale)).Status.Should().Be(HttpStatusCode.OK);

        var after = await StateAsync(sale);
        after.InvoiceStatus.Should().Be(SalesInvoiceStatus.Cancelled);
        after.CancelledIn.Should().Be(SoldQuantity);
        AssertNeverOverReversed(after);
    }

    [Fact]
    public async Task Devolucion_draft_no_bloquea_la_anulacion_y_queda_inautorizable()
    {
        var sale = await CreateSaleAsync();
        var draft = await CreateReturnDraftAsync(sale, 1m);

        (await CancelAsync(sale)).Status.Should().Be(HttpStatusCode.OK);
        var afterCancel = await StateAsync(sale);
        afterCancel
            .DraftReturns.Should()
            .Be(1, "el Draft no tiene efectos: la anulación no lo toca");

        var authorize = await AuthorizeReturnAsync(sale, draft);

        authorize
            .Status.Should()
            .Be(HttpStatusCode.UnprocessableEntity, "una factura anulada no admite devoluciones");
        var after = await StateAsync(sale);
        after.Should().Be(afterCancel);
        AssertNeverOverReversed(after);
        // El Draft sigue cancelable por su propio flujo (inocuo).
        (await _f.Client.DeleteAsync($"/api/v1/sales/returns/{draft.Id}"))
            .IsSuccessStatusCode.Should()
            .BeTrue();
    }

    [Fact]
    public async Task Devolucion_draft_cancelada_no_bloquea_y_una_autorizada_si_aunque_haya_otra_cancelada()
    {
        var sale = await CreateSaleAsync();
        var cancelledDraft = await CreateReturnDraftAsync(sale, 1m);
        (await _f.Client.DeleteAsync($"/api/v1/sales/returns/{cancelledDraft.Id}"))
            .IsSuccessStatusCode.Should()
            .BeTrue();
        await CreateAuthorizedReturnAsync(sale, 1m);

        var cancel = await CancelAsync(sale);

        cancel.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        cancel.Code.Should().Be(DomainRuleViolation);
        AssertNeverOverReversed(await StateAsync(sale));
    }

    // ── concurrencia ────────────────────────────────────────────────────

    private async Task<NpgsqlTransaction> HoldInvoiceLockAsync(Guid invoiceId)
    {
        string connectionString;
        using (var scope = _f.CreateDbScope())
            connectionString = scope
                .ServiceProvider.GetRequiredService<ErpDbContext>()
                .Database.GetConnectionString()!;
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        var tx = await connection.BeginTransactionAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT 1 FROM sales_invoices WHERE id = @id FOR UPDATE",
            connection,
            tx
        );
        cmd.Parameters.AddWithValue("id", invoiceId);
        await cmd.ExecuteScalarAsync();
        return tx;
    }

    private async Task WaitForInvoiceLockWaitersAsync(int expected)
    {
        using var scope = _f.CreateDbScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (true)
        {
            var waiting = await db
                .Database.SqlQueryRaw<int>(
                    "SELECT count(*)::int AS \"Value\" FROM pg_stat_activity WHERE wait_event_type = 'Lock' AND query ILIKE '%sales_invoices%FOR UPDATE%'"
                )
                .SingleAsync();
            if (waiting >= expected)
                return;
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException(
                    $"Esperaba {expected} requests bloqueadas en la factura; hay {waiting}."
                );
            await Task.Delay(50);
        }
    }

    private static async Task ReleaseAsync(NpgsqlTransaction tx)
    {
        var connection = tx.Connection!;
        await tx.RollbackAsync();
        await connection.DisposeAsync();
    }

    [Fact]
    public async Task Autorizar_devolucion_obtiene_el_lock_primero_y_la_anulacion_en_espera_se_rechaza()
    {
        for (var round = 0; round < 3; round++)
        {
            var sale = await CreateSaleAsync();
            var draft = await CreateReturnDraftAsync(sale, 1m);
            var holder = await HoldInvoiceLockAsync(sale.InvoiceId);
            Task<Response> authorizeTask,
                cancelTask;
            try
            {
                authorizeTask = AuthorizeReturnAsync(sale, draft);
                await WaitForInvoiceLockWaitersAsync(1);
                cancelTask = CancelAsync(sale);
                await WaitForInvoiceLockWaitersAsync(2);
            }
            finally
            {
                await ReleaseAsync(holder);
            }
            var authorize = await authorizeTask;
            var cancel = await cancelTask;

            var after = await StateAsync(sale);
            Log($"ronda {round}", after, authorize, cancel);
            authorize.Status.Should().Be(HttpStatusCode.OK);
            cancel
                .Status.Should()
                .Be(
                    HttpStatusCode.UnprocessableEntity,
                    "la anulación ve la devolución ya autorizada bajo el lock"
                );
            cancel.Code.Should().Be(DomainRuleViolation);
            AssertNeverOverReversed(after);
        }
    }

    [Fact]
    public async Task Autorizar_devolucion_y_anular_simultaneos_nunca_dejan_ambos_efectos()
    {
        for (var round = 0; round < 8; round++)
        {
            var sale = await CreateSaleAsync(credit: round % 2 == 1);
            var draft = await CreateReturnDraftAsync(sale, 1m);

            var authorizeTask = AuthorizeReturnAsync(sale, draft);
            var cancelTask = CancelAsync(sale);
            var authorize = await authorizeTask;
            var cancel = await cancelTask;

            var after = await StateAsync(sale);
            Log($"ronda {round}", after, authorize, cancel);
            new[] { authorize, cancel }
                .Count(r => r.Status == HttpStatusCode.OK)
                .Should()
                .Be(1, "solo uno de los dos caminos revierte la venta");
            new[] { authorize, cancel }.Should().OnlyContain(r => (int)r.Status < 500);
            AssertNeverOverReversed(after);
        }
    }

    [Fact]
    public async Task Doble_anulacion_con_devolucion_draft_produce_un_solo_conjunto_de_efectos()
    {
        for (var round = 0; round < 5; round++)
        {
            var sale = await CreateSaleAsync();
            await CreateReturnDraftAsync(sale, 1m);

            var responses = await Task.WhenAll(CancelAsync(sale), CancelAsync(sale));

            var after = await StateAsync(sale);
            Log($"ronda {round}", after, responses);
            responses.Count(r => r.Status == HttpStatusCode.OK).Should().Be(1);
            responses
                .Single(r => r.Status != HttpStatusCode.OK)
                .Code.Should()
                .Be(DomainRuleViolation);
            AssertNeverOverReversed(after);
        }
    }

    // ── alcance ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Anulacion_desde_otra_empresa_es_fail_closed_sin_efectos()
    {
        var sale = await CreateSaleAsync();
        await CreateAuthorizedReturnAsync(sale, 1m);
        var before = await StateAsync(sale);
        var company = _f.Factory.Services.GetRequiredService<MutableCurrentCompany>();
        var ownCompanyId = company.CompanyId;
        Response cancel;
        company.CompanyId = Guid.NewGuid();
        try
        {
            cancel = await CancelAsync(sale);
        }
        finally
        {
            company.CompanyId = ownCompanyId;
        }

        cancel.Status.Should().NotBe(HttpStatusCode.OK);
        ((int)cancel.Status).Should().BeLessThan(500);
        (await StateAsync(sale)).Should().Be(before);
    }
}
