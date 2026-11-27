using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ERP.API.Tests.Support;
using ERP.Domain.Access.Entities;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Common;
using ERP.Domain.Kernel.Permissions;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.InitialLoad.Entities;
using ERP.Domain.Modules.InitialLoad.Enums;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.Integration;

/// <summary>
/// CUSTOMER-ADVANCES-V1 CA-F4 — aplicación de anticipos a CxC y su reversa con HTTP real +
/// PostgreSQL real sobre el flujo oficial de ventas (factura a crédito autorizada, cobros,
/// devolución y anulación): saldos, cuotas, concurrencia, idempotencia, TA-06, contabilidad,
/// auditoría, permisos y aislamiento. El estado se lee desde un DbContext independiente.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class CustomerAdvanceApplicationApiTests
    : IClassFixture<SalesReturnFlowFixture>,
        IClassFixture<CustomerAdvanceApplicationApiTests.Catalog>
{
    private const string Url = "/api/v1/finance/customer-advances";
    private const string Collections = "/api/v1/finance/collections";
    private const string DomainRule = "DOMAIN_RULE_VIOLATION";
    private const decimal Total = 115m;

    private readonly SalesReturnFlowFixture _f;
    private readonly Catalog _catalog;

    public CustomerAdvanceApplicationApiTests(SalesReturnFlowFixture f, Catalog catalog)
    {
        _f = f;
        _catalog = catalog;
    }

    public sealed class Catalog
    {
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public Seeded? Value { get; set; }
    }

    public sealed record Seeded(
        Guid UserId,
        Guid TransferMethod,
        Guid BankId,
        Guid BankAccount,
        Guid Advances,
        Guid Receivables,
        Guid TwoInstallmentTerm
    );

    private sealed record Response(
        HttpStatusCode Status,
        string Code,
        JsonElement Data,
        string Body
    );

    private sealed record Sale(Guid InvoiceId, Guid LineId, Guid ReceivableId, Guid[] Installments);

    private DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-5));

    // ── Aplicación ───────────────────────────────────────────────────────

    [Fact]
    public async Task Aplicacion_parcial_y_total_reduce_la_CxC_sin_dinero_ni_cobro_y_contabiliza_por_linea()
    {
        var s = await StartAsync();
        var sale = await CreateSaleAsync();
        var advance = await RegisterAdvanceAsync(s, 200m);
        var moneyBefore = await MoneyAsync();

        var partial = await ApplyAsync(advance, Line(sale, 40m));
        partial.Status.Should().Be(HttpStatusCode.OK, partial.Body);
        await AssertLedgerAsync(sale, collected: 0m, applied: 40m);

        var total = await ApplyAsync(advance, Line(sale, 75m));

        total.Status.Should().Be(HttpStatusCode.OK, total.Body);
        total.Data.GetProperty("availableAmount").GetDecimal().Should().Be(85m);
        total.Data.GetProperty("status").GetString().Should().Be("Confirmed");
        await AssertLedgerAsync(sale, collected: 0m, applied: Total);
        (await MoneyAsync())
            .Should()
            .Be(moneyBefore, "no hay Payment, caja ni cobro contabilizado");
        foreach (
            var movement in await MovementsAsync(advance, CustomerAdvanceMovementType.Application)
        )
        {
            var effects = await AccountingAsync(movement.Id);
            effects
                .Postings.Should()
                .ContainSingle()
                .Which.Should()
                .Be(("CustomerAdvanceApplied", AccountingPostingStatus.Posted));
            effects
                .Lines.Should()
                .BeEquivalentTo(
                    new[]
                    {
                        (s.Advances, movement.Amount, 0m),
                        (s.Receivables, 0m, movement.Amount),
                    }
                );
        }
        (await AuditActionsAsync(advance))
            .Should()
            .BeEquivalentTo(["Received", "Applied", "Applied"]);
    }

    [Fact]
    public async Task Una_operacion_multilinea_aplica_varias_CxC_con_una_identidad_por_linea()
    {
        var s = await StartAsync();
        var first = await CreateSaleAsync(s.TwoInstallmentTerm);
        var second = await CreateSaleAsync();
        var advance = await RegisterAdvanceAsync(s, 200m);
        var key = Guid.NewGuid();

        var response = await ApplyAsync(
            advance,
            key,
            Line(first, 20m, first.Installments[1]),
            Line(second, 30m),
            Line(first, 10m)
        );

        response.Status.Should().Be(HttpStatusCode.OK, response.Body);
        var movements = await MovementsAsync(advance, CustomerAdvanceMovementType.Application);
        movements
            .Select(m => (m.ClientRequestId, (int)m.LineNumber, m.Amount))
            .Should()
            .Equal((key, 1, 20m), (key, 2, 30m), (key, 3, 10m));
        await AssertLedgerAsync(first, 0m, 30m);
        await AssertLedgerAsync(second, 0m, 30m);
        (await InstallmentPaidAsync(first)).Should().Equal(10m, 20m);
        foreach (var movement in movements)
            (await AccountingAsync(movement.Id)).Postings.Should().ContainSingle();

        var receivable = await GetAsync($"{Url}/by-receivable/{first.ReceivableId}");
        receivable.Status.Should().Be(HttpStatusCode.OK, receivable.Body);
        receivable
            .Data.EnumerateArray()
            .Select(a => a.GetProperty("amount").GetDecimal())
            .Should()
            .Equal(20m, 10m);
        receivable
            .Data.EnumerateArray()
            .Should()
            .OnlyContain(a =>
                a.GetProperty("operationId").GetGuid() == key
                && !a.GetProperty("isReversed").GetBoolean()
            );
    }

    [Theory]
    [InlineData("overAdvance", 422, DomainRule)]
    [InlineData("overReceivable", 422, DomainRule)]
    [InlineData("cumulativeOverReceivable", 422, DomainRule)]
    [InlineData("overInstallment", 422, DomainRule)]
    [InlineData("cumulativeOverInstallment", 422, DomainRule)]
    [InlineData("foreignInstallment", 422, DomainRule)]
    [InlineData("otherCustomer", 422, DomainRule)]
    [InlineData("cancelledReceivable", 422, DomainRule)]
    [InlineData("secondLineInvalid", 422, DomainRule)]
    [InlineData("unknownReceivable", 404, "NOT_FOUND")]
    [InlineData("otherCompanyReceivable", 404, "NOT_FOUND")]
    [InlineData("unknownAdvance", 404, "NOT_FOUND")]
    [InlineData("zeroAmount", 422, "VALIDATION_ERROR")]
    [InlineData("threeDecimals", 422, "VALIDATION_ERROR")]
    [InlineData("noLines", 422, "VALIDATION_ERROR")]
    [InlineData("noClientRequestId", 422, "VALIDATION_ERROR")]
    [InlineData("closedPeriod", 422, DomainRule)]
    public async Task Una_linea_o_dato_invalido_rechaza_toda_la_operacion_sin_efectos(
        string scenario,
        int status,
        string code
    )
    {
        var s = await StartAsync();
        var sale = await CreateSaleAsync(s.TwoInstallmentTerm);
        var advance = await RegisterAdvanceAsync(s, scenario == "overAdvance" ? 10m : 500m);
        var target = advance;
        Guid? key = null;
        object[] lines = [Line(sale, 20m)];
        switch (scenario)
        {
            case "overReceivable":
                lines = [Line(sale, Total + 0.01m)];
                break;
            case "cumulativeOverReceivable":
                lines = [Line(sale, 80m), Line(sale, 40m)];
                break;
            case "overInstallment":
                lines = [Line(sale, 60m, sale.Installments[0])];
                break;
            case "cumulativeOverInstallment":
                lines =
                [
                    Line(sale, 40m, sale.Installments[0]),
                    Line(sale, 20m, sale.Installments[0]),
                ];
                break;
            case "foreignInstallment":
                lines = [Line(sale, 5m, (await CreateSaleAsync()).Installments[0])];
                break;
            case "otherCustomer":
                lines = [Line(await CreateSaleAsync(customerId: await AddCustomerAsync(s)), 5m)];
                break;
            case "cancelledReceivable":
                var cancelled = await CreateSaleAsync();
                (await CancelInvoiceAsync(cancelled)).Status.Should().Be(HttpStatusCode.OK);
                lines = [Line(cancelled, 5m)];
                break;
            case "secondLineInvalid":
                lines = [Line(sale, 20m), Line(await CreateSaleAsync(), Total + 1m)];
                break;
            case "unknownReceivable":
                lines = [Line(sale, 20m), new { receivableId = Guid.NewGuid(), amount = 1m }];
                break;
            case "otherCompanyReceivable":
                lines =
                [
                    new { receivableId = (await AddOtherCompanyAsync(s)).Receivable, amount = 1m },
                ];
                break;
            case "unknownAdvance":
                target = Guid.NewGuid();
                break;
            case "zeroAmount":
                lines = [Line(sale, 0m)];
                break;
            case "threeDecimals":
                lines = [Line(sale, 1.001m)];
                break;
            case "noLines":
                lines = [];
                break;
            case "noClientRequestId":
                key = Guid.Empty;
                break;
        }
        var before = await TotalsAsync();
        if (scenario == "closedPeriod")
            await SetCurrentPeriodStatusAsync(PeriodStatus.Closed);

        Response response;
        try
        {
            response = await PostAsync(
                $"{Url}/{target}/applications",
                new
                {
                    effectiveDate = Today,
                    lines,
                    clientRequestId = key ?? Guid.NewGuid(),
                }
            );
        }
        finally
        {
            if (scenario == "closedPeriod")
                await SetCurrentPeriodStatusAsync(PeriodStatus.Open);
        }

        ((int)response.Status).Should().Be(status, response.Body);
        if (scenario == "closedPeriod")
            response.Body.Should().NotContain("anterior a la fecha").And.NotContain("posterior al");
        response.Code.Should().Be(code, response.Body);
        (await TotalsAsync()).Should().Be(before);
    }

    [Fact]
    public async Task Se_aplica_entre_sucursales_autorizadas_y_queda_la_sucursal_activa()
    {
        var s = await StartAsync();
        var sale = await CreateSaleAsync();
        var advance = await RegisterAdvanceAsync(s, 50m);
        var branch = await AddBranchAsync(s, _currentUser);
        UseBranch(branch);

        var response = await ApplyAsync(advance, Line(sale, 25m));

        response.Status.Should().Be(HttpStatusCode.OK, response.Body);
        (await MovementsAsync(advance, CustomerAdvanceMovementType.Application))
            .Should()
            .ContainSingle()
            .Which.BranchId.Should()
            .Be(branch);

        UseBranch(await AddBranchAsync(s, authorizedUser: null));
        (await ApplyAsync(advance, Line(sale, 1m))).Status.Should().Be(HttpStatusCode.Forbidden);
        await AssertLedgerAsync(sale, 0m, 25m);
    }

    [Fact]
    public async Task Aplicar_revertir_y_consultar_exigen_permisos_independientes()
    {
        var s = await StartAsync();
        var sale = await CreateSaleAsync();
        var advance = await RegisterAdvanceAsync(s, 50m);
        var applyUser = await AddLimitedUserAsync(s, FinancePermissions.CustomerAdvancesApply);
        var reverseUser = await AddLimitedUserAsync(s, FinancePermissions.CustomerAdvancesReverse);
        var viewer = await AddLimitedUserAsync(s, FinancePermissions.View);
        var key = Guid.NewGuid();

        Impersonate(viewer);
        (await ApplyAsync(advance, Line(sale, 10m))).Status.Should().Be(HttpStatusCode.Forbidden);
        (await ReverseAsync(advance, key)).Status.Should().Be(HttpStatusCode.Forbidden);
        (await GetAsync($"{Url}/by-receivable/{sale.ReceivableId}"))
            .Status.Should()
            .Be(HttpStatusCode.OK);
        Impersonate(reverseUser);
        (await ApplyAsync(advance, Line(sale, 10m))).Status.Should().Be(HttpStatusCode.Forbidden);
        Impersonate(applyUser);
        (await ApplyAsync(advance, key, Line(sale, 10m))).Status.Should().Be(HttpStatusCode.OK);
        (await ReverseAsync(advance, key)).Status.Should().Be(HttpStatusCode.Forbidden);
        (await GetAsync($"{Url}/by-receivable/{sale.ReceivableId}"))
            .Status.Should()
            .Be(HttpStatusCode.Forbidden);
        Impersonate(reverseUser);
        (await ReverseAsync(advance, key)).Status.Should().Be(HttpStatusCode.OK);
        await AssertLedgerAsync(sale, 0m, 0m);
    }

    [Fact]
    public async Task Otra_empresa_o_tenant_no_se_alcanzan_al_aplicar_revertir_ni_consultar()
    {
        var s = await StartAsync();
        var sale = await CreateSaleAsync();
        var other = await AddOtherCompanyAsync(s);
        var before = await TotalsAsync();

        (await ApplyAsync(other.Advance, Line(sale, 1m)))
            .Status.Should()
            .Be(HttpStatusCode.NotFound);
        (await ReverseAsync(other.Advance, Guid.NewGuid()))
            .Status.Should()
            .Be(HttpStatusCode.NotFound);
        (await GetAsync($"{Url}/by-receivable/{other.Receivable}"))
            .Status.Should()
            .Be(HttpStatusCode.NotFound);
        var advance = await RegisterAdvanceAsync(s, 10m);
        var tenantReceivable = await AddForeignTenantReceivableAsync(s);
        (await ApplyAsync(advance, new { receivableId = tenantReceivable, amount = 1m }))
            .Status.Should()
            .Be(HttpStatusCode.NotFound);
        (await GetAsync($"{Url}/by-receivable/{tenantReceivable}"))
            .Status.Should()
            .Be(HttpStatusCode.NotFound);

        (await TotalsAsync()).Applications.Should().Be(before.Applications);
    }

    // ── Idempotencia ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task El_reintento_de_aplicacion_y_de_reversa_no_duplica_efectos(bool concurrent)
    {
        var s = await StartAsync();
        var first = await CreateSaleAsync();
        var second = await CreateSaleAsync();
        var advance = await RegisterAdvanceAsync(s, 100m);
        var apply = Guid.NewGuid();
        var reverse = Guid.NewGuid();

        var applied = await RepeatAsync(
            concurrent,
            () => ApplyAsync(advance, apply, Line(first, 30m), Line(second, 20m))
        );

        applied.Should().OnlyContain(r => r.Status == HttpStatusCode.OK, Bodies(applied));
        applied
            .Select(r => r.Data.GetProperty("movements").GetArrayLength())
            .Should()
            .OnlyContain(n => n == 3);
        await AssertLedgerAsync(first, 0m, 30m);
        await AssertLedgerAsync(second, 0m, 20m);
        (await AuditActionsAsync(advance)).Should().HaveCount(3);

        var reversed = await RepeatAsync(concurrent, () => ReverseAsync(advance, apply, reverse));

        reversed.Should().OnlyContain(r => r.Status == HttpStatusCode.OK, Bodies(reversed));
        reversed
            .Select(r => r.Data.GetProperty("availableAmount").GetDecimal())
            .Should()
            .OnlyContain(a => a == 100m);
        await AssertLedgerAsync(first, 0m, 0m);
        await AssertLedgerAsync(second, 0m, 0m);
        (await MovementsAsync(advance, CustomerAdvanceMovementType.ReversalOfApplication))
            .Should()
            .HaveCount(2);
        (await AuditActionsAsync(advance)).Should().HaveCount(5);
        foreach (
            var movement in await MovementsAsync(advance, CustomerAdvanceMovementType.Application)
        )
        {
            var effects = await AccountingAsync(movement.Id);
            effects.Postings.Should().ContainSingle();
            effects.Entries.Should().Be(1);
            effects.Mirrors.Should().Be(1);
        }
    }

    [Fact]
    public async Task Una_clave_no_se_reutiliza_con_otros_datos_operacion_anticipo_ni_empresa()
    {
        var s = await StartAsync();
        var sale = await CreateSaleAsync();
        var registration = Guid.NewGuid();
        var advance = await RegisterAdvanceAsync(s, 100m, registration);
        var another = await RegisterAdvanceAsync(s, 100m);
        var key = Guid.NewGuid();
        (await ApplyAsync(advance, key, Line(sale, 30m))).Status.Should().Be(HttpStatusCode.OK);
        var foreignKey = Guid.NewGuid();
        var other = await AddOtherCompanyAsync(s, foreignKey);
        var before = await TotalsAsync();

        var conflicts = new[]
        {
            await ApplyAsync(advance, key, Line(sale, 31m)),
            await ApplyAsync(another, key, Line(sale, 30m)),
            await ApplyAsync(advance, registration, Line(sale, 30m)),
            await ReverseAsync(advance, key, key),
            await ApplyAsync(advance, foreignKey, Line(sale, 5m)),
            await ReverseAsync(advance, key, foreignKey),
        };

        conflicts.Should().OnlyContain(r => r.Status == HttpStatusCode.Conflict, Bodies(conflicts));
        Bodies(conflicts).Should().NotContain(other.Advance.ToString());
        (await TotalsAsync()).Should().Be(before);
    }

    [Fact]
    public async Task Un_rechazo_no_consume_la_clave()
    {
        var s = await StartAsync();
        var sale = await CreateSaleAsync();
        var advance = await RegisterAdvanceAsync(s, 20m);
        var key = Guid.NewGuid();
        (await ApplyAsync(advance, key, Line(sale, 21m)))
            .Status.Should()
            .Be(HttpStatusCode.UnprocessableEntity);

        var retried = await ApplyAsync(advance, key, Line(sale, 20m));

        retried.Status.Should().Be(HttpStatusCode.OK, retried.Body);
        await AssertLedgerAsync(sale, 0m, 20m);
    }

    // ── Concurrencia ─────────────────────────────────────────────────────

    [Fact]
    public async Task Dos_aplicaciones_concurrentes_no_consumen_dos_veces_el_anticipo()
    {
        var s = await StartAsync();
        var first = await CreateSaleAsync();
        var second = await CreateSaleAsync();
        var advance = await RegisterAdvanceAsync(s, 50m);

        var responses = await Task.WhenAll(
            ApplyAsync(advance, Line(first, 40m)),
            ApplyAsync(advance, Line(second, 40m))
        );

        responses.Count(r => r.Status == HttpStatusCode.OK).Should().Be(1, Bodies(responses));
        responses.Single(r => r.Status != HttpStatusCode.OK).Code.Should().Be(DomainRule);
        (await AvailableAsync(advance)).Should().Be(10m);
        ((await PaidAsync(first)) + (await PaidAsync(second))).Should().Be(40m);
    }

    [Theory]
    [InlineData("advances")]
    [InlineData("collection")]
    public async Task Dos_operaciones_concurrentes_no_cobran_la_CxC_por_encima_de_su_saldo(
        string rival
    )
    {
        for (var round = 0; round < 3; round++)
        {
            var s = await StartAsync();
            var sale = await CreateSaleAsync();
            var advance = await RegisterAdvanceAsync(s, 100m);
            var other = await RegisterAdvanceAsync(s, 100m);

            var responses = await Task.WhenAll(
                ApplyAsync(advance, Line(sale, 80m)),
                rival == "advances" ? ApplyAsync(other, Line(sale, 80m)) : CollectAsync(sale, 80m)
            );

            responses
                .Count(r => r.Status is HttpStatusCode.OK or HttpStatusCode.Created)
                .Should()
                .Be(1, Bodies(responses));
            responses.Should().OnlyContain(r => (int)r.Status < 500, Bodies(responses));
            (await PaidAsync(sale)).Should().Be(80m);
            await AssertConsistentAsync(sale);
        }
    }

    [Fact]
    public async Task Aplicaciones_cruzadas_sobre_las_mismas_CxC_no_producen_deadlock()
    {
        var s = await StartAsync();
        var first = await CreateSaleAsync();
        var second = await CreateSaleAsync();
        var left = await RegisterAdvanceAsync(s, 100m);
        var right = await RegisterAdvanceAsync(s, 100m);

        for (var round = 0; round < 8; round++)
        {
            var responses = await Task.WhenAll(
                ApplyAsync(left, Line(first, 1m), Line(second, 1m)),
                ApplyAsync(right, Line(second, 1m), Line(first, 1m))
            );
            responses.Should().OnlyContain(r => r.Status == HttpStatusCode.OK, Bodies(responses));
        }

        await AssertLedgerAsync(first, 0m, 16m);
        await AssertLedgerAsync(second, 0m, 16m);
    }

    [Fact]
    public async Task Reversa_y_aplicacion_concurrentes_del_mismo_anticipo_se_serializan()
    {
        var s = await StartAsync();
        var first = await CreateSaleAsync();
        var second = await CreateSaleAsync();
        var advance = await RegisterAdvanceAsync(s, 50m);
        var key = Guid.NewGuid();
        (await ApplyAsync(advance, key, Line(first, 50m))).Status.Should().Be(HttpStatusCode.OK);

        var responses = await Task.WhenAll(
            ReverseAsync(advance, key),
            ApplyAsync(advance, Line(second, 30m))
        );

        responses[0].Status.Should().Be(HttpStatusCode.OK, responses[0].Body);
        responses.Should().OnlyContain(r => (int)r.Status < 500, Bodies(responses));
        var applied = responses[1].Status == HttpStatusCode.OK ? 30m : 0m;
        (await AvailableAsync(advance)).Should().Be(50m - applied);
        await AssertLedgerAsync(first, 0m, 0m);
        await AssertLedgerAsync(second, 0m, applied);
    }

    // ── Reversa ──────────────────────────────────────────────────────────

    [Fact]
    public async Task La_reversa_completa_espeja_el_asiento_historico_y_no_se_repite()
    {
        var s = await StartAsync();
        var first = await CreateSaleAsync(s.TwoInstallmentTerm);
        var second = await CreateSaleAsync();
        var advance = await RegisterAdvanceAsync(s, 100m);
        var key = Guid.NewGuid();
        (await ApplyAsync(advance, key, Line(first, 20m, first.Installments[1]), Line(second, 30m)))
            .Status.Should()
            .Be(HttpStatusCode.OK);
        var originals = await MovementsAsync(advance, CustomerAdvanceMovementType.Application);
        var moneyBefore = await MoneyAsync();

        var response = await ReverseAsync(advance, key);

        response.Status.Should().Be(HttpStatusCode.OK, response.Body);
        response.Data.GetProperty("availableAmount").GetDecimal().Should().Be(100m);
        await AssertLedgerAsync(first, 0m, 0m);
        await AssertLedgerAsync(second, 0m, 0m);
        (await MoneyAsync()).Should().Be(moneyBefore, "la reversa no devuelve dinero");
        var reversals = await MovementsAsync(
            advance,
            CustomerAdvanceMovementType.ReversalOfApplication
        );
        reversals
            .Select(m => (m.ReversalOfMovementId, m.Amount, m.ReceivableId, m.InstallmentId))
            .Should()
            .Equal(originals.Select(m => ((Guid?)m.Id, m.Amount, m.ReceivableId, m.InstallmentId)));
        reversals
            .Should()
            .OnlyContain(m =>
                m.Reason == "Error de aplicación"
                && m.Reference == "ACTA-1"
                && m.EffectiveDate == Today
                && m.CreatedByUserId == _currentUser
            );
        (await MovementsAsync(advance, CustomerAdvanceMovementType.Application))
            .Should()
            .BeEquivalentTo(originals, "el historial no se modifica");
        foreach (var original in originals)
        {
            var effects = await AccountingAsync(original.Id);
            effects.Entries.Should().Be(1);
            effects.Mirrors.Should().Be(1);
            effects
                .MirrorLines.Should()
                .BeEquivalentTo(effects.Lines.Select(l => (l.Account, l.Credit, l.Debit)));
        }
        (await AuditActionsAsync(advance)).Count(a => a == "ApplicationReversed").Should().Be(2);
        var byReceivable = await GetAsync($"{Url}/by-receivable/{second.ReceivableId}");
        byReceivable
            .Data.EnumerateArray()
            .Should()
            .ContainSingle()
            .Which.GetProperty("isReversed")
            .GetBoolean()
            .Should()
            .BeTrue();
        var before = await TotalsAsync();

        var twice = await ReverseAsync(advance, key);

        twice.Status.Should().Be(HttpStatusCode.UnprocessableEntity, twice.Body);
        twice.Code.Should().Be(DomainRule);
        (await TotalsAsync()).Should().Be(before);
    }

    [Theory]
    [InlineData("noReason", 422)]
    [InlineData("noReference", 422)]
    [InlineData("noClientRequestId", 422)]
    [InlineData("unknownOperation", 404)]
    [InlineData("closedPeriod", 422)]
    public async Task Una_reversa_invalida_no_produce_efectos(string scenario, int status)
    {
        var s = await StartAsync();
        var sale = await CreateSaleAsync();
        var advance = await RegisterAdvanceAsync(s, 50m);
        var key = Guid.NewGuid();
        (await ApplyAsync(advance, key, Line(sale, 20m))).Status.Should().Be(HttpStatusCode.OK);
        var before = await TotalsAsync();
        if (scenario == "closedPeriod")
            await SetCurrentPeriodStatusAsync(PeriodStatus.Closed);
        try
        {
            var response = await PostAsync(
                $"{Url}/{advance}/applications/{(scenario == "unknownOperation" ? Guid.NewGuid() : key)}/reverse",
                new
                {
                    reason = scenario == "noReason" ? " " : "Motivo",
                    reference = scenario == "noReference" ? "" : "REF",
                    clientRequestId = scenario == "noClientRequestId" ? Guid.Empty : Guid.NewGuid(),
                }
            );

            ((int)response.Status).Should().Be(status, response.Body);
            (await TotalsAsync()).Should().Be(before);
            await AssertLedgerAsync(sale, 0m, 20m);
        }
        finally
        {
            if (scenario == "closedPeriod")
                await SetCurrentPeriodStatusAsync(PeriodStatus.Open);
        }
    }

    [Fact]
    public async Task Revertir_una_aplicacion_sin_asiento_cancela_su_obligacion()
    {
        var s = await StartAsync();
        var sale = await CreateSaleAsync();
        var advance = await RegisterAdvanceAsync(s, 50m);
        var key = Guid.NewGuid();
        await SetAccountActiveAsync(s.Advances, false);
        try
        {
            (await ApplyAsync(advance, key, Line(sale, 20m))).Status.Should().Be(HttpStatusCode.OK);
            var movement = (
                await MovementsAsync(advance, CustomerAdvanceMovementType.Application)
            ).Single();
            (await AccountingAsync(movement.Id))
                .Postings.Should()
                .ContainSingle()
                .Which.Status.Should()
                .Be(AccountingPostingStatus.Failed);

            (await ReverseAsync(advance, key)).Status.Should().Be(HttpStatusCode.OK);

            var effects = await AccountingAsync(movement.Id);
            effects
                .Postings.Should()
                .ContainSingle()
                .Which.Status.Should()
                .Be(AccountingPostingStatus.Canceled);
            effects.Entries.Should().Be(0);
            effects.Mirrors.Should().Be(0);
            await AssertLedgerAsync(sale, 0m, 0m);
        }
        finally
        {
            await SetAccountActiveAsync(s.Advances, true);
        }
    }

    // ── Cronología (ADR-052 §17) ─────────────────────────────────────────

    [Fact]
    public async Task La_fecha_igual_a_la_del_anticipo_y_la_factura_se_acepta()
    {
        var s = await StartAsync();
        var date = Today.AddDays(-1);
        var sale = await CreateSaleAsync(issueDate: date);
        var advance = await RegisterAdvanceAsync(s, 50m, date: date);

        var response = await ApplyOnAsync(advance, date, Guid.NewGuid(), Line(sale, 20m));

        response.Status.Should().Be(HttpStatusCode.OK, response.Body);
        await AssertLedgerAsync(sale, 0m, 20m);
    }

    [Fact]
    public async Task La_fecha_igual_al_dia_de_negocio_se_acepta()
    {
        var s = await StartAsync();
        var sale = await CreateSaleAsync();
        var advance = await RegisterAdvanceAsync(s, 50m);

        var response = await ApplyOnAsync(advance, Today, Guid.NewGuid(), Line(sale, 20m));

        response.Status.Should().Be(HttpStatusCode.OK, response.Body);
        await AssertLedgerAsync(sale, 0m, 20m);
    }

    [Theory]
    [InlineData(0, -1, -1, "anterior a la fecha del anticipo")]
    [InlineData(-5, -1, -2, "anterior a la fecha de origen")]
    [InlineData(0, -1, 1, "posterior al d")]
    public async Task Una_fecha_incompatible_rechaza_sin_efectos_y_no_consume_la_clave(
        int advanceOffset,
        int invoiceOffset,
        int effectiveOffset,
        string message
    )
    {
        var s = await StartAsync();
        await EnsurePeriodAsync(s, Today.AddDays(-5));
        var sale = await CreateSaleAsync(issueDate: Today.AddDays(invoiceOffset));
        var advance = await RegisterAdvanceAsync(s, 50m, date: Today.AddDays(advanceOffset));
        var key = Guid.NewGuid();
        var before = await TotalsAsync();

        var rejected = await ApplyOnAsync(
            advance,
            Today.AddDays(effectiveOffset),
            key,
            Line(sale, 20m)
        );

        rejected.Status.Should().Be(HttpStatusCode.UnprocessableEntity, rejected.Body);
        rejected.Code.Should().Be(DomainRule);
        rejected.Body.Should().Contain(message);
        (await TotalsAsync()).Should().Be(before);

        var accepted = await ApplyOnAsync(advance, Today, key, Line(sale, 20m));
        accepted.Status.Should().Be(HttpStatusCode.OK, accepted.Body);
        await AssertLedgerAsync(sale, 0m, 20m);
    }

    [Fact]
    public async Task Una_sola_CxC_posterior_a_la_fecha_rechaza_toda_la_operacion()
    {
        var s = await StartAsync();
        await EnsurePeriodAsync(s, Today.AddDays(-5));
        var older = await CreateSaleAsync(issueDate: Today.AddDays(-3));
        var newer = await CreateSaleAsync();
        var advance = await RegisterAdvanceAsync(s, 50m, date: Today.AddDays(-5));
        var before = await TotalsAsync();

        var response = await ApplyOnAsync(
            advance,
            Today.AddDays(-2),
            Guid.NewGuid(),
            Line(older, 10m),
            Line(newer, 10m)
        );

        response.Status.Should().Be(HttpStatusCode.UnprocessableEntity, response.Body);
        response.Body.Should().Contain(newer.ReceivableId.ToString());
        (await TotalsAsync()).Should().Be(before);
        await AssertLedgerAsync(older, 0m, 0m);
        await AssertLedgerAsync(newer, 0m, 0m);
    }

    [Fact]
    public async Task Una_CxC_sin_fecha_de_origen_determinable_se_rechaza_sin_efectos()
    {
        var s = await StartAsync();
        var advance = await RegisterAdvanceAsync(s, 50m);
        // Saldo inicial en una empresa sin fecha de apertura (la carga oficial no lo admite): su
        // fecha de origen contable no existe.
        var receivableId = await AddUndatedOpeningReceivableAsync(s);
        var before = await TotalsAsync();

        var response = await ApplyAsync(advance, new { receivableId, amount = 10m });

        response.Status.Should().Be(HttpStatusCode.UnprocessableEntity, response.Body);
        response.Code.Should().Be(DomainRule);
        response.Body.Should().Contain("No se puede determinar la fecha de origen");
        (await TotalsAsync()).Should().Be(before);
    }

    [Fact]
    public async Task El_reintento_de_una_aplicacion_aceptada_no_vuelve_a_evaluar_las_fechas()
    {
        var s = await StartAsync();
        var sale = await CreateSaleAsync();
        var advance = await RegisterAdvanceAsync(s, 50m);
        var key = Guid.NewGuid();
        (await ApplyAsync(advance, key, Line(sale, 20m))).Status.Should().Be(HttpStatusCode.OK);
        // La factura pasa a ser posterior a la fecha aplicada: una operación nueva se rechazaría.
        await DbAsync(async db =>
        {
            var invoice = await db
                .SalesInvoices.IgnoreQueryFilters()
                .SingleAsync(i => i.Id == sale.InvoiceId);
            db.Entry(invoice).Property(i => i.IssueDate).CurrentValue = Today.AddDays(1);
        });
        var before = await TotalsAsync();

        var replay = await ApplyAsync(advance, key, Line(sale, 20m));
        var rejected = await ApplyAsync(advance, Line(sale, 5m));

        replay.Status.Should().Be(HttpStatusCode.OK, replay.Body);
        rejected.Status.Should().Be(HttpStatusCode.UnprocessableEntity, rejected.Body);
        (await TotalsAsync()).Should().Be(before);
    }

    // ── TA-06 ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Devolucion_anterior_sin_cobros_deja_la_CxC_consistente_y_admite_aplicar_y_revertir()
    {
        var s = await StartAsync();
        var sale = await CreateSaleAsync(s.TwoInstallmentTerm);
        await ReturnOneUnitAsync(sale);
        var advance = await RegisterAdvanceAsync(s, 50m);
        var key = Guid.NewGuid();

        (await ApplyAsync(advance, key, Line(sale, 20m))).Status.Should().Be(HttpStatusCode.OK);
        await AssertConsistentAsync(sale);
        (await ReverseAsync(advance, key)).Status.Should().Be(HttpStatusCode.OK);

        (await PaidAsync(sale)).Should().Be(0m);
        await AssertConsistentAsync(sale);
    }

    [Fact]
    public async Task Devolucion_posterior_a_un_cobro_desalinea_las_cuotas_y_rechaza_aplicar()
    {
        var s = await StartAsync();
        var sale = await CreateSaleAsync(s.TwoInstallmentTerm);
        (await CollectAsync(sale, 10m)).Status.Should().Be(HttpStatusCode.Created);
        await ReturnOneUnitAsync(sale);
        (await InstallmentPaidAsync(sale)).Sum().Should().NotBe(await PaidAsync(sale));
        var advance = await RegisterAdvanceAsync(s, 50m);
        var before = await TotalsAsync();
        var state = await ReceivableSnapshotAsync(sale);

        var response = await ApplyAsync(advance, Line(sale, 5m));

        response.Status.Should().Be(HttpStatusCode.UnprocessableEntity, response.Body);
        response.Code.Should().Be(DomainRule);
        (await TotalsAsync()).Should().Be(before);
        (await ReceivableSnapshotAsync(sale)).Should().Be(state);
    }

    [Fact]
    public async Task Devolucion_posterior_a_la_aplicacion_rechaza_la_reversa_completa_sin_efectos()
    {
        var s = await StartAsync();
        var affected = await CreateSaleAsync(s.TwoInstallmentTerm);
        var healthy = await CreateSaleAsync();
        var advance = await RegisterAdvanceAsync(s, 50m);
        var key = Guid.NewGuid();
        (await ApplyAsync(advance, key, Line(healthy, 10m), Line(affected, 10m)))
            .Status.Should()
            .Be(HttpStatusCode.OK);
        await ReturnOneUnitAsync(affected);
        var before = await TotalsAsync();
        var states = (
            await ReceivableSnapshotAsync(affected),
            await ReceivableSnapshotAsync(healthy)
        );

        var response = await ReverseAsync(advance, key);

        response.Status.Should().Be(HttpStatusCode.UnprocessableEntity, response.Body);
        response.Code.Should().Be(DomainRule);
        (await TotalsAsync()).Should().Be(before);
        ((await ReceivableSnapshotAsync(affected)), (await ReceivableSnapshotAsync(healthy)))
            .Should()
            .Be(states);
        (await AvailableAsync(advance)).Should().Be(30m);
    }

    [Fact]
    public async Task Una_CxC_sin_cuotas_no_admite_aplicaciones()
    {
        var s = await StartAsync();
        var sale = await CreateSaleAsync();
        await DbAsync(async db =>
            db.RemoveRange(
                await db
                    .SalesReceivableInstallments.IgnoreQueryFilters()
                    .Where(i => i.ReceivableId == sale.ReceivableId)
                    .ToListAsync()
            )
        );
        var advance = await RegisterAdvanceAsync(s, 50m);

        var response = await ApplyAsync(advance, Line(sale, 5m));

        response.Status.Should().Be(HttpStatusCode.UnprocessableEntity, response.Body);
        (await PaidAsync(sale)).Should().Be(0m);
        (await AvailableAsync(advance)).Should().Be(50m);
    }

    // ── Prueba mixta obligatoria ─────────────────────────────────────────

    public static TheoryData<bool, bool, bool> Mixed()
    {
        var data = new TheoryData<bool, bool, bool>();
        foreach (var collectionFirst in new[] { true, false })
        foreach (var explicitInstallment in new[] { true, false })
        foreach (var reverseCollectionFirst in new[] { true, false })
            data.Add(collectionFirst, explicitInstallment, reverseCollectionFirst);
        return data;
    }

    [Theory]
    [MemberData(nameof(Mixed))]
    public async Task Cobro_y_anticipo_coexisten_y_se_revierten_por_separado(
        bool collectionFirst,
        bool explicitInstallment,
        bool reverseCollectionFirst
    )
    {
        var s = await StartAsync();
        var sale = await CreateSaleAsync(s.TwoInstallmentTerm);
        sale.Installments.Should().HaveCount(2);
        var advance = await RegisterAdvanceAsync(s, 100m);
        var operation = Guid.NewGuid();
        decimal collected = 0m,
            applied = 0m;
        Guid? collectionId = null;
        // Con cuota explícita cada operación usa una cuota distinta; sin ella, el reparto oficial.
        var collectionInstallment = explicitInstallment ? sale.Installments[0] : (Guid?)null;
        var advanceInstallment = explicitInstallment ? sale.Installments[1] : (Guid?)null;

        async Task CollectStepAsync()
        {
            var response = await CollectAsync(sale, 40m, collectionInstallment);
            response.Status.Should().Be(HttpStatusCode.Created, response.Body);
            collectionId = response.Data.GetProperty("id").GetGuid();
            collected = 40m;
            await AssertLedgerAsync(sale, collected, applied);
        }
        async Task ApplyStepAsync()
        {
            var response = await ApplyAsync(
                advance,
                operation,
                Line(sale, 30m, advanceInstallment)
            );
            response.Status.Should().Be(HttpStatusCode.OK, response.Body);
            applied = 30m;
            await AssertLedgerAsync(sale, collected, applied);
        }
        async Task CancelIsRejectedAsync()
        {
            var response = await CancelInvoiceAsync(sale);
            response.Status.Should().Be(HttpStatusCode.UnprocessableEntity, response.Body);
            await AssertLedgerAsync(sale, collected, applied);
        }
        async Task ReverseCollectionStepAsync()
        {
            var response = await PostAsync(
                $"{Collections}/{collectionId}/reverse",
                new { reason = "Error" }
            );
            response.Status.Should().Be(HttpStatusCode.OK, response.Body);
            collected = 0m;
            await AssertLedgerAsync(sale, collected, applied);
        }
        async Task ReverseAdvanceStepAsync()
        {
            var response = await ReverseAsync(advance, operation);
            response.Status.Should().Be(HttpStatusCode.OK, response.Body);
            applied = 0m;
            await AssertLedgerAsync(sale, collected, applied);
        }

        if (collectionFirst)
        {
            await CollectStepAsync();
            await ApplyStepAsync();
        }
        else
        {
            await ApplyStepAsync();
            await CollectStepAsync();
        }
        await CancelIsRejectedAsync();

        if (reverseCollectionFirst)
        {
            await ReverseCollectionStepAsync();
            await CancelIsRejectedAsync(); // solo queda la aplicación vigente
            await ReverseAdvanceStepAsync();
        }
        else
        {
            await ReverseAdvanceStepAsync();
            await CancelIsRejectedAsync();
            await ReverseCollectionStepAsync();
        }

        (await AvailableAsync(advance)).Should().Be(100m);
        var application = (
            await MovementsAsync(advance, CustomerAdvanceMovementType.Application)
        ).Single();
        var accounting = await AccountingAsync(application.Id);
        accounting.Postings.Should().ContainSingle();
        (accounting.Entries, accounting.Mirrors).Should().Be((1, 1));
        var collectionAccounting = await AccountingAsync(collectionId!.Value);
        collectionAccounting.Entries.Should().Be(1);
        (await CancelInvoiceAsync(sale))
            .Status.Should()
            .Be(HttpStatusCode.OK, "sin nada vigente se puede anular");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cobro_y_anticipo_concurrentes_sobre_dos_cuotas_mantienen_las_sumas(
        bool explicitInstallment
    )
    {
        for (var round = 0; round < 3; round++)
        {
            var s = await StartAsync();
            var sale = await CreateSaleAsync(s.TwoInstallmentTerm);
            var advance = await RegisterAdvanceAsync(s, 100m);
            var key = Guid.NewGuid();

            var responses = await Task.WhenAll(
                CollectAsync(sale, 40m, explicitInstallment ? sale.Installments[0] : null),
                ApplyAsync(
                    advance,
                    key,
                    Line(sale, 30m, explicitInstallment ? sale.Installments[1] : null)
                )
            );

            responses[0].Status.Should().Be(HttpStatusCode.Created, responses[0].Body);
            responses[1].Status.Should().Be(HttpStatusCode.OK, responses[1].Body);
            await AssertLedgerAsync(sale, 40m, 30m);
            (await ReverseAsync(advance, key)).Status.Should().Be(HttpStatusCode.OK);
            await AssertLedgerAsync(sale, 40m, 0m);
        }
    }

    // ── Comprobaciones ───────────────────────────────────────────────────

    /// <summary>CA-INV-07 y suma de cuotas: nunca se asume qué cuota recibió cada importe.</summary>
    private async Task AssertLedgerAsync(Sale sale, decimal collected, decimal applied)
    {
        var (collections, applications) = await QueryAsync(async db =>
        {
            var active = (
                await db
                    .Payments.IgnoreQueryFilters()
                    .Include(p => p.Lines)
                    .Where(p =>
                        p.Status == PaymentStatus.Applied
                        && p.Lines.Any(l => l.ReceivableId == sale.ReceivableId)
                    )
                    .ToListAsync()
            )
                .SelectMany(p => p.Lines)
                .Where(l => l.ReceivableId == sale.ReceivableId)
                .Sum(l => l.AppliedAmount);
            var movements = await db
                .CustomerAdvanceMovements.IgnoreQueryFilters()
                .Where(m => m.ReceivableId == sale.ReceivableId)
                .ToListAsync();
            var net = movements.Sum(m =>
                m.MovementType == CustomerAdvanceMovementType.Application ? m.Amount : -m.Amount
            );
            return (active, net);
        });
        (collections, applications).Should().Be((collected, applied));
        (await PaidAsync(sale)).Should().Be(collected + applied);
        await AssertConsistentAsync(sale);
    }

    private async Task AssertConsistentAsync(Sale sale)
    {
        var (receivable, installments) = await QueryAsync(async db =>
            (
                await db
                    .SalesReceivables.IgnoreQueryFilters()
                    .AsNoTracking()
                    .SingleAsync(r => r.Id == sale.ReceivableId),
                await db
                    .SalesReceivableInstallments.IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(i => i.ReceivableId == sale.ReceivableId)
                    .ToListAsync()
            )
        );
        installments.Sum(i => i.PaidAmount).Should().Be(receivable.PaidAmount);
        receivable.PaidAmount.Should().BeInRange(0m, receivable.OriginalAmount);
        installments.Should().OnlyContain(i => i.PaidAmount >= 0m && i.PaidAmount <= i.Amount);
    }

    private Task<decimal> PaidAsync(Sale sale) =>
        QueryAsync(db =>
            db.SalesReceivables.IgnoreQueryFilters()
                .Where(r => r.Id == sale.ReceivableId)
                .Select(r => r.PaidAmount)
                .SingleAsync()
        );

    private Task<List<decimal>> InstallmentPaidAsync(Sale sale) =>
        QueryAsync(db =>
            db.SalesReceivableInstallments.IgnoreQueryFilters()
                .Where(i => i.ReceivableId == sale.ReceivableId)
                .OrderBy(i => i.InstallmentNumber)
                .Select(i => i.PaidAmount)
                .ToListAsync()
        );

    private Task<string> ReceivableSnapshotAsync(Sale sale) =>
        QueryAsync(async db =>
        {
            var r = await db
                .SalesReceivables.IgnoreQueryFilters()
                .AsNoTracking()
                .Include(x => x.Installments)
                .SingleAsync(x => x.Id == sale.ReceivableId);
            return $"{r.OriginalAmount}|{r.PaidAmount}|{r.Status}|"
                + string.Join(
                    ';',
                    r.Installments.OrderBy(i => i.InstallmentNumber)
                        .Select(i => $"{i.Id}:{i.Amount}:{i.PaidAmount}")
                );
        });

    private Task<decimal> AvailableAsync(Guid advance) =>
        QueryAsync(db =>
            db.CustomerAdvances.IgnoreQueryFilters()
                .Where(a => a.Id == advance)
                .Select(a => a.AvailableAmount)
                .SingleAsync()
        );

    private Task<List<CustomerAdvanceMovement>> MovementsAsync(
        Guid advance,
        CustomerAdvanceMovementType type
    ) =>
        QueryAsync(db =>
            db.CustomerAdvanceMovements.IgnoreQueryFilters()
                .AsNoTracking()
                .Where(m => m.CustomerAdvanceId == advance && m.MovementType == type)
                .OrderBy(m => m.LineNumber)
                .ToListAsync()
        );

    private Task<List<string>> AuditActionsAsync(Guid advance) =>
        QueryAsync(db =>
            db.CustomerAdvanceAudits.IgnoreQueryFilters()
                .Where(a => a.EntityId == advance)
                .Select(a => a.Action)
                .ToListAsync()
        );

    private sealed record Totals(
        int Applications,
        int Reversals,
        int Audits,
        int Postings,
        int Entries,
        int Outbox,
        decimal Available,
        decimal ReceivablePaid
    );

    private Task<Totals> TotalsAsync() =>
        QueryAsync(async db => new Totals(
            await db
                .CustomerAdvanceMovements.IgnoreQueryFilters()
                .CountAsync(m => m.MovementType == CustomerAdvanceMovementType.Application),
            await db
                .CustomerAdvanceMovements.IgnoreQueryFilters()
                .CountAsync(m =>
                    m.MovementType == CustomerAdvanceMovementType.ReversalOfApplication
                ),
            await db.CustomerAdvanceAudits.IgnoreQueryFilters().CountAsync(),
            await db
                .AccountingPostings.IgnoreQueryFilters()
                .CountAsync(p =>
                    p.FactType.StartsWith("CustomerAdvance") || p.FactType == "Reversal"
                ),
            await db
                .JournalEntries.IgnoreQueryFilters()
                .CountAsync(j => j.SourceEventType.StartsWith("CustomerAdvance")),
            await db
                .OutboxMessages.IgnoreQueryFilters()
                .CountAsync(o => o.EventName.StartsWith("CustomerAdvance")),
            await db.CustomerAdvances.IgnoreQueryFilters().SumAsync(a => a.AvailableAmount),
            await db.SalesReceivables.IgnoreQueryFilters().SumAsync(r => r.PaidAmount)
        ));

    /// <summary>Rastros de dinero que una aplicación nunca debe crear.</summary>
    private Task<(int Payments, int CashMovements, int CollectionPostings)> MoneyAsync() =>
        QueryAsync(async db =>
            (
                await db.Payments.IgnoreQueryFilters().CountAsync(),
                await db.CashMovements.IgnoreQueryFilters().CountAsync(),
                await db
                    .AccountingPostings.IgnoreQueryFilters()
                    .CountAsync(p => p.FactType == "CollectionApplied")
            )
        );

    private sealed record Accounting(
        List<(string Fact, AccountingPostingStatus Status)> Postings,
        int Entries,
        List<(Guid Account, decimal Debit, decimal Credit)> Lines,
        int Mirrors,
        List<(Guid Account, decimal Debit, decimal Credit)> MirrorLines
    );

    private Task<Accounting> AccountingAsync(Guid sourceId) =>
        QueryAsync(async db =>
        {
            var postings = await db
                .AccountingPostings.IgnoreQueryFilters()
                .Where(p => p.SourceEventId == sourceId)
                .Select(p => new { p.FactType, p.Status })
                .ToListAsync();
            var entries = await db
                .JournalEntries.IgnoreQueryFilters()
                .AsNoTracking()
                .Include(j => j.Lines)
                .Where(j => j.SourceEventId == sourceId)
                .ToListAsync();
            var ids = entries.Select(e => (Guid?)e.Id).ToList();
            var mirrors = await db
                .JournalEntries.IgnoreQueryFilters()
                .AsNoTracking()
                .Include(j => j.Lines)
                .Where(j => ids.Contains(j.OriginalJournalEntryId))
                .ToListAsync();
            return new Accounting(
                postings.Select(p => (p.FactType, p.Status)).ToList(),
                entries.Count,
                entries
                    .SelectMany(e => e.Lines)
                    .Select(l => (l.AccountId, l.Debit, l.Credit))
                    .ToList(),
                mirrors.Count,
                mirrors
                    .SelectMany(e => e.Lines)
                    .Select(l => (l.AccountId, l.Debit, l.Credit))
                    .ToList()
            );
        });

    // ── HTTP ─────────────────────────────────────────────────────────────

    private Guid _currentUser;

    private static object Line(Sale sale, decimal amount, Guid? installmentId = null) =>
        new
        {
            receivableId = sale.ReceivableId,
            installmentId,
            amount,
        };

    private static string Bodies(IEnumerable<Response> responses) =>
        string.Join('\n', responses.Select(r => r.Body));

    private static async Task<Response[]> RepeatAsync(bool concurrent, Func<Task<Response>> send)
    {
        if (concurrent)
            return await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => send()));
        return [await send(), await send()];
    }

    private async Task<Response> ReadAsync(HttpResponseMessage http)
    {
        var body = await http.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(body))
            return new Response(http.StatusCode, "", default, body);
        var json = JsonDocument.Parse(body).RootElement;
        return new Response(
            http.StatusCode,
            json.TryGetProperty("code", out var c) ? c.GetString() ?? "" : "",
            json.TryGetProperty("data", out var d) ? d : default,
            body
        );
    }

    private async Task<Response> PostAsync(string url, object body) =>
        await ReadAsync(await _f.Client.PostAsJsonAsync(url, body));

    private async Task<Response> GetAsync(string url) =>
        await ReadAsync(await _f.Client.GetAsync(url));

    private Task<Response> ApplyAsync(Guid advance, params object[] lines) =>
        ApplyAsync(advance, Guid.NewGuid(), lines);

    private Task<Response> ApplyAsync(Guid advance, Guid key, params object[] lines) =>
        PostAsync(
            $"{Url}/{advance}/applications",
            new
            {
                effectiveDate = Today,
                lines,
                clientRequestId = key,
            }
        );

    private Task<Response> ReverseAsync(Guid advance, Guid operation, Guid? key = null) =>
        PostAsync(
            $"{Url}/{advance}/applications/{operation}/reverse",
            new
            {
                reason = "Error de aplicación",
                reference = "ACTA-1",
                clientRequestId = key ?? Guid.NewGuid(),
            }
        );

    private Task<Response> CollectAsync(Sale sale, decimal amount, Guid? installmentId = null) =>
        PostAsync(
            Collections,
            new
            {
                customerId = _f.CustomerId,
                amount,
                paymentDate = Today,
                lines = new[]
                {
                    new
                    {
                        documentId = sale.ReceivableId,
                        installmentId,
                        appliedAmount = amount,
                    },
                },
                clientRequestId = Guid.NewGuid(),
            }
        );

    private Task<Response> CancelInvoiceAsync(Sale sale) =>
        PostAsync($"/api/v1/sales/{sale.InvoiceId}/cancel", new { reason = "Anulación" });

    private async Task<Guid> RegisterAdvanceAsync(
        Seeded s,
        decimal amount,
        Guid? key = null,
        DateOnly? date = null
    )
    {
        var response = await PostAsync(
            Url,
            new
            {
                customerId = _f.CustomerId,
                amount,
                advanceDate = date ?? Today,
                paymentMethodId = s.TransferMethod,
                companyBankAccountId = s.BankAccount,
                clientRequestId = key ?? Guid.NewGuid(),
            }
        );
        response.Status.Should().Be(HttpStatusCode.Created, response.Body);
        return response.Data.GetProperty("id").GetGuid();
    }

    /// <summary>Devolución autorizada de una unidad (57.50) acreditada a la CxC, por el flujo oficial.</summary>
    private async Task ReturnOneUnitAsync(Sale sale)
    {
        var draft = await PostAsync(
            "/api/v1/sales/returns",
            new
            {
                salesInvoiceId = sale.InvoiceId,
                reason = "Devolución",
                lines = new[] { new { invoiceDetailId = sale.LineId, quantity = 1m } },
            }
        );
        draft.Status.Should().Be(HttpStatusCode.Created, draft.Body);
        var amount = draft.Data.GetProperty("grandTotal").GetDecimal();
        var authorized = await PostAsync(
            $"/api/v1/sales/returns/{draft.Data.GetProperty("id").GetGuid()}/authorize",
            new { refundAllocations = new[] { new { method = "ReceivableCredit", amount } } }
        );
        authorized.Status.Should().Be(HttpStatusCode.OK, authorized.Body);
    }

    /// <summary>Factura a crédito autorizada por el flujo oficial (2 × 50 + IVA 15 % = 115).</summary>
    private async Task<Sale> CreateSaleAsync(
        Guid? paymentTerm = null,
        Guid? customerId = null,
        DateOnly? issueDate = null
    )
    {
        var create = await PostAsync(
            "/api/v1/sales",
            new
            {
                customerId = customerId ?? _f.CustomerId,
                issueDate = issueDate ?? Today.AddDays(-1),
                paymentTermId = paymentTerm ?? _f.CreditPaymentTermId,
                lines = new[]
                {
                    new
                    {
                        itemId = _f.ItemId,
                        description = "Producto anticipo",
                        quantity = 2m,
                        unitPrice = 50m,
                        vatCode = _f.VatCode,
                        warehouseId = _f.WarehouseId,
                    },
                },
                payments = new[]
                {
                    new { paymentMethodId = _f.CreditPaymentMethodId, amount = Total },
                },
            }
        );
        create.Status.Should().Be(HttpStatusCode.Created, create.Body);
        var invoiceId = create.Data.GetProperty("id").GetGuid();
        var authorize = await ReadAsync(
            await _f.Client.PostAsync($"/api/v1/sales/{invoiceId}/authorize", null)
        );
        authorize.Status.Should().Be(HttpStatusCode.OK, authorize.Body);
        var lineId = authorize.Data.GetProperty("lines")[0].GetProperty("id").GetGuid();
        return await QueryAsync(async db =>
        {
            var receivable = await db
                .SalesReceivables.IgnoreQueryFilters()
                .Include(r => r.Installments)
                .SingleAsync(r => r.InvoiceId == invoiceId);
            return new Sale(
                invoiceId,
                lineId,
                receivable.Id,
                receivable
                    .Installments.OrderBy(i => i.InstallmentNumber)
                    .Select(i => i.Id)
                    .ToArray()
            );
        });
    }

    // ── Siembra ──────────────────────────────────────────────────────────

    /// <summary>Operador propio por prueba (membresía Admin, turno abierto para vender).</summary>
    private async Task<Seeded> StartAsync()
    {
        _currentUser = await _f.CreateUserWithBranchAccessAsync();
        _f.SetActiveContext(_currentUser);
        var register = await _f.CreateCashRegisterAsync();
        var open = await PostAsync(
            "/api/v1/cash-sessions/open",
            new { cashRegisterId = register, openingAmount = 100m }
        );
        open.Status.Should().Be(HttpStatusCode.Created, open.Body);

        await _catalog.Gate.WaitAsync();
        try
        {
            if (_catalog.Value is { } existing)
                return existing;
            Seeded? seeded = null;
            await DbAsync(async db =>
            {
                var userId = _currentUser;
                var accounts = (
                    await db
                        .Accounts.IgnoreQueryFilters()
                        .Where(a => a.CompanyId == _f.CompanyId)
                        .ToListAsync()
                ).ToDictionary(a => a.Code.Value, a => a.Id);
                var transfer = PaymentMethod.Create(
                    _f.TenantId,
                    "TRANSF",
                    "Transferencia",
                    false,
                    false,
                    5,
                    userId,
                    PaymentMethodDetailType.Transfer
                );
                var bank = Bank.Create(
                    _f.TenantId,
                    "PICHINCHA",
                    "Banco Pichincha",
                    "Pichincha",
                    userId
                );
                var term = PaymentTerm.Create(
                    _f.TenantId,
                    "2C",
                    "Dos cuotas",
                    installments: 2,
                    daysBetweenInstallments: 30,
                    createdBy: userId
                );
                db.PaymentMethods.Add(transfer);
                db.Add(bank);
                db.PaymentTerms.Add(term);
                await db.SaveChangesAsync();
                var bankAccount = CompanyBankAccount.Create(
                    _f.TenantId,
                    _f.CompanyId,
                    bank.Id,
                    BankAccountType.Checking,
                    "2200000001",
                    "Cuenta anticipos",
                    accounts["1.1.02.001"],
                    userId
                );
                db.Add(bankAccount);
                seeded = new Seeded(
                    userId,
                    transfer.Id,
                    bank.Id,
                    bankAccount.Id,
                    accounts["2.1.01.002"],
                    accounts["1.1.03.001"],
                    term.Id
                );
            });
            return _catalog.Value = seeded!;
        }
        finally
        {
            _catalog.Gate.Release();
        }
    }

    private void UseBranch(Guid branchId)
    {
        _f.Client.DefaultRequestHeaders.Remove("X-Branch-Id");
        _f.Client.DefaultRequestHeaders.Add("X-Branch-Id", branchId.ToString());
    }

    private void Impersonate(Guid userId)
    {
        _f.SetActiveContext(userId);
        _f.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwtFactory.CreateSessionJwt(_f.TenantId, userId, role: "User")
        );
    }

    private async Task DbAsync(Func<ErpDbContext, Task> action)
    {
        using var scope = _f.CreateDbScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        await action(db);
        await db.SaveChangesAsync();
    }

    private async Task<T> QueryAsync<T>(Func<ErpDbContext, Task<T>> query)
    {
        using var scope = _f.CreateDbScope();
        return await query(scope.ServiceProvider.GetRequiredService<ErpDbContext>());
    }

    private async Task<Guid> AddCustomerAsync(Seeded s)
    {
        var partner = BusinessPartner.Create(
            _f.TenantId,
            "06",
            $"P{Guid.NewGuid():N}"[..12],
            1,
            "Otro cliente",
            s.UserId
        );
        await DbAsync(async db =>
        {
            db.BusinessPartners.Add(partner);
            await db.SaveChangesAsync();
            db.BusinessPartnerRoles.Add(
                BusinessPartnerRole.Create(_f.TenantId, partner.Id, RoleType.Customer, s.UserId)
            );
        });
        return partner.Id;
    }

    private async Task<Guid> AddLimitedUserAsync(Seeded s, string permission)
    {
        var user = IdentityUser.Create(
            $"lim-{Guid.NewGuid():N}",
            "Limitado",
            "CA",
            $"lim-{Guid.NewGuid():N}@example.com",
            "hash",
            s.UserId
        );
        await DbAsync(async db =>
        {
            var profile = AccessProfile.Create(
                _f.TenantId,
                _f.CompanyId,
                $"P {Guid.NewGuid():N}"[..20],
                null,
                s.UserId
            );
            db.AccessProfiles.Add(profile);
            db.IdentityUsers.Add(user);
            await db.SaveChangesAsync();
            db.AccessProfilePermissions.Add(
                AccessProfilePermission.Create(_f.TenantId, profile.Id, permission, true, s.UserId)
            );
            var membership = CompanyUserMembership.Create(
                _f.CompanyId,
                user.Id,
                "Cajero",
                profile.Id,
                s.UserId
            );
            db.CompanyUserMemberships.Add(membership);
            await db.SaveChangesAsync();
            db.CompanyUserBranches.Add(
                CompanyUserBranch.Create(
                    _f.TenantId,
                    _f.CompanyId,
                    membership.Id,
                    _f.BranchId,
                    s.UserId
                )
            );
        });
        return user.Id;
    }

    private Branch NewBranch(Guid companyId, Guid userId, bool main) =>
        Branch.Create(
            _f.TenantId,
            "Sucursal",
            "Av. 2",
            $"S{Random.Shared.Next(1000, 999999)}",
            description: null,
            reference: null,
            postalCode: null,
            phone: null,
            secondaryPhone: null,
            email: null,
            website: null,
            managerName: null,
            managerPosition: null,
            managerEmail: null,
            managerPhone: null,
            countryId: null,
            provinceId: null,
            cantonId: null,
            parishId: null,
            latitude: null,
            longitude: null,
            openingDate: null,
            internalNotes: null,
            isMainBranch: main,
            createdBy: userId,
            companyId: companyId
        );

    private async Task<Guid> AddBranchAsync(Seeded s, Guid? authorizedUser)
    {
        var branch = NewBranch(_f.CompanyId, s.UserId, main: false);
        await DbAsync(async db =>
        {
            db.Branches.Add(branch);
            await db.SaveChangesAsync();
            if (authorizedUser is not { } userId)
                return;
            var membership = await db
                .CompanyUserMemberships.IgnoreQueryFilters()
                .SingleAsync(m => m.CompanyId == _f.CompanyId && m.IdentityUserId == userId);
            db.CompanyUserBranches.Add(
                CompanyUserBranch.Create(
                    _f.TenantId,
                    _f.CompanyId,
                    membership.Id,
                    branch.Id,
                    s.UserId
                )
            );
        });
        return branch.Id;
    }

    private sealed record OtherCompany(Guid Id, Guid Receivable, Guid Advance);

    /// <summary>Empresa del mismo tenant con una CxC (saldo inicial) y un anticipo propios.</summary>
    private async Task<OtherCompany> AddOtherCompanyAsync(Seeded s, Guid? advanceKey = null)
    {
        var company = Company.CreateManaged(
            _f.TenantId,
            $"099{Guid.NewGuid():N}"[..13],
            "Empresa Ajena S.A.",
            createdBy: s.UserId
        );
        var branch = NewBranch(company.Id, s.UserId, main: true);
        var account = Account.Create(
            _f.TenantId,
            company.Id,
            AccountCode.Create("1.1.02"),
            "Banco",
            null,
            AccountType.Asset,
            AccountNature.Debit,
            allowsPosting: true,
            createdBy: s.UserId
        );
        var result = Guid.Empty;
        var advanceId = Guid.Empty;
        await DbAsync(async db =>
        {
            db.Companies.Add(company);
            await db.SaveChangesAsync();
            db.Branches.Add(branch);
            db.Accounts.Add(account);
            var batch = ImportBatch.Create(
                _f.TenantId,
                company.Id,
                ImportType.InitialReceivables,
                s.UserId
            );
            db.Add(batch);
            await db.SaveChangesAsync();
            var bank = CompanyBankAccount.Create(
                _f.TenantId,
                company.Id,
                s.BankId,
                BankAccountType.Checking,
                $"44{Random.Shared.Next(10000000, 99999999)}",
                "Cuenta ajena",
                account.Id,
                s.UserId
            );
            db.Add(bank);
            var receivable = SalesReceivable.CreateInitialBalance(
                _f.TenantId,
                company.Id,
                branch.Id,
                _f.CustomerId,
                $"AJ-{Guid.NewGuid():N}"[..12],
                Today,
                Today,
                100m,
                batch.Id,
                s.UserId
            );
            db.SalesReceivables.Add(receivable);
            await db.SaveChangesAsync();
            var advance = CustomerAdvance.Register(
                _f.TenantId,
                company.Id,
                branch.Id,
                _f.CustomerId,
                "USD",
                100m,
                Today,
                s.TransferMethod,
                bank.Id,
                null,
                s.UserId,
                new ClientRequestKey(advanceKey ?? Guid.NewGuid(), new string('B', 64))
            );
            advance.ClearDomainEvents();
            db.CustomerAdvances.Add(advance);
            (result, advanceId) = (receivable.Id, advance.Id);
        });
        return new OtherCompany(company.Id, result, advanceId);
    }

    private async Task<Guid> AddForeignTenantReceivableAsync(Seeded s)
    {
        var tenant = Tenant.Create("Ajeno", $"aj-{Guid.NewGuid():N}", s.UserId);
        var id = Guid.Empty;
        await DbAsync(async db =>
        {
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();
            var company = Company.CreateManaged(
                tenant.Id,
                $"177{Guid.NewGuid():N}"[..13],
                "Tenant Ajeno S.A.",
                createdBy: s.UserId
            );
            var partner = BusinessPartner.Create(
                tenant.Id,
                "05",
                "1710034065",
                1,
                "Cliente ajeno",
                s.UserId
            );
            db.Companies.Add(company);
            db.BusinessPartners.Add(partner);
            await db.SaveChangesAsync();
            var branch = Branch.Create(
                tenant.Id,
                "Ajena",
                "Av. 4",
                "T001",
                description: null,
                reference: null,
                postalCode: null,
                phone: null,
                secondaryPhone: null,
                email: null,
                website: null,
                managerName: null,
                managerPosition: null,
                managerEmail: null,
                managerPhone: null,
                countryId: null,
                provinceId: null,
                cantonId: null,
                parishId: null,
                latitude: null,
                longitude: null,
                openingDate: null,
                internalNotes: null,
                isMainBranch: true,
                createdBy: s.UserId,
                companyId: company.Id
            );
            var batch = ImportBatch.Create(
                tenant.Id,
                company.Id,
                ImportType.InitialReceivables,
                s.UserId
            );
            db.Branches.Add(branch);
            db.Add(batch);
            await db.SaveChangesAsync();
            var receivable = SalesReceivable.CreateInitialBalance(
                tenant.Id,
                company.Id,
                branch.Id,
                partner.Id,
                "AJ-1",
                Today,
                Today,
                100m,
                batch.Id,
                s.UserId
            );
            db.SalesReceivables.Add(receivable);
            id = receivable.Id;
        });
        return id;
    }

    private Task<Response> ApplyOnAsync(
        Guid advance,
        DateOnly effectiveDate,
        Guid key,
        params object[] lines
    ) =>
        PostAsync(
            $"{Url}/{advance}/applications",
            new
            {
                effectiveDate,
                lines,
                clientRequestId = key,
            }
        );

    /// <summary>Período abierto cuando una fecha pasada cae en el año anterior.</summary>
    private Task EnsurePeriodAsync(Seeded s, DateOnly date) =>
        DbAsync(async db =>
        {
            if (
                await db
                    .AccountingPeriods.IgnoreQueryFilters()
                    .AnyAsync(p => p.CompanyId == _f.CompanyId && p.FiscalYear == date.Year)
            )
                return;
            db.AccountingPeriods.Add(
                AccountingPeriod.Create(
                    _f.TenantId,
                    _f.CompanyId,
                    date.Year,
                    1,
                    new DateOnly(date.Year, 1, 1),
                    new DateOnly(date.Year, 12, 31),
                    s.UserId
                )
            );
        });

    private async Task<Guid> AddUndatedOpeningReceivableAsync(Seeded s)
    {
        var id = Guid.Empty;
        await DbAsync(async db =>
        {
            var batch = ImportBatch.Create(
                _f.TenantId,
                _f.CompanyId,
                ImportType.InitialReceivables,
                s.UserId
            );
            db.Add(batch);
            await db.SaveChangesAsync();
            var receivable = SalesReceivable.CreateInitialBalance(
                _f.TenantId,
                _f.CompanyId,
                _f.BranchId,
                _f.CustomerId,
                $"SF-{Guid.NewGuid():N}"[..12],
                Today,
                Today,
                100m,
                batch.Id,
                s.UserId
            );
            db.SalesReceivables.Add(receivable);
            id = receivable.Id;
        });
        return id;
    }

    private Task SetCurrentPeriodStatusAsync(PeriodStatus status) =>
        DbAsync(async db =>
        {
            var today = Today;
            var period = await db
                .AccountingPeriods.IgnoreQueryFilters()
                .SingleAsync(p =>
                    p.CompanyId == _f.CompanyId && p.StartDate <= today && p.EndDate >= today
                );
            db.Entry(period).Property(p => p.Status).CurrentValue = status;
        });

    private Task SetAccountActiveAsync(Guid accountId, bool active) =>
        DbAsync(async db =>
        {
            var account = await db
                .Accounts.IgnoreQueryFilters()
                .SingleAsync(a => a.Id == accountId);
            db.Entry(account).Property(a => a.IsActive).CurrentValue = active;
        });
}
