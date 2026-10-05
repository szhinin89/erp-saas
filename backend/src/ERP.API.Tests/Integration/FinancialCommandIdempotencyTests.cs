using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ERP.Domain.Kernel.Permissions;
using FluentAssertions;

namespace ERP.API.Tests.Integration;

/// <summary>
/// ZH-FINANCIAL-COMMAND-IDEMPOTENCY-01 — contrato HTTP real + PostgreSQL real de los tres comandos
/// financieros creadores de dinero. Una intención (ClientRequestId) produce como máximo UN efecto
/// económico ante reintento secuencial, reintento concurrente o fallo previo; los efectos se cuentan
/// físicamente en BD (documento, aplicaciones a CxP/CxC, CashMovement, JournalEntry, outbox,
/// SupplierCredit, saldos), no solo por HTTP.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class FinancialCommandIdempotencyTests
    : IClassFixture<FinancialCommandIdempotencyFixture>
{
    private const string SupplierPaymentsUrl = "/api/v1/supplier-payments";
    private const string CollectionsUrl = "/api/v1/finance/collections";
    private const int ConcurrentRequests = 5;

    private readonly FinancialCommandIdempotencyFixture _f;

    public FinancialCommandIdempotencyTests(FinancialCommandIdempotencyFixture f) => _f = f;

    // ── helpers ──────────────────────────────────────────────────────────

    private sealed record Response(HttpStatusCode Status, string Code, Guid? Id);

    private static async Task<Response> PostAsync(HttpClient client, string url, object body)
    {
        var http = await client.PostAsJsonAsync(url, body);
        var json = JsonDocument.Parse(await http.Content.ReadAsStringAsync()).RootElement;
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
        string url,
        object body
    ) =>
        Task.WhenAll(
            Enumerable.Range(0, ConcurrentRequests).Select(_ => PostAsync(client, url, body))
        );

    private object SupplierPayment(
        FinancialCommandIdempotencyFixture.Operator op,
        Guid installmentId,
        decimal amount,
        Guid clientRequestId,
        string? receipt = null
    ) =>
        new
        {
            supplierId = _f.SupplierId,
            paymentDate = _f.Today,
            totalAmount = amount,
            receiptNumber = receipt,
            methodLines = new[]
            {
                new
                {
                    paymentMethodId = _f.CashMethodId,
                    cashRegisterId = op.CashRegisterId,
                    amount,
                },
            },
            applicationLines = new[]
            {
                new { accountsPayableInstallmentId = installmentId, amountApplied = amount },
            },
            allocations = new[]
            {
                new
                {
                    methodLineIndex = 0,
                    applicationLineIndex = 0,
                    amount,
                },
            },
            clientRequestId,
        };

    private object Collection(
        Guid receivableId,
        decimal amount,
        Guid clientRequestId,
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
            clientRequestId,
        };

    private object Movement(
        decimal amount,
        Guid clientRequestId,
        Guid? reasonId = null,
        string description = "Ingreso de prueba"
    ) =>
        new
        {
            movementType = "ManualIncome",
            reasonId = reasonId ?? _f.ManualIncomeReasonId,
            amount,
            description,
            clientRequestId,
        };

    private static string MovementsUrl(FinancialCommandIdempotencyFixture.Operator op) =>
        $"/api/v1/cash-sessions/{op.CashSessionId}/movements";

    // ══ Pago a proveedor ═════════════════════════════════════════════════

    [Fact]
    public async Task Pago_mismo_ClientRequestId_secuencial_produce_un_solo_pago_con_todos_sus_efectos()
    {
        var op = await _f.CreateOperatorAsync();
        var inst = await _f.CreatePayableInstallmentAsync(300m);
        var client = _f.CreateClient(op.UserId);
        var body = SupplierPayment(op, inst, 100m, Guid.NewGuid());
        var before = await _f.CountEffectsAsync(inst, sessionId: op.CashSessionId);

        var first = await PostAsync(client, SupplierPaymentsUrl, body);
        var replay = await PostAsync(client, SupplierPaymentsUrl, body);

        first.Status.Should().Be(HttpStatusCode.Created);
        replay.Status.Should().Be(HttpStatusCode.Created);
        replay.Id.Should().Be(first.Id, "el replay devuelve el pago original");
        var after = await _f.CountEffectsAsync(inst, sessionId: op.CashSessionId);
        after.SupplierPayments.Should().Be(1);
        after.InstallmentPaid.Should().Be(100m);
        (after.CashMovements - before.CashMovements).Should().Be(1);
        after.CashMovementsAmount.Should().Be(100m);
        (after.JournalEntries - before.JournalEntries).Should().Be(1);
        (after.OutboxMessages - before.OutboxMessages).Should().Be(1);
        (after.SupplierCredits - before.SupplierCredits).Should().Be(0);
    }

    [Fact]
    public async Task Pago_mismo_ClientRequestId_concurrente_produce_un_solo_pago()
    {
        var op = await _f.CreateOperatorAsync();
        var inst = await _f.CreatePayableInstallmentAsync(300m);
        var client = _f.CreateClient(op.UserId);
        var body = SupplierPayment(op, inst, 100m, Guid.NewGuid());
        var before = await _f.CountEffectsAsync(inst, sessionId: op.CashSessionId);

        var responses = await PostConcurrentlyAsync(client, SupplierPaymentsUrl, body);

        responses.Should().OnlyContain(r => r.Status == HttpStatusCode.Created);
        responses.Select(r => r.Id).Distinct().Should().ContainSingle();
        var after = await _f.CountEffectsAsync(inst, sessionId: op.CashSessionId);
        after.SupplierPayments.Should().Be(1);
        after.InstallmentPaid.Should().Be(100m);
        (after.CashMovements - before.CashMovements).Should().Be(1);
        (after.JournalEntries - before.JournalEntries).Should().Be(1);
        (after.OutboxMessages - before.OutboxMessages).Should().Be(1);
    }

    [Fact]
    public async Task Pago_por_el_saldo_total_concurrente_responde_el_original_y_no_error_de_saldo()
    {
        // El perdedor, tras esperar el lock, encuentra la cuota ya pagada por el ganador: debe
        // responder el pago original (replay), nunca "excede el saldo".
        var op = await _f.CreateOperatorAsync();
        var inst = await _f.CreatePayableInstallmentAsync(100m);
        var client = _f.CreateClient(op.UserId);

        var responses = await PostConcurrentlyAsync(
            client,
            SupplierPaymentsUrl,
            SupplierPayment(op, inst, 100m, Guid.NewGuid())
        );

        responses.Should().OnlyContain(r => r.Status == HttpStatusCode.Created);
        responses.Select(r => r.Id).Distinct().Should().ContainSingle();
        var after = await _f.CountEffectsAsync(inst, sessionId: op.CashSessionId);
        after.SupplierPayments.Should().Be(1);
        after.InstallmentPaid.Should().Be(100m);
    }

    [Fact]
    public async Task Pago_mismo_ClientRequestId_con_payload_distinto_es_Conflict_sin_efectos_nuevos()
    {
        var op = await _f.CreateOperatorAsync();
        var inst = await _f.CreatePayableInstallmentAsync(300m);
        var client = _f.CreateClient(op.UserId);
        var key = Guid.NewGuid();
        (await PostAsync(client, SupplierPaymentsUrl, SupplierPayment(op, inst, 100m, key)))
            .Status.Should()
            .Be(HttpStatusCode.Created);
        var before = await _f.CountEffectsAsync(inst, sessionId: op.CashSessionId);

        var conflict = await PostAsync(
            client,
            SupplierPaymentsUrl,
            SupplierPayment(op, inst, 150m, key)
        );

        conflict.Status.Should().Be(HttpStatusCode.Conflict);
        conflict.Code.Should().Be("CONFLICT");
        (await _f.CountEffectsAsync(inst, sessionId: op.CashSessionId)).Should().Be(before);
    }

    [Fact]
    public async Task Pago_con_ClientRequestId_distinto_son_dos_pagos_legitimos()
    {
        var op = await _f.CreateOperatorAsync();
        var inst = await _f.CreatePayableInstallmentAsync(300m);
        var client = _f.CreateClient(op.UserId);

        var a = await PostAsync(
            client,
            SupplierPaymentsUrl,
            SupplierPayment(op, inst, 100m, Guid.NewGuid())
        );
        var b = await PostAsync(
            client,
            SupplierPaymentsUrl,
            SupplierPayment(op, inst, 100m, Guid.NewGuid())
        );

        a.Status.Should().Be(HttpStatusCode.Created);
        b.Status.Should().Be(HttpStatusCode.Created);
        b.Id.Should().NotBe(a.Id!.Value);
        var after = await _f.CountEffectsAsync(inst, sessionId: op.CashSessionId);
        after.SupplierPayments.Should().Be(2);
        after.InstallmentPaid.Should().Be(200m);
    }

    [Fact]
    public async Task Pago_rechazado_durante_la_ejecucion_no_consume_la_intencion_y_el_reintento_registra_una_vez()
    {
        // Caja con 50: pagar 100 en efectivo se rechaza dentro de la transacción (caja FOR UPDATE).
        var op = await _f.CreateOperatorAsync(openingCash: 50m);
        var inst = await _f.CreatePayableInstallmentAsync(300m);
        var client = _f.CreateClient(op.UserId);
        var body = SupplierPayment(op, inst, 100m, Guid.NewGuid());
        var before = await _f.CountEffectsAsync(inst, sessionId: op.CashSessionId);

        var rejected = await PostAsync(client, SupplierPaymentsUrl, body);
        rejected.Status.Should().NotBe(HttpStatusCode.Created);
        var afterReject = await _f.CountEffectsAsync(inst, sessionId: op.CashSessionId);
        afterReject.Should().Be(before, "un rechazo no deja ningún efecto ni consume la intención");

        (await PostAsync(client, MovementsUrl(op), Movement(100m, Guid.NewGuid())))
            .Status.Should()
            .Be(HttpStatusCode.Created);
        var retried = await PostAsync(client, SupplierPaymentsUrl, body);
        var replay = await PostAsync(client, SupplierPaymentsUrl, body);

        retried.Status.Should().Be(HttpStatusCode.Created);
        replay.Id.Should().Be(retried.Id);
        var after = await _f.CountEffectsAsync(inst, sessionId: op.CashSessionId);
        after.SupplierPayments.Should().Be(1);
        after.InstallmentPaid.Should().Be(100m);
        (after.JournalEntries - before.JournalEntries).Should().Be(1);
    }

    [Fact]
    public async Task Pago_con_comprobante_reintentado_devuelve_el_original_en_lugar_de_error_de_unicidad()
    {
        var op = await _f.CreateOperatorAsync();
        var inst = await _f.CreatePayableInstallmentAsync(300m);
        var client = _f.CreateClient(op.UserId);
        var body = SupplierPayment(
            op,
            inst,
            100m,
            Guid.NewGuid(),
            receipt: $"REC-{Guid.NewGuid():N}"[..20]
        );

        var responses = await PostConcurrentlyAsync(client, SupplierPaymentsUrl, body);

        responses.Should().OnlyContain(r => r.Status == HttpStatusCode.Created);
        responses.Select(r => r.Id).Distinct().Should().ContainSingle();
        (await _f.CountEffectsAsync(inst)).SupplierPayments.Should().Be(1);
    }

    [Fact]
    public async Task Pago_sin_ClientRequestId_se_rechaza_sin_efectos()
    {
        var op = await _f.CreateOperatorAsync();
        var inst = await _f.CreatePayableInstallmentAsync(300m);
        var client = _f.CreateClient(op.UserId);

        var response = await PostAsync(
            client,
            SupplierPaymentsUrl,
            SupplierPayment(op, inst, 100m, Guid.Empty)
        );

        response.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        response.Code.Should().Be("VALIDATION_ERROR");
        (await _f.CountEffectsAsync(inst)).SupplierPayments.Should().Be(0);
    }

    // ══ Cobro de CxC ═════════════════════════════════════════════════════

    [Fact]
    public async Task Cobro_mismo_ClientRequestId_secuencial_produce_un_solo_cobro_con_todos_sus_efectos()
    {
        var op = await _f.CreateOperatorAsync();
        var receivable = await _f.CreateReceivableAsync(300m);
        var client = _f.CreateClient(op.UserId);
        var body = Collection(receivable, 100m, Guid.NewGuid());
        var before = await _f.CountEffectsAsync(receivableId: receivable);

        var first = await PostAsync(client, CollectionsUrl, body);
        var replay = await PostAsync(client, CollectionsUrl, body);

        first.Status.Should().Be(HttpStatusCode.Created);
        replay.Status.Should().Be(HttpStatusCode.Created);
        replay.Id.Should().Be(first.Id);
        var after = await _f.CountEffectsAsync(receivableId: receivable);
        after.Collections.Should().Be(1);
        after.ReceivablePaid.Should().Be(100m);
        (after.JournalEntries - before.JournalEntries).Should().Be(1);
        (after.OutboxMessages - before.OutboxMessages).Should().Be(1);
    }

    [Fact]
    public async Task Cobro_mismo_ClientRequestId_concurrente_produce_un_solo_cobro_y_saldo_consistente()
    {
        var op = await _f.CreateOperatorAsync();
        var receivable = await _f.CreateReceivableAsync(300m);
        var client = _f.CreateClient(op.UserId);
        var body = Collection(receivable, 100m, Guid.NewGuid());
        var before = await _f.CountEffectsAsync(receivableId: receivable);

        var responses = await PostConcurrentlyAsync(client, CollectionsUrl, body);

        responses.Should().OnlyContain(r => r.Status == HttpStatusCode.Created);
        responses.Select(r => r.Id).Distinct().Should().ContainSingle();
        var after = await _f.CountEffectsAsync(receivableId: receivable);
        after.Collections.Should().Be(1);
        after.ReceivablePaid.Should().Be(100m);
        (after.JournalEntries - before.JournalEntries).Should().Be(1);
        (after.OutboxMessages - before.OutboxMessages).Should().Be(1);
    }

    [Fact]
    public async Task Cobro_por_el_saldo_total_concurrente_responde_el_original_y_no_error_de_saldo()
    {
        var op = await _f.CreateOperatorAsync();
        var receivable = await _f.CreateReceivableAsync(100m);
        var client = _f.CreateClient(op.UserId);

        var responses = await PostConcurrentlyAsync(
            client,
            CollectionsUrl,
            Collection(receivable, 100m, Guid.NewGuid())
        );

        responses.Should().OnlyContain(r => r.Status == HttpStatusCode.Created);
        responses.Select(r => r.Id).Distinct().Should().ContainSingle();
        var after = await _f.CountEffectsAsync(receivableId: receivable);
        after.Collections.Should().Be(1);
        after.ReceivablePaid.Should().Be(100m);
    }

    [Fact]
    public async Task Cobro_mismo_ClientRequestId_con_payload_distinto_es_Conflict_sin_efectos_nuevos()
    {
        var op = await _f.CreateOperatorAsync();
        var receivable = await _f.CreateReceivableAsync(300m);
        var client = _f.CreateClient(op.UserId);
        var key = Guid.NewGuid();
        (await PostAsync(client, CollectionsUrl, Collection(receivable, 100m, key)))
            .Status.Should()
            .Be(HttpStatusCode.Created);
        var before = await _f.CountEffectsAsync(receivableId: receivable);

        var conflict = await PostAsync(client, CollectionsUrl, Collection(receivable, 120m, key));

        conflict.Status.Should().Be(HttpStatusCode.Conflict);
        conflict.Code.Should().Be("CONFLICT");
        (await _f.CountEffectsAsync(receivableId: receivable)).Should().Be(before);
    }

    [Fact]
    public async Task Cobro_con_ClientRequestId_distinto_son_dos_cobros_legitimos()
    {
        var op = await _f.CreateOperatorAsync();
        var receivable = await _f.CreateReceivableAsync(300m);
        var client = _f.CreateClient(op.UserId);

        var a = await PostAsync(
            client,
            CollectionsUrl,
            Collection(receivable, 100m, Guid.NewGuid())
        );
        var b = await PostAsync(
            client,
            CollectionsUrl,
            Collection(receivable, 100m, Guid.NewGuid())
        );

        a.Status.Should().Be(HttpStatusCode.Created);
        b.Status.Should().Be(HttpStatusCode.Created);
        b.Id.Should().NotBe(a.Id!.Value);
        var after = await _f.CountEffectsAsync(receivableId: receivable);
        after.Collections.Should().Be(2);
        after.ReceivablePaid.Should().Be(200m);
    }

    [Fact]
    public async Task Cobro_rechazado_no_consume_la_intencion_y_el_reintento_registra_una_vez()
    {
        var op = await _f.CreateOperatorAsync();
        var receivable = await _f.CreateReceivableAsync(300m);
        var register = await _f.CreateCashRegisterWithoutAccountAsync();
        var client = _f.CreateClient(op.UserId);
        var body = Collection(receivable, 100m, Guid.NewGuid(), cashRegisterId: register);
        var before = await _f.CountEffectsAsync(receivableId: receivable);

        (await PostAsync(client, CollectionsUrl, body))
            .Status.Should()
            .NotBe(HttpStatusCode.Created);
        (await _f.CountEffectsAsync(receivableId: receivable)).Should().Be(before);

        await _f.SetCashRegisterAccountAsync(register);
        var retried = await PostAsync(client, CollectionsUrl, body);
        var replay = await PostAsync(client, CollectionsUrl, body);

        retried.Status.Should().Be(HttpStatusCode.Created);
        replay.Id.Should().Be(retried.Id);
        var after = await _f.CountEffectsAsync(receivableId: receivable);
        after.Collections.Should().Be(1);
        after.ReceivablePaid.Should().Be(100m);
    }

    [Fact]
    public async Task Cobro_sin_ClientRequestId_se_rechaza_sin_efectos()
    {
        var op = await _f.CreateOperatorAsync();
        var receivable = await _f.CreateReceivableAsync(300m);
        var client = _f.CreateClient(op.UserId);

        var response = await PostAsync(
            client,
            CollectionsUrl,
            Collection(receivable, 100m, Guid.Empty)
        );

        response.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        response.Code.Should().Be("VALIDATION_ERROR");
        (await _f.CountEffectsAsync(receivableId: receivable)).Collections.Should().Be(0);
    }

    // ══ Movimiento manual de caja ════════════════════════════════════════

    [Fact]
    public async Task Movimiento_mismo_ClientRequestId_secuencial_produce_un_solo_movimiento()
    {
        var op = await _f.CreateOperatorAsync();
        var client = _f.CreateClient(op.UserId);
        var body = Movement(25m, Guid.NewGuid());
        var before = await _f.CountEffectsAsync(sessionId: op.CashSessionId);

        var first = await PostAsync(client, MovementsUrl(op), body);
        var replay = await PostAsync(client, MovementsUrl(op), body);

        first.Status.Should().Be(HttpStatusCode.Created);
        replay.Status.Should().Be(HttpStatusCode.Created);
        replay.Id.Should().Be(first.Id);
        var after = await _f.CountEffectsAsync(sessionId: op.CashSessionId);
        (after.CashMovements - before.CashMovements).Should().Be(1);
        after.CashMovementsAmount.Should().Be(25m);
        (after.JournalEntries - before.JournalEntries)
            .Should()
            .Be(0, "un movimiento manual nunca postea");
        (after.OutboxMessages - before.OutboxMessages).Should().Be(0);
    }

    [Fact]
    public async Task Movimiento_mismo_ClientRequestId_concurrente_produce_un_solo_movimiento_sin_error_de_concurrencia()
    {
        var op = await _f.CreateOperatorAsync();
        var client = _f.CreateClient(op.UserId);
        var body = Movement(25m, Guid.NewGuid());

        var responses = await PostConcurrentlyAsync(client, MovementsUrl(op), body);

        responses.Should().OnlyContain(r => r.Status == HttpStatusCode.Created);
        responses.Select(r => r.Id).Distinct().Should().ContainSingle();
        var after = await _f.CountEffectsAsync(sessionId: op.CashSessionId);
        after.CashMovements.Should().Be(1);
        after.CashMovementsAmount.Should().Be(25m);
    }

    [Fact]
    public async Task Movimientos_distintos_concurrentes_en_la_misma_caja_se_serializan_y_registran_todos()
    {
        var op = await _f.CreateOperatorAsync();
        var client = _f.CreateClient(op.UserId);

        var responses = await Task.WhenAll(
            Enumerable
                .Range(0, ConcurrentRequests)
                .Select(_ => PostAsync(client, MovementsUrl(op), Movement(10m, Guid.NewGuid())))
        );

        responses.Should().OnlyContain(r => r.Status == HttpStatusCode.Created);
        var after = await _f.CountEffectsAsync(sessionId: op.CashSessionId);
        after.CashMovements.Should().Be(ConcurrentRequests);
        after.CashMovementsAmount.Should().Be(10m * ConcurrentRequests);
    }

    [Fact]
    public async Task Movimiento_mismo_ClientRequestId_con_payload_distinto_es_Conflict_sin_efectos_nuevos()
    {
        var op = await _f.CreateOperatorAsync();
        var client = _f.CreateClient(op.UserId);
        var key = Guid.NewGuid();
        (await PostAsync(client, MovementsUrl(op), Movement(25m, key)))
            .Status.Should()
            .Be(HttpStatusCode.Created);
        var before = await _f.CountEffectsAsync(sessionId: op.CashSessionId);

        var conflict = await PostAsync(client, MovementsUrl(op), Movement(30m, key));

        conflict.Status.Should().Be(HttpStatusCode.Conflict);
        conflict.Code.Should().Be("CONFLICT");
        (await _f.CountEffectsAsync(sessionId: op.CashSessionId)).Should().Be(before);
    }

    [Fact]
    public async Task Movimiento_rechazado_durante_la_ejecucion_no_consume_la_intencion()
    {
        var op = await _f.CreateOperatorAsync();
        var client = _f.CreateClient(op.UserId);
        var reason = await _f.CreateManualIncomeReasonAsync(active: false);
        var body = Movement(25m, Guid.NewGuid(), reasonId: reason);

        (await PostAsync(client, MovementsUrl(op), body))
            .Status.Should()
            .NotBe(HttpStatusCode.Created);
        (await _f.CountEffectsAsync(sessionId: op.CashSessionId)).CashMovements.Should().Be(0);

        await _f.EnableReasonAsync(reason);
        var retried = await PostAsync(client, MovementsUrl(op), body);
        var replay = await PostAsync(client, MovementsUrl(op), body);

        retried.Status.Should().Be(HttpStatusCode.Created);
        replay.Id.Should().Be(retried.Id);
        (await _f.CountEffectsAsync(sessionId: op.CashSessionId)).CashMovements.Should().Be(1);
    }

    [Fact]
    public async Task Movimiento_sin_ClientRequestId_se_rechaza_sin_efectos()
    {
        var op = await _f.CreateOperatorAsync();
        var client = _f.CreateClient(op.UserId);

        var response = await PostAsync(client, MovementsUrl(op), Movement(25m, Guid.Empty));

        response.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        response.Code.Should().Be("VALIDATION_ERROR");
        (await _f.CountEffectsAsync(sessionId: op.CashSessionId)).CashMovements.Should().Be(0);
    }

    [Fact]
    public async Task Replay_no_salta_la_autorizacion_un_usuario_sin_permiso_recibe_403()
    {
        var op = await _f.CreateOperatorAsync();
        var owner = _f.CreateClient(op.UserId);
        var body = Movement(25m, Guid.NewGuid());
        (await PostAsync(owner, MovementsUrl(op), body)).Status.Should().Be(HttpStatusCode.Created);

        var limitedUser = await _f.CreateUserWithPermissionsAsync(CajaPermissions.View);
        var limited = _f.CreateClient(limitedUser, role: "Cajero");
        var forbidden = await limited.PostAsJsonAsync(MovementsUrl(op), body);

        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _f.CountEffectsAsync(sessionId: op.CashSessionId)).CashMovements.Should().Be(1);
    }
}
