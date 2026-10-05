using ERP.API.Tests.Support;
using ERP.Application.Modules.Finance.UseCases.Payments;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit.Abstractions;

namespace ERP.API.Tests.Integration;

/// <summary>
/// ZH-SALES-CANCEL-COLLECTION-CONCURRENCY-01 — anulación de factura de venta vs cobro / reversa de
/// cobro / devolución / otra anulación sobre la misma factura y su CxC, con HTTP real + PostgreSQL
/// real (factura a crédito autorizada por el flujo oficial: CxC, Kardex y asientos reales).
/// Regla vigente que se preserva (no se inventa): una factura solo se anula si su CxC no tiene
/// cobros registrados (<c>SalesReceivable.Cancel</c>: PaidAmount &gt; 0 → rechazo), y una CxC
/// anulada no admite cobros (<c>RegisterCollection</c>). Todo estado final se lee desde un
/// DbContext independiente; los escenarios concurrentes se repiten varias veces.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class SalesCancelCollectionConcurrencyTests : IClassFixture<SalesReturnFlowFixture>
{
    private const int Rounds = 8;
    private const string DomainRuleViolation = "DOMAIN_RULE_VIOLATION";

    private readonly SalesReturnFlowFixture _f;
    private readonly ITestOutputHelper _out;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public SalesCancelCollectionConcurrencyTests(SalesReturnFlowFixture f, ITestOutputHelper o)
    {
        _f = f;
        _out = o;
    }

    // ── helpers ──────────────────────────────────────────────────────────

    private sealed record Response(HttpStatusCode Status, string Code, Guid? Id);

    private sealed record Invoice(Guid Id, Guid LineId, Guid ReceivableId, decimal GrandTotal);

    /// <summary>Estado físico de la factura y su CxC + efectos contables/Kardex/outbox.</summary>
    private sealed record State(
        SalesInvoiceStatus InvoiceStatus,
        string ReceivableStatus,
        decimal Paid,
        decimal OriginalAmount,
        int AppliedCollections,
        decimal AppliedAmount,
        int ReversedCollections,
        int CancelStockMovements,
        int InvoiceJournalEntries,
        int InvoiceJournalReversals,
        int JournalEntries,
        int OutboxMessages
    );

    private static async Task<Response> ReadAsync(HttpResponseMessage http)
    {
        var text = await http.Content.ReadAsStringAsync();
        var json = string.IsNullOrWhiteSpace(text) ? default : JsonDocument.Parse(text).RootElement;
        var code = json.ValueKind == JsonValueKind.Object && json.TryGetProperty("code", out var c) ? c.GetString() ?? "" : "";
        Guid? id = json.ValueKind == JsonValueKind.Object
            && json.TryGetProperty("data", out var d)
            && d.ValueKind == JsonValueKind.Object
            && d.TryGetProperty("id", out var i)
            ? i.GetGuid()
            : null;
        return new Response(http.StatusCode, code, id);
    }

    private async Task<Invoice> CreateCreditInvoiceAsync()
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
            paymentTermId = _f.CreditPaymentTermId,
            lines = new[]
            {
                new { itemId = _f.ItemId, description = "Producto anulación", quantity, unitPrice, vatCode = _f.VatCode, warehouseId = _f.WarehouseId },
            },
            payments = new[] { new { paymentMethodId = _f.CreditPaymentMethodId, amount = quantity * unitPrice + vat } },
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created, await create.Content.ReadAsStringAsync());
        var draft = (await create.Content.ReadFromJsonAsync<SrEnvelope<SrSalesInvoiceResponseDto>>(JsonOptions))!.Data!;

        var authorize = await _f.Client.PostAsync($"/api/v1/sales/{draft.Id}/authorize", null);
        authorize.StatusCode.Should().Be(HttpStatusCode.OK, await authorize.Content.ReadAsStringAsync());
        var authorized = (await authorize.Content.ReadFromJsonAsync<SrEnvelope<SrSalesInvoiceResponseDto>>(JsonOptions))!.Data!;

        using var scope = _f.CreateDbScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var receivableId = await db.SalesReceivables.IgnoreQueryFilters()
            .Where(r => r.InvoiceId == draft.Id).Select(r => r.Id).SingleAsync();
        return new Invoice(draft.Id, authorized.Lines.Single().Id, receivableId, authorized.GrandTotal);
    }

    private async Task<Response> CancelAsync(Guid invoiceId, HttpClient? client = null) =>
        await ReadAsync(await (client ?? _f.Client).PostAsJsonAsync($"/api/v1/sales/{invoiceId}/cancel", new { reason = "Anulación de prueba" }));

    private async Task<Response> CollectAsync(Invoice invoice, decimal amount, Guid? clientRequestId = null) =>
        await ReadAsync(await _f.Client.PostAsJsonAsync("/api/v1/finance/collections", new
        {
            customerId = _f.CustomerId,
            amount,
            paymentDate = DateOnly.FromDateTime(DateTime.UtcNow),
            paymentMethodId = _f.PaymentMethodId,
            reference = (string?)null,
            lines = new[] { new { documentId = invoice.ReceivableId, installmentId = (Guid?)null, appliedAmount = amount } },
            clientRequestId = clientRequestId ?? Guid.NewGuid(),
        }));

    /// <summary>La reversa de cobro no tiene endpoint HTTP: se envía por el mismo pipeline MediatR del host real.</summary>
    private async Task<Response> ReverseAsync(Guid paymentId)
    {
        using var scope = _f.CreateDbScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        try
        {
            var result = await mediator.Send(new ReverseCollectionCommand(paymentId, "Reversa de prueba"));
            return new Response(result.IsSuccess ? HttpStatusCode.OK : HttpStatusCode.UnprocessableEntity, result.IsSuccess ? "" : "FAILURE", paymentId);
        }
        catch (ERP.Domain.Exceptions.DomainRuleViolationException)
        {
            return new Response(HttpStatusCode.UnprocessableEntity, DomainRuleViolation, paymentId);
        }
    }

    private async Task<Response> AuthorizeReturnCreditAsync(Invoice invoice)
    {
        var create = await _f.Client.PostAsJsonAsync("/api/v1/sales/returns", new
        {
            salesInvoiceId = invoice.Id,
            reason = "Devolución concurrente",
            lines = new[] { new { invoiceDetailId = invoice.LineId, quantity = 1m } },
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created, await create.Content.ReadAsStringAsync());
        var draft = (await create.Content.ReadFromJsonAsync<SrEnvelope<SalesReturnResponseDto>>(JsonOptions))!.Data!;
        return await ReadAsync(await _f.Client.PostAsJsonAsync($"/api/v1/sales/returns/{draft.Id}/authorize", new
        {
            refundAllocations = new[] { new { method = "ReceivableCredit", amount = draft.GrandTotal } },
        }));
    }

    private async Task<State> StateAsync(Invoice invoice)
    {
        using var scope = _f.CreateDbScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var inv = await db.SalesInvoices.IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == invoice.Id);
        var rx = await db.SalesReceivables.IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == invoice.ReceivableId);
        var collections = await db.Payments.IgnoreQueryFilters()
            .Where(p => p.Direction == PaymentDirection.Collection && p.Lines.Any(l => l.ReceivableId == invoice.ReceivableId))
            .Select(p => new { p.Status, p.Amount })
            .ToListAsync();
        var cancelStock = await db.StockMovements.IgnoreQueryFilters().CountAsync(m =>
            m.SourceDocId == invoice.Id && m.SourceDocType == "SalesInvoice" && m.MovementType == StockMovementType.SaleReturn);
        var invoiceJournalIds = await db.JournalEntries.IgnoreQueryFilters()
            .Where(j => j.SourceEventId == invoice.Id).Select(j => j.Id).ToListAsync();
        var invoiceJournalReversals = await db.JournalEntries.IgnoreQueryFilters()
            .CountAsync(j => j.OriginalJournalEntryId != null && invoiceJournalIds.Contains(j.OriginalJournalEntryId.Value));
        var journals = await db.JournalEntries.IgnoreQueryFilters().CountAsync(j => j.TenantId == _f.TenantId);
        var outbox = await db.OutboxMessages.IgnoreQueryFilters().CountAsync(o => o.TenantId == _f.TenantId);
        return new State(
            inv.Status,
            rx.Status,
            rx.PaidAmount,
            rx.OriginalAmount,
            collections.Count(c => c.Status == PaymentStatus.Applied),
            collections.Where(c => c.Status == PaymentStatus.Applied).Sum(c => c.Amount),
            collections.Count(c => c.Status == PaymentStatus.Reversed),
            cancelStock,
            invoiceJournalIds.Count,
            invoiceJournalReversals,
            journals,
            outbox
        );
    }

    /// <summary>
    /// Invariante final: la CxC nunca queda anulada con cobros activos, el saldo pagado coincide con
    /// los cobros aplicados y la factura y su CxC están anuladas juntas o ninguna.
    /// </summary>
    private static void AssertValid(State s)
    {
        s.Paid.Should().Be(s.AppliedAmount, "PaidAmount = Σ cobros aplicados");
        var invoiceCancelled = s.InvoiceStatus == SalesInvoiceStatus.Cancelled;
        (s.ReceivableStatus == "cancelled").Should().Be(invoiceCancelled, "factura y CxC se anulan juntas");
        if (invoiceCancelled)
        {
            s.Paid.Should().Be(0m, "una CxC anulada no puede tener cobros registrados");
            s.AppliedCollections.Should().Be(0, "no puede quedar un cobro aplicado sobre una factura anulada");
            s.CancelStockMovements.Should().Be(1, "el Kardex se revierte exactamente una vez");
            s.InvoiceJournalReversals.Should().Be(s.InvoiceJournalEntries, "cada asiento de la factura se reversa una sola vez");
        }
        else
        {
            s.CancelStockMovements.Should().Be(0);
            s.InvoiceJournalReversals.Should().Be(0);
        }
    }

    private void Log(string label, State s, params Response[] responses) =>
        _out.WriteLine(
            $"{label}: {string.Join(" | ", responses.Select(r => $"{(int)r.Status} {r.Code}"))} → factura={s.InvoiceStatus} cxc={s.ReceivableStatus} pagado={s.Paid} aplicados={s.AppliedCollections}({s.AppliedAmount}) reversados={s.ReversedCollections} kardexAnulación={s.CancelStockMovements} asientosFactura={s.InvoiceJournalEntries} reversos={s.InvoiceJournalReversals}"
        );

    /// <summary>
    /// Toma el lock de la CxC desde una conexión propia: permite encolar las requests en un orden
    /// conocido (ambas esperan el mismo lock) y verificar que el segundo en obtenerlo reevalúa.
    /// </summary>
    private async Task<NpgsqlTransaction> HoldReceivableLockAsync(Guid receivableId)
    {
        string connectionString;
        using (var scope = _f.CreateDbScope())
            connectionString = scope.ServiceProvider.GetRequiredService<ErpDbContext>().Database.GetConnectionString()!;
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        var tx = await connection.BeginTransactionAsync();
        await using var cmd = new NpgsqlCommand("SELECT 1 FROM sales_receivables WHERE id = @id FOR UPDATE", connection, tx);
        cmd.Parameters.AddWithValue("id", receivableId);
        await cmd.ExecuteScalarAsync();
        return tx;
    }

    private async Task WaitForLockWaitersAsync(int expected)
    {
        using var scope = _f.CreateDbScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (true)
        {
            var waiting = await db.Database
                .SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM pg_stat_activity WHERE wait_event_type = 'Lock' AND query ILIKE '%sales_receivables%FOR UPDATE%'")
                .SingleAsync();
            if (waiting >= expected)
                return;
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException($"Esperaba {expected} requests bloqueadas en la CxC; hay {waiting}.");
            await Task.Delay(50);
        }
    }

    private static async Task ReleaseAsync(NpgsqlTransaction tx)
    {
        var connection = tx.Connection!;
        await tx.RollbackAsync();
        await connection.DisposeAsync();
    }

    // ── Cancel vs Collection ─────────────────────────────────────────────

    [Fact]
    public async Task Cancel_obtiene_el_lock_primero_y_el_cobro_en_espera_se_reevalua_y_se_rechaza()
    {
        for (var round = 0; round < 3; round++)
        {
            var invoice = await CreateCreditInvoiceAsync();
            var before = await StateAsync(invoice);
            var holder = await HoldReceivableLockAsync(invoice.ReceivableId);
            Task<Response> cancelTask, collectTask;
            try
            {
                cancelTask = CancelAsync(invoice.Id);
                await WaitForLockWaitersAsync(1);
                collectTask = CollectAsync(invoice, 50m);
                await WaitForLockWaitersAsync(2);
            }
            finally
            {
                await ReleaseAsync(holder);
            }
            var cancel = await cancelTask;
            var collect = await collectTask;

            var after = await StateAsync(invoice);
            Log($"ronda {round}", after, cancel, collect);
            cancel.Status.Should().Be(HttpStatusCode.OK);
            collect.Status.Should().Be(HttpStatusCode.UnprocessableEntity, "el cobro ve la CxC ya anulada bajo el lock");
            collect.Code.Should().Be(DomainRuleViolation);
            AssertValid(after);
            after.AppliedCollections.Should().Be(before.AppliedCollections);
        }
    }

    [Fact]
    public async Task Cobro_obtiene_el_lock_primero_y_la_anulacion_en_espera_se_reevalua_y_se_rechaza()
    {
        for (var round = 0; round < 3; round++)
        {
            var invoice = await CreateCreditInvoiceAsync();
            var before = await StateAsync(invoice);
            var holder = await HoldReceivableLockAsync(invoice.ReceivableId);
            Task<Response> cancelTask, collectTask;
            try
            {
                collectTask = CollectAsync(invoice, 50m);
                await WaitForLockWaitersAsync(1);
                cancelTask = CancelAsync(invoice.Id);
                await WaitForLockWaitersAsync(2);
            }
            finally
            {
                await ReleaseAsync(holder);
            }
            var collect = await collectTask;
            var cancel = await cancelTask;

            var after = await StateAsync(invoice);
            Log($"ronda {round}", after, collect, cancel);
            collect.Status.Should().Be(HttpStatusCode.Created);
            cancel.Status.Should().Be(HttpStatusCode.UnprocessableEntity, "la anulación ve el cobro ya registrado bajo el lock");
            cancel.Code.Should().Be(DomainRuleViolation);
            AssertValid(after);
            after.CancelStockMovements.Should().Be(before.CancelStockMovements, "sin reverso de Kardex");
            after.InvoiceJournalReversals.Should().Be(0, "sin reverso contable");
        }
    }

    [Fact]
    public async Task Cancel_primero_luego_cobro_el_cobro_se_rechaza_sin_efectos()
    {
        var invoice = await CreateCreditInvoiceAsync();
        (await CancelAsync(invoice.Id)).Status.Should().Be(HttpStatusCode.OK);
        var before = await StateAsync(invoice);

        var collect = await CollectAsync(invoice, 50m);

        collect.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        collect.Code.Should().Be(DomainRuleViolation);
        var after = await StateAsync(invoice);
        after.Should().Be(before, "el cobro rechazado no deja ningún efecto");
        AssertValid(after);
    }

    [Fact]
    public async Task Cobro_primero_luego_cancel_la_anulacion_se_rechaza_sin_efectos()
    {
        var invoice = await CreateCreditInvoiceAsync();
        (await CollectAsync(invoice, 50m)).Status.Should().Be(HttpStatusCode.Created);
        var before = await StateAsync(invoice);

        var cancel = await CancelAsync(invoice.Id);

        cancel.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        cancel.Code.Should().Be(DomainRuleViolation);
        var after = await StateAsync(invoice);
        after.Should().Be(before, "la anulación rechazada no revierte Kardex, asientos ni CxC");
        after.InvoiceStatus.Should().Be(SalesInvoiceStatus.Authorized);
        AssertValid(after);
    }

    [Fact]
    public async Task Cancel_y_cobro_simultaneos_terminan_siempre_en_una_combinacion_valida()
    {
        for (var round = 0; round < Rounds; round++)
        {
            var invoice = await CreateCreditInvoiceAsync();
            var before = await StateAsync(invoice);

            var cancelTask = CancelAsync(invoice.Id);
            var collectTask = CollectAsync(invoice, 50m);
            var cancel = await cancelTask;
            var collect = await collectTask;

            var after = await StateAsync(invoice);
            Log($"ronda {round}", after, cancel, collect);
            AssertValid(after);
            // Exactamente uno gana: la regla vigente no permite ambos efectos.
            new[] { cancel, collect }.Count(r => r.Status is HttpStatusCode.OK or HttpStatusCode.Created).Should().Be(1);
            var loser = cancel.Status == HttpStatusCode.OK ? collect : cancel;
            loser.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
            loser.Code.Should().Be(DomainRuleViolation);
            if (cancel.Status == HttpStatusCode.OK)
                after.JournalEntries.Should().BeGreaterThan(before.JournalEntries, "la anulación reversa los asientos de la factura");
            else
                after.JournalEntries.Should().Be(before.JournalEntries + 1, "solo el asiento del cobro");
        }
    }

    [Fact]
    public async Task Cobro_idempotente_replay_sigue_funcionando_tras_una_anulacion_rechazada()
    {
        var invoice = await CreateCreditInvoiceAsync();
        var key = Guid.NewGuid();
        var first = await CollectAsync(invoice, 40m, key);
        first.Status.Should().Be(HttpStatusCode.Created);
        (await CancelAsync(invoice.Id)).Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        var before = await StateAsync(invoice);

        var replay = await CollectAsync(invoice, 40m, key);

        replay.Status.Should().Be(HttpStatusCode.Created);
        replay.Id.Should().Be(first.Id);
        (await StateAsync(invoice)).Should().Be(before);
    }

    // ── Cancel vs ReverseCollection ─────────────────────────────────────

    [Fact]
    public async Task Cancel_y_reversa_de_cobro_simultaneas_terminan_siempre_en_una_combinacion_valida()
    {
        for (var round = 0; round < Rounds; round++)
        {
            var invoice = await CreateCreditInvoiceAsync();
            var collect = await CollectAsync(invoice, 50m);
            collect.Status.Should().Be(HttpStatusCode.Created);

            var cancelTask = CancelAsync(invoice.Id);
            var reverseTask = ReverseAsync(collect.Id!.Value);
            var cancel = await cancelTask;
            var reverse = await reverseTask;

            var after = await StateAsync(invoice);
            Log($"ronda {round}", after, cancel, reverse);
            reverse.Status.Should().Be(HttpStatusCode.OK, "la reversa no depende de la anulación");
            after.ReversedCollections.Should().Be(1);
            AssertValid(after);
            // Anulación antes de la reversa → rechazada (había cobro); después → válida (saldo sin cobros).
            if (cancel.Status != HttpStatusCode.OK)
            {
                cancel.Code.Should().Be(DomainRuleViolation);
                after.InvoiceStatus.Should().Be(SalesInvoiceStatus.Authorized);
            }
        }
    }

    // ── Cancel vs Cancel / SalesReturn ──────────────────────────────────

    [Fact]
    public async Task Dos_anulaciones_simultaneas_revierten_Kardex_y_asientos_una_sola_vez()
    {
        for (var round = 0; round < Rounds; round++)
        {
            var invoice = await CreateCreditInvoiceAsync();
            var before = await StateAsync(invoice);

            var responses = await Task.WhenAll(CancelAsync(invoice.Id), CancelAsync(invoice.Id));

            var after = await StateAsync(invoice);
            Log($"ronda {round}", after, responses);
            responses.Count(r => r.Status == HttpStatusCode.OK).Should().Be(1);
            responses.Single(r => r.Status != HttpStatusCode.OK).Code.Should().Be(DomainRuleViolation);
            AssertValid(after);
            after.InvoiceJournalEntries.Should().Be(before.InvoiceJournalEntries);
        }
    }

    [Fact]
    public async Task Cancel_y_devolucion_con_credito_a_CxC_simultaneas_no_se_bloquean_y_quedan_consistentes()
    {
        for (var round = 0; round < Rounds; round++)
        {
            var invoice = await CreateCreditInvoiceAsync();

            var cancelTask = CancelAsync(invoice.Id);
            var returnTask = AuthorizeReturnCreditAsync(invoice);
            var cancel = await cancelTask;
            var salesReturn = await returnTask;

            var after = await StateAsync(invoice);
            Log($"ronda {round}", after, cancel, salesReturn);
            new[] { cancel, salesReturn }.Should().OnlyContain(r => (int)r.Status < 500, "sin deadlock ni error de servidor");
            AssertValid(after);
            if (cancel.Status == HttpStatusCode.OK && salesReturn.Status != HttpStatusCode.OK)
                after.OriginalAmount.Should().Be(invoice.GrandTotal, "la devolución rechazada no acreditó la CxC");
        }
    }

    // ── sin cobros / alcance ────────────────────────────────────────────

    [Fact]
    public async Task Factura_sin_cobros_se_anula_normalmente()
    {
        var invoice = await CreateCreditInvoiceAsync();
        var before = await StateAsync(invoice);

        (await CancelAsync(invoice.Id)).Status.Should().Be(HttpStatusCode.OK);

        var after = await StateAsync(invoice);
        after.InvoiceStatus.Should().Be(SalesInvoiceStatus.Cancelled);
        after.ReceivableStatus.Should().Be("cancelled");
        after.InvoiceJournalReversals.Should().Be(before.InvoiceJournalEntries).And.BeGreaterThan(0);
        after.CancelStockMovements.Should().Be(1);
        AssertValid(after);
    }

    [Fact]
    public async Task Anulacion_desde_otra_empresa_es_fail_closed_sin_efectos()
    {
        var invoice = await CreateCreditInvoiceAsync();
        var before = await StateAsync(invoice);
        var company = _f.Factory.Services.GetRequiredService<MutableCurrentCompany>();
        var ownCompanyId = company.CompanyId;
        Response cancel;
        company.CompanyId = Guid.NewGuid();
        try
        {
            cancel = await CancelAsync(invoice.Id);
        }
        finally
        {
            company.CompanyId = ownCompanyId;
        }

        cancel.Status.Should().NotBe(HttpStatusCode.OK);
        ((int)cancel.Status).Should().BeLessThan(500);
        (await StateAsync(invoice)).Should().Be(before);
    }
}
