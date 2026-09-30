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
/// ZH-SALES-RETURN-INVOICE-STATE-CONCURRENCY-01 — una devolución de venta solo se autoriza mientras
/// su factura origen sigue <c>Authorized</c> (misma regla que la creación del Draft), evaluada bajo
/// el lock de la factura e inmediatamente antes de producir efectos (Kardex, reembolso en caja /
/// crédito a CxC, asiento, outbox, Nota de Crédito). HTTP real + PostgreSQL real, factura autorizada
/// por el flujo oficial; todo estado final se lee desde un DbContext independiente.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class SalesReturnInvoiceStateConcurrencyTests : IClassFixture<SalesReturnFlowFixture>
{
    private const int Rounds = 8;
    /// <summary>Mismo resultado canónico que la creación del Draft sobre una factura no autorizada.</summary>
    private const string NotReturnable = "VALIDATION_ERROR";

    private readonly SalesReturnFlowFixture _f;
    private readonly ITestOutputHelper _out;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public SalesReturnInvoiceStateConcurrencyTests(SalesReturnFlowFixture f, ITestOutputHelper o)
    {
        _f = f;
        _out = o;
    }

    // ── helpers ──────────────────────────────────────────────────────────

    private sealed record Response(HttpStatusCode Status, string Code);

    private sealed record Scenario(Guid InvoiceId, Guid ReturnId, decimal ReturnTotal, bool CashRefund);

    /// <summary>Estado físico de factura + devolución + todos los efectos que produce autorizarla.</summary>
    private sealed record State(
        SalesInvoiceStatus InvoiceStatus,
        DateTime? InvoiceCancelledAt,
        SalesReturnStatus ReturnStatus,
        DateTime? ReturnUpdatedAt,
        string? CreditNoteNumber,
        int ReturnStockMovements,
        int ReturnCashRefunds,
        decimal? ReceivableOriginal,
        int ReturnJournalEntries,
        int CancelStockMovements,
        int OutboxMessages
    );

    private static async Task<Response> ReadAsync(HttpResponseMessage http)
    {
        var text = await http.Content.ReadAsStringAsync();
        var json = string.IsNullOrWhiteSpace(text) ? default : JsonDocument.Parse(text).RootElement;
        var code = json.ValueKind == JsonValueKind.Object && json.TryGetProperty("code", out var c) ? c.GetString() ?? "" : "";
        return new Response(http.StatusCode, code);
    }

    /// <summary>Factura autorizada (contado → reembolso en efectivo; crédito → crédito a CxC) + devolución Draft de 1 unidad.</summary>
    private async Task<Scenario> CreateScenarioAsync(bool cashRefund = true)
    {
        var userId = await _f.CreateUserWithBranchAccessAsync();
        _f.SetActiveContext(userId);
        var cashRegisterId = await _f.CreateCashRegisterAsync();
        var open = await _f.Client.PostAsJsonAsync("/api/v1/cash-sessions/open", new { cashRegisterId, openingAmount = 100m });
        open.StatusCode.Should().Be(HttpStatusCode.Created, await open.Content.ReadAsStringAsync());

        const decimal quantity = 2m;
        const decimal unitPrice = 50m;
        var vat = Math.Round(quantity * unitPrice * _f.VatPercentage / 100m, 2, MidpointRounding.AwayFromZero);
        var create = await _f.Client.PostAsJsonAsync("/api/v1/sales", new
        {
            customerId = _f.CustomerId,
            issueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)).ToString("yyyy-MM-dd"),
            paymentTermId = cashRefund ? _f.CashPaymentTermId : _f.CreditPaymentTermId,
            lines = new[]
            {
                new { itemId = _f.ItemId, description = "Producto devolución", quantity, unitPrice, vatCode = _f.VatCode, warehouseId = _f.WarehouseId },
            },
            payments = new[] { new { paymentMethodId = cashRefund ? _f.PaymentMethodId : _f.CreditPaymentMethodId, amount = quantity * unitPrice + vat } },
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created, await create.Content.ReadAsStringAsync());
        var draft = (await create.Content.ReadFromJsonAsync<SrEnvelope<SrSalesInvoiceResponseDto>>(JsonOptions))!.Data!;
        var authorize = await _f.Client.PostAsync($"/api/v1/sales/{draft.Id}/authorize", null);
        authorize.StatusCode.Should().Be(HttpStatusCode.OK, await authorize.Content.ReadAsStringAsync());
        var invoice = (await authorize.Content.ReadFromJsonAsync<SrEnvelope<SrSalesInvoiceResponseDto>>(JsonOptions))!.Data!;

        var returnResponse = await _f.Client.PostAsJsonAsync("/api/v1/sales/returns", new
        {
            salesInvoiceId = invoice.Id,
            reason = "Devolución concurrente",
            lines = new[] { new { invoiceDetailId = invoice.Lines.Single().Id, quantity = 1m } },
        });
        returnResponse.StatusCode.Should().Be(HttpStatusCode.Created, await returnResponse.Content.ReadAsStringAsync());
        var salesReturn = (await returnResponse.Content.ReadFromJsonAsync<SrEnvelope<SalesReturnResponseDto>>(JsonOptions))!.Data!;
        return new Scenario(invoice.Id, salesReturn.Id, salesReturn.GrandTotal, cashRefund);
    }

    private async Task<Response> AuthorizeReturnAsync(Scenario s) =>
        await ReadAsync(await _f.Client.PostAsJsonAsync($"/api/v1/sales/returns/{s.ReturnId}/authorize", new
        {
            refundAllocations = new[] { new { method = s.CashRefund ? "Cash" : "ReceivableCredit", amount = s.ReturnTotal } },
        }));

    private async Task<Response> CancelInvoiceAsync(Scenario s) =>
        await ReadAsync(await _f.Client.PostAsJsonAsync($"/api/v1/sales/{s.InvoiceId}/cancel", new { reason = "Anulación concurrente" }));

    private async Task<State> StateAsync(Scenario s)
    {
        using var scope = _f.CreateDbScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var invoice = await db.SalesInvoices.IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == s.InvoiceId);
        var salesReturn = await db.SalesReturns.IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == s.ReturnId);
        var receivable = await db.SalesReceivables.IgnoreQueryFilters().AsNoTracking()
            .Where(r => r.InvoiceId == s.InvoiceId).Select(r => (decimal?)r.OriginalAmount).SingleOrDefaultAsync();
        return new State(
            invoice.Status,
            invoice.CancelledAt,
            salesReturn.Status,
            salesReturn.UpdatedAt,
            salesReturn.CreditNoteDocumentNumber,
            await db.StockMovements.IgnoreQueryFilters().CountAsync(m => m.SourceDocId == s.ReturnId && m.SourceDocType == "SalesReturn"),
            await db.CashMovements.IgnoreQueryFilters().CountAsync(m =>
                m.ReferenceId == s.ReturnId && m.ReferenceType == CashReferenceType.SalesReturn && m.MovementType == CashMovementType.SaleRefund),
            receivable,
            await db.JournalEntries.IgnoreQueryFilters().CountAsync(j => j.SourceEventId == s.ReturnId),
            await db.StockMovements.IgnoreQueryFilters().CountAsync(m =>
                m.SourceDocId == s.InvoiceId && m.SourceDocType == "SalesInvoice" && m.MovementType == StockMovementType.SaleReturn),
            await db.OutboxMessages.IgnoreQueryFilters().CountAsync(o => o.TenantId == _f.TenantId)
        );
    }

    /// <summary>
    /// Invariante: una devolución autorizada lo fue mientras la factura seguía autorizada (si hoy está
    /// anulada, la anulación ocurrió DESPUÉS); una devolución no autorizada no dejó ningún efecto; una
    /// autorizada dejó exactamente un conjunto de efectos.
    /// </summary>
    private static void AssertValid(State s, Scenario scenario)
    {
        if (s.ReturnStatus == SalesReturnStatus.Authorized)
        {
            if (s.InvoiceStatus == SalesInvoiceStatus.Cancelled)
                s.ReturnUpdatedAt.Should().BeBefore(s.InvoiceCancelledAt!.Value, "la devolución se autorizó antes de la anulación, nunca sobre una factura anulada");
            s.ReturnStockMovements.Should().Be(1, "un único reingreso de Kardex");
            s.ReturnCashRefunds.Should().Be(scenario.CashRefund ? 1 : 0, "un único reembolso en caja");
            s.CreditNoteNumber.Should().NotBeNull();
        }
        else
        {
            s.ReturnStatus.Should().Be(SalesReturnStatus.Draft);
            s.ReturnStockMovements.Should().Be(0, "sin Kardex de la devolución perdedora");
            s.ReturnCashRefunds.Should().Be(0, "sin reembolso de la devolución perdedora");
            s.ReturnJournalEntries.Should().Be(0, "sin asiento de la devolución perdedora");
            s.CreditNoteNumber.Should().BeNull("sin Nota de Crédito ni secuencial consumido");
        }
        s.CancelStockMovements.Should().Be(s.InvoiceStatus == SalesInvoiceStatus.Cancelled ? 1 : 0);
    }

    private void Log(string label, State s, params Response[] responses) =>
        _out.WriteLine(
            $"{label}: {string.Join(" | ", responses.Select(r => $"{(int)r.Status} {r.Code}"))} → factura={s.InvoiceStatus} devolución={s.ReturnStatus} kardexDev={s.ReturnStockMovements} reembolsoCaja={s.ReturnCashRefunds} cxcOriginal={s.ReceivableOriginal} asientosDev={s.ReturnJournalEntries} NC={s.CreditNoteNumber ?? "-"} kardexAnulación={s.CancelStockMovements} devAntesDeAnular={(s.InvoiceCancelledAt is null || s.ReturnStatus != SalesReturnStatus.Authorized ? "-" : (s.ReturnUpdatedAt < s.InvoiceCancelledAt).ToString())}"
        );

    /// <summary>Retiene el lock de la fila de la factura desde otra conexión para encolar las requests en orden conocido.</summary>
    private async Task<NpgsqlTransaction> HoldInvoiceLockAsync(Guid invoiceId)
    {
        string connectionString;
        using (var scope = _f.CreateDbScope())
            connectionString = scope.ServiceProvider.GetRequiredService<ErpDbContext>().Database.GetConnectionString()!;
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        var tx = await connection.BeginTransactionAsync();
        await using var cmd = new NpgsqlCommand("SELECT 1 FROM sales_invoices WHERE id = @id FOR UPDATE", connection, tx);
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
            var waiting = await db.Database
                .SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM pg_stat_activity WHERE wait_event_type = 'Lock' AND query ILIKE '%sales_invoices%FOR UPDATE%'")
                .SingleAsync();
            if (waiting >= expected)
                return;
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException($"Esperaba {expected} requests bloqueadas en la factura; hay {waiting}.");
            await Task.Delay(50);
        }
    }

    private static async Task ReleaseAsync(NpgsqlTransaction tx)
    {
        var connection = tx.Connection!;
        await tx.RollbackAsync();
        await connection.DisposeAsync();
    }

    // ── secuencial ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Draft_luego_anular_factura_luego_autorizar_se_rechaza_sin_efectos(bool cashRefund)
    {
        var s = await CreateScenarioAsync(cashRefund);
        (await CancelInvoiceAsync(s)).Status.Should().Be(HttpStatusCode.OK);
        var before = await StateAsync(s);

        var authorize = await AuthorizeReturnAsync(s);

        var after = await StateAsync(s);
        Log(cashRefund ? "efectivo" : "crédito CxC", after, authorize);
        authorize.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        authorize.Code.Should().Be(NotReturnable);
        after.Should().Be(before, "cero efectos: Kardex, reembolso, CxC, asiento, outbox, Nota de Crédito");
        AssertValid(after, s);
    }

    // ── orden forzado bajo el lock de la factura ────────────────────────

    [Fact]
    public async Task Autorizar_obtiene_el_lock_primero_y_la_anulacion_en_espera_se_reevalua()
    {
        for (var round = 0; round < 3; round++)
        {
            var s = await CreateScenarioAsync();
            var holder = await HoldInvoiceLockAsync(s.InvoiceId);
            Task<Response> authorizeTask, cancelTask;
            try
            {
                authorizeTask = AuthorizeReturnAsync(s);
                await WaitForInvoiceLockWaitersAsync(1);
                cancelTask = CancelInvoiceAsync(s);
                await WaitForInvoiceLockWaitersAsync(2);
            }
            finally
            {
                await ReleaseAsync(holder);
            }
            var authorize = await authorizeTask;
            var cancel = await cancelTask;

            var after = await StateAsync(s);
            Log($"ronda {round}", after, authorize, cancel);
            authorize.Status.Should().Be(HttpStatusCode.OK);
            after.ReturnStatus.Should().Be(SalesReturnStatus.Authorized);
            // La anulación reevalúa con la regla vigente de CancelSalesInvoice, que no considera
            // devoluciones autorizadas (hallazgo P1 reportado, no cambiado en este ticket): procede.
            cancel.Status.Should().Be(HttpStatusCode.OK);
            AssertValid(after, s);
        }
    }

    [Fact]
    public async Task Anular_obtiene_el_lock_primero_y_la_autorizacion_en_espera_se_rechaza_sin_efectos()
    {
        for (var round = 0; round < 3; round++)
        {
            var s = await CreateScenarioAsync();
            var holder = await HoldInvoiceLockAsync(s.InvoiceId);
            Task<Response> authorizeTask, cancelTask;
            try
            {
                cancelTask = CancelInvoiceAsync(s);
                await WaitForInvoiceLockWaitersAsync(1);
                authorizeTask = AuthorizeReturnAsync(s);
                await WaitForInvoiceLockWaitersAsync(2);
            }
            finally
            {
                await ReleaseAsync(holder);
            }
            var cancel = await cancelTask;
            var authorize = await authorizeTask;

            var after = await StateAsync(s);
            Log($"ronda {round}", after, cancel, authorize);
            cancel.Status.Should().Be(HttpStatusCode.OK);
            authorize.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
            authorize.Code.Should().Be(NotReturnable);
            AssertValid(after, s);
        }
    }

    // ── simultáneo ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Autorizar_y_anular_simultaneos_nunca_dejan_una_devolucion_autorizada_sobre_factura_anulada(bool cashRefund)
    {
        for (var round = 0; round < Rounds; round++)
        {
            var s = await CreateScenarioAsync(cashRefund);

            var authorizeTask = AuthorizeReturnAsync(s);
            var cancelTask = CancelInvoiceAsync(s);
            var authorize = await authorizeTask;
            var cancel = await cancelTask;

            var after = await StateAsync(s);
            Log($"ronda {round}", after, authorize, cancel);
            new[] { authorize, cancel }.Should().OnlyContain(r => (int)r.Status < 500, "sin deadlock ni error de servidor");
            if (authorize.Status != HttpStatusCode.OK)
                authorize.Code.Should().Be(NotReturnable);
            AssertValid(after, s);
        }
    }

    // ── doble autorización ─────────────────────────────────────────────

    [Fact]
    public async Task Dos_autorizaciones_simultaneas_de_la_misma_devolucion_producen_un_solo_conjunto_de_efectos()
    {
        for (var round = 0; round < 5; round++)
        {
            var s = await CreateScenarioAsync();
            var before = await StateAsync(s);

            var responses = await Task.WhenAll(AuthorizeReturnAsync(s), AuthorizeReturnAsync(s));

            var after = await StateAsync(s);
            Log($"ronda {round}", after, responses);
            responses.Count(r => r.Status == HttpStatusCode.OK).Should().Be(1);
            ((int)responses.Single(r => r.Status != HttpStatusCode.OK).Status).Should().BeLessThan(500);
            AssertValid(after, s);
            after.ReturnJournalEntries.Should().BeLessThanOrEqualTo(2);
            before.ReturnJournalEntries.Should().Be(0);
        }
    }

    // ── alcance ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Autorizar_desde_otra_empresa_es_fail_closed_sin_efectos()
    {
        var s = await CreateScenarioAsync();
        var before = await StateAsync(s);
        var company = _f.Factory.Services.GetRequiredService<MutableCurrentCompany>();
        var ownCompanyId = company.CompanyId;
        Response authorize;
        company.CompanyId = Guid.NewGuid();
        try
        {
            authorize = await AuthorizeReturnAsync(s);
        }
        finally
        {
            company.CompanyId = ownCompanyId;
        }

        authorize.Status.Should().NotBe(HttpStatusCode.OK);
        ((int)authorize.Status).Should().BeLessThan(500);
        (await StateAsync(s)).Should().Be(before);
    }
}
