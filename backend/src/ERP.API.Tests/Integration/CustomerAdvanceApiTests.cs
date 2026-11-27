using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ERP.API.Tests.Support;
using ERP.Domain.Access.Entities;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Kernel.Permissions;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Enums;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.Integration;

/// <summary>
/// CUSTOMER-ADVANCES-V1 CA-F3 — contrato HTTP real + PostgreSQL real del registro y las consultas de
/// anticipos de clientes: caja, banco, referencias, permisos, aislamiento, idempotencia, atomicidad,
/// auditoría y contabilización única. Los efectos se cuentan físicamente en BD. Requiere Docker.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class CustomerAdvanceApiTests
    : IClassFixture<FinancialCommandIdempotencyFixture>,
        IClassFixture<CustomerAdvanceApiTests.Catalog>
{
    private const string Url = "/api/v1/finance/customer-advances";
    private const int ConcurrentRequests = 5;

    private readonly FinancialCommandIdempotencyFixture _f;
    private readonly Catalog _catalog;

    public CustomerAdvanceApiTests(FinancialCommandIdempotencyFixture f, Catalog catalog)
    {
        _f = f;
        _catalog = catalog;
    }

    /// <summary>Identificadores sembrados una sola vez sobre la base del fixture compartido.</summary>
    public sealed class Catalog
    {
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public Seeded? Value { get; set; }
    }

    public sealed record Seeded(
        Guid TransferMethod,
        Guid ReferencedTransferMethod,
        Guid CardMethod,
        Guid ChequeMethod,
        Guid CreditMethod,
        Guid InactiveMethod,
        Guid BankId,
        Guid BankAccount,
        Guid BankAccountingAccount,
        Guid AdvancesAccount,
        Guid UserId
    );

    private sealed record Response(
        HttpStatusCode Status,
        string Code,
        JsonElement Data,
        string Body
    )
    {
        public Guid Id => Data.GetProperty("id").GetGuid();
    }

    // ── Registro ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Efectivo_registra_anticipo_ingreso_de_caja_auditoria_y_un_solo_asiento()
    {
        var s = await SeedAsync();
        var op = await _f.CreateOperatorAsync();
        var client = _f.CreateClient(op.UserId);
        var paymentsBefore = await _f.CountCollectionsAsync();

        var response = await PostAsync(client, Cash(op, 100m));

        response.Status.Should().Be(HttpStatusCode.Created, response.Body);
        response.Data.GetProperty("status").GetString().Should().Be("Confirmed");
        response.Data.GetProperty("availableAmount").GetDecimal().Should().Be(100m);
        response.Data.GetProperty("currencyCode").GetString().Should().Be("USD");
        response.Data.GetProperty("branchId").GetGuid().Should().Be(_f.BranchId);
        var effects = await EffectsAsync(response.Id, op.CashSessionId);
        var movement = effects.Movements.Should().ContainSingle().Subject;
        movement.MovementType.Should().Be(CustomerAdvanceMovementType.Received);
        var cash = effects.CashMovements.Should().ContainSingle().Subject;
        cash.MovementType.Should().Be(CashMovementType.ManualIncome);
        cash.ReferenceType.Should().Be(CashReferenceType.CustomerAdvance);
        ((int)cash.ReferenceType).Should().Be(6);
        cash.ReferenceId.Should().Be(movement.Id);
        cash.Amount.Should().Be(100m);
        cash.ReasonId.Should().BeNull();
        movement.CashMovementId.Should().Be(cash.Id);
        movement.CashSessionId.Should().Be(op.CashSessionId);
        effects.Audits.Should().ContainSingle().Which.Should().Be(op.UserId);
        effects.Outbox.Should().Be(1);
        var posting = effects.Postings.Should().ContainSingle().Subject;
        (posting.SourceModule, posting.FactType)
            .Should()
            .Be(("Finance", "CustomerAdvanceReceived"));
        posting.Status.Should().Be(AccountingPostingStatus.Posted, posting.ErrorMessage);
        effects
            .Lines.Should()
            .BeEquivalentTo(new[] { (_f.CashAccountId, 100m, 0m), (s.AdvancesAccount, 0m, 100m) });
        effects.CashPostings.Should().Be(0, "el movimiento de caja nunca es fuente contable");
        (await _f.CountCollectionsAsync()).Should().Be(paymentsBefore, "no se crea Payment");
    }

    [Fact]
    public async Task Transferencia_registra_contra_el_banco_sin_movimiento_de_caja()
    {
        var s = await SeedAsync();
        var op = await _f.CreateOperatorAsync();
        var client = _f.CreateClient(op.UserId);

        var response = await PostAsync(
            client,
            Transfer(s, 250.55m, reference: "  TRX-9  ", reason: "Reserva")
        );

        response.Status.Should().Be(HttpStatusCode.Created, response.Body);
        response.Data.GetProperty("externalReference").GetString().Should().Be("TRX-9");
        response.Data.GetProperty("note").GetString().Should().Be("Reserva");
        var effects = await EffectsAsync(response.Id, op.CashSessionId);
        effects.CashMovements.Should().BeEmpty();
        effects.Movements.Should().ContainSingle().Which.CashMovementId.Should().BeNull();
        effects
            .Postings.Should()
            .ContainSingle()
            .Which.Status.Should()
            .Be(AccountingPostingStatus.Posted);
        effects
            .Lines.Should()
            .BeEquivalentTo(
                new[] { (s.BankAccountingAccount, 250.55m, 0m), (s.AdvancesAccount, 0m, 250.55m) }
            );
    }

    [Fact]
    public async Task Caja_de_otra_sucursal_de_la_misma_empresa_se_admite_y_conserva_la_sucursal_activa()
    {
        var s = await SeedAsync();
        var op = await _f.CreateOperatorAsync();
        var other = await AddBranchAsync(op.UserId);
        var client = _f.CreateClient(op.UserId);
        UseBranch(client, other);

        var response = await PostAsync(client, Cash(op, 10m));

        response.Status.Should().Be(HttpStatusCode.Created, response.Body);
        response.Data.GetProperty("branchId").GetGuid().Should().Be(other);
        (await EffectsAsync(response.Id, op.CashSessionId)).CashMovements.Should().ContainSingle();
    }

    // ── Rechazos ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("noDestination", 422)]
    [InlineData("twoDestinations", 422)]
    [InlineData("cashMethodToBank", 422)]
    [InlineData("transferToCash", 422)]
    [InlineData("card", 422)]
    [InlineData("cheque", 422)]
    [InlineData("credit", 422)]
    [InlineData("inactiveMethod", 422)]
    [InlineData("unknownMethod", 404)]
    [InlineData("missingReference", 422)]
    [InlineData("zeroAmount", 422)]
    [InlineData("threeDecimals", 422)]
    [InlineData("noClientRequestId", 422)]
    [InlineData("unknownCustomer", 404)]
    [InlineData("inactiveCustomer", 422)]
    [InlineData("notACustomer", 422)]
    [InlineData("otherTenantCustomer", 404)]
    [InlineData("inactiveRegister", 422)]
    [InlineData("inactiveBank", 422)]
    [InlineData("unknownBank", 404)]
    [InlineData("otherCompanyBank", 404)]
    [InlineData("otherCompanyRegister", 404)]
    [InlineData("otherTenantBank", 404)]
    [InlineData("foreignAccountingAccount", 422)]
    [InlineData("noOpenSession", 422)]
    [InlineData("anotherUsersSession", 422)]
    public async Task Una_referencia_o_dato_invalido_se_rechaza_sin_efectos(
        string scenario,
        int status
    )
    {
        var s = await SeedAsync();
        var op = await _f.CreateOperatorAsync();
        var client = _f.CreateClient(op.UserId);
        var body = Transfer(s, 10m);
        switch (scenario)
        {
            case "noDestination":
                body = body with { CompanyBankAccountId = null };
                break;
            case "twoDestinations":
                body = body with { CashRegisterId = op.CashRegisterId };
                break;
            case "cashMethodToBank":
                body = body with { PaymentMethodId = _f.CashMethodId };
                break;
            case "transferToCash":
                body = body with
                {
                    CompanyBankAccountId = null,
                    CashRegisterId = op.CashRegisterId,
                };
                break;
            case "card":
                body = body with { PaymentMethodId = s.CardMethod };
                break;
            case "cheque":
                body = body with { PaymentMethodId = s.ChequeMethod };
                break;
            case "credit":
                body = body with { PaymentMethodId = s.CreditMethod };
                break;
            case "inactiveMethod":
                body = body with { PaymentMethodId = s.InactiveMethod };
                break;
            case "unknownMethod":
                body = body with { PaymentMethodId = Guid.NewGuid() };
                break;
            case "missingReference":
                body = body with { PaymentMethodId = s.ReferencedTransferMethod, Reference = " " };
                break;
            case "zeroAmount":
                body = body with { Amount = 0m };
                break;
            case "threeDecimals":
                body = body with { Amount = 1.001m };
                break;
            case "noClientRequestId":
                body = body with { ClientRequestId = Guid.Empty };
                break;
            case "unknownCustomer":
                body = body with { CustomerId = Guid.NewGuid() };
                break;
            case "inactiveCustomer":
                body = body with { CustomerId = await AddCustomerAsync(s, active: false) };
                break;
            case "notACustomer":
                body = body with
                {
                    CustomerId = await AddCustomerAsync(s, role: RoleType.Supplier),
                };
                break;
            case "otherTenantCustomer":
                body = body with { CustomerId = (await AddForeignTenantAsync(s)).Customer };
                break;
            case "inactiveRegister":
                await _f.SetCashRegisterActiveAsync(op.CashRegisterId, active: false);
                body = Cash(op, 10m);
                break;
            case "inactiveBank":
                body = body with
                {
                    CompanyBankAccountId = await AddBankAccountAsync(s, active: false),
                };
                break;
            case "unknownBank":
                body = body with { CompanyBankAccountId = Guid.NewGuid() };
                break;
            case "otherCompanyBank":
                body = body with { CompanyBankAccountId = (await AddOtherCompanyAsync(s)).Bank };
                break;
            case "otherCompanyRegister":
                body = Cash(op, 10m) with
                {
                    CashRegisterId = (await AddOtherCompanyAsync(s)).Register,
                };
                break;
            case "otherTenantBank":
                body = body with { CompanyBankAccountId = (await AddForeignTenantAsync(s)).Bank };
                break;
            case "foreignAccountingAccount":
                body = body with
                {
                    CompanyBankAccountId = await AddBankAccountAsync(
                        s,
                        accountId: (await AddOtherCompanyAsync(s)).Account
                    ),
                };
                break;
            case "noOpenSession":
                body = Cash(op, 10m) with
                {
                    CashRegisterId = await _f.CreateCashRegisterWithoutAccountAsync(),
                };
                break;
            case "anotherUsersSession":
                body = Cash(await _f.CreateOperatorAsync(), 10m);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario));
        }
        var before = await TotalsAsync();

        var response = await PostAsync(client, body);

        ((int)response.Status).Should().Be(status, response.Body);
        (await TotalsAsync()).Should().Be(before);
    }

    [Theory]
    [InlineData("missingBranch")]
    [InlineData("unauthorizedBranch")]
    [InlineData("unknownBranch")]
    public async Task Sin_sucursal_activa_autorizada_no_se_registra(string scenario)
    {
        var s = await SeedAsync();
        var op = await _f.CreateOperatorAsync();
        var client = _f.CreateClient(op.UserId);
        client.DefaultRequestHeaders.Remove("X-Branch-Id");
        if (scenario == "unauthorizedBranch")
            client.DefaultRequestHeaders.Add(
                "X-Branch-Id",
                (await AddBranchAsync(authorizedUser: null)).ToString()
            );
        if (scenario == "unknownBranch")
            client.DefaultRequestHeaders.Add("X-Branch-Id", Guid.NewGuid().ToString());
        var before = await TotalsAsync();

        var response = await PostAsync(client, Transfer(s, 10m));

        response.Status.Should().Be(HttpStatusCode.Forbidden, response.Body);
        (await TotalsAsync()).Should().Be(before);
    }

    [Fact]
    public async Task Cada_operacion_exige_su_permiso()
    {
        var s = await SeedAsync();
        var viewer = _f.CreateClient(
            await _f.CreateUserWithPermissionsAsync(FinancePermissions.View),
            "User"
        );
        (await PostAsync(viewer, Transfer(s, 10m))).Status.Should().Be(HttpStatusCode.Forbidden);
        (await viewer.GetAsync(Url)).StatusCode.Should().Be(HttpStatusCode.OK);

        // Ni el permiso genérico de cobros ni los permisos futuros autorizan registrar.
        var others = _f.CreateClient(
            await _f.CreateUserWithPermissionsAsync(
                FinancePermissions.Create,
                FinancePermissions.CustomerAdvancesApply,
                FinancePermissions.CustomerAdvancesReverse,
                FinancePermissions.CustomerAdvancesRefund
            ),
            "User"
        );
        (await PostAsync(others, Transfer(s, 10m))).Status.Should().Be(HttpStatusCode.Forbidden);
        foreach (
            var url in new[]
            {
                Url,
                $"{Url}/{Guid.NewGuid()}",
                $"{Url}/balance?customerId={_f.CustomerId}",
            }
        )
            (await others.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.Forbidden, url);

        var creator = _f.CreateClient(
            await _f.CreateUserWithPermissionsAsync(FinancePermissions.CustomerAdvancesCreate),
            "User"
        );
        var created = await PostAsync(creator, Transfer(s, 10m));
        created.Status.Should().Be(HttpStatusCode.Created, created.Body);
        (await creator.GetAsync(Url)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Idempotencia ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Reintento_secuencial_devuelve_el_original_sin_duplicar_efectos(bool cash)
    {
        var s = await SeedAsync();
        var op = await _f.CreateOperatorAsync();
        var client = _f.CreateClient(op.UserId);
        var body = cash ? Cash(op, 40m) : Transfer(s, 40m);

        var first = await PostAsync(client, body);
        var replay = await PostAsync(client, body with { Amount = 40.00m });

        first.Status.Should().Be(HttpStatusCode.Created, first.Body);
        replay.Status.Should().Be(HttpStatusCode.Created, replay.Body);
        replay.Data.GetProperty("id").GetGuid().Should().Be(first.Id);
        replay.Data.GetProperty("originalAmount").GetDecimal().Should().Be(40m);
        replay.Data.GetProperty("movements").GetArrayLength().Should().Be(1);
        await AssertSingleEffectAsync(first.Id, op.CashSessionId, cash);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Reintentos_concurrentes_producen_un_solo_efecto(bool cash)
    {
        var s = await SeedAsync();
        var op = await _f.CreateOperatorAsync();
        var client = _f.CreateClient(op.UserId);
        var body = cash ? Cash(op, 30m) : Transfer(s, 30m);
        var before = await TotalsAsync();

        var responses = await Task.WhenAll(
            Enumerable.Range(0, ConcurrentRequests).Select(_ => PostAsync(client, body))
        );

        responses
            .Should()
            .OnlyContain(
                r => r.Status == HttpStatusCode.Created,
                string.Join('\n', responses.Select(r => r.Body))
            );
        responses.Select(r => r.Id).Distinct().Should().ContainSingle();
        (await TotalsAsync()).Advances.Should().Be(before.Advances + 1);
        await AssertSingleEffectAsync(responses[0].Id, op.CashSessionId, cash);
    }

    [Theory]
    [InlineData("amount")]
    [InlineData("reference")]
    [InlineData("reason")]
    [InlineData("destination")]
    public async Task La_misma_clave_con_otros_datos_responde_409_sin_efectos(string change)
    {
        var s = await SeedAsync();
        var op = await _f.CreateOperatorAsync();
        var client = _f.CreateClient(op.UserId);
        var body = Transfer(s, 20m, reference: "A", reason: "B");
        var first = await PostAsync(client, body);
        var changed = change switch
        {
            "amount" => body with { Amount = 21m },
            "reference" => body with { Reference = "Z" },
            "reason" => body with { Reason = "Z" },
            _ => Cash(op, 20m) with
            {
                ClientRequestId = body.ClientRequestId,
                Reference = "A",
                Reason = "B",
            },
        };
        var before = await TotalsAsync();

        var conflict = await PostAsync(client, changed);

        conflict.Status.Should().Be(HttpStatusCode.Conflict, conflict.Body);
        (await TotalsAsync()).Should().Be(before);
        await AssertSingleEffectAsync(first.Id, op.CashSessionId, cash: false);
    }

    [Fact]
    public async Task Una_clave_usada_en_otra_empresa_del_tenant_responde_409_sin_revelar_nada()
    {
        var s = await SeedAsync();
        var op = await _f.CreateOperatorAsync();
        var client = _f.CreateClient(op.UserId);
        var key = Guid.NewGuid();
        var foreignAdvance = await AddAdvanceInOtherCompanyAsync(s, key, amount: 777m);
        var before = await TotalsAsync();

        var response = await PostAsync(client, Cash(op, 15m) with { ClientRequestId = key });

        response.Status.Should().Be(HttpStatusCode.Conflict, response.Body);
        response.Body.Should().NotContain(foreignAdvance.ToString()).And.NotContain("777");
        (await TotalsAsync()).Should().Be(before);
    }

    [Fact]
    public async Task Un_rechazo_no_consume_la_clave()
    {
        await SeedAsync();
        var op = await _f.CreateOperatorAsync();
        var client = _f.CreateClient(op.UserId);
        var body = Cash(op, 12m);
        await _f.SetCashRegisterActiveAsync(op.CashRegisterId, active: false);
        (await PostAsync(client, body)).Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        await _f.SetCashRegisterActiveAsync(op.CashRegisterId, active: true);

        var retried = await PostAsync(client, body);

        retried.Status.Should().Be(HttpStatusCode.Created, retried.Body);
        await AssertSingleEffectAsync(retried.Id, op.CashSessionId, cash: true);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Una_fecha_futura_se_rechaza_sin_efectos_y_la_misma_clave_registra_hoy(
        bool cash
    )
    {
        var s = await SeedAsync();
        var op = await _f.CreateOperatorAsync();
        var client = _f.CreateClient(op.UserId);
        var body = cash ? Cash(op, 25m) : Transfer(s, 25m);
        var before = await TotalsAsync();

        var future = await PostAsync(client, body with { AdvanceDate = _f.Today.AddDays(1) });

        future.Status.Should().Be(HttpStatusCode.UnprocessableEntity, future.Body);
        future.Body.Should().Contain("es posterior al d");
        (await TotalsAsync()).Should().Be(before);

        var accepted = await PostAsync(client, body);
        var replay = await PostAsync(client, body);

        accepted.Status.Should().Be(HttpStatusCode.Created, accepted.Body);
        accepted.Data.GetProperty("advanceDate").GetString().Should().Be($"{_f.Today:yyyy-MM-dd}");
        replay.Status.Should().Be(HttpStatusCode.Created, replay.Body);
        replay.Id.Should().Be(accepted.Id);
        await AssertSingleEffectAsync(accepted.Id, op.CashSessionId, cash);
    }

    // ── Atomicidad y contrato contable ───────────────────────────────────

    [Fact]
    public async Task Un_periodo_cerrado_revierte_anticipo_caja_auditoria_obligacion_y_outbox()
    {
        await SeedAsync();
        var op = await _f.CreateOperatorAsync();
        var client = _f.CreateClient(op.UserId);
        var date = new DateOnly(2019, 3, 10);
        await DbAsync(async db =>
        {
            if (
                await db
                    .AccountingPeriods.IgnoreQueryFilters()
                    .AnyAsync(p => p.CompanyId == _f.CompanyId && p.FiscalYear == date.Year)
            )
                return;
            var period = AccountingPeriod.Create(
                _f.TenantId,
                _f.CompanyId,
                date.Year,
                1,
                new DateOnly(date.Year, 1, 1),
                new DateOnly(date.Year, 12, 31),
                op.UserId
            );
            period.Close(op.UserId, JournalEntryClosureReadiness.Ready);
            db.AccountingPeriods.Add(period);
        });
        var before = await TotalsAsync();

        var response = await PostAsync(client, Cash(op, 55m) with { AdvanceDate = date });

        response.Status.Should().Be(HttpStatusCode.UnprocessableEntity, response.Body);
        response.Body.Should().Contain("ACCOUNTING_PERIOD_CLOSED");
        (await TotalsAsync()).Should().Be(before);
    }

    [Theory]
    [InlineData("registerWithoutAccount")]
    [InlineData("inactiveBankAccountingAccount")]
    public async Task Configuracion_contable_invalida_deja_la_obligacion_Failed_y_el_anticipo_vigente(
        string scenario
    )
    {
        var s = await SeedAsync();
        var op = await _f.CreateOperatorAsync();
        var client = _f.CreateClient(op.UserId);
        RegisterBody body;
        if (scenario == "registerWithoutAccount")
        {
            await DbAsync(async db =>
            {
                var register = await db
                    .CashRegisters.IgnoreQueryFilters()
                    .SingleAsync(r => r.Id == op.CashRegisterId);
                db.Entry(register).Property(r => r.AccountingAccountId).CurrentValue = null;
            });
            body = Cash(op, 8m);
        }
        else
        {
            var account = await AddAccountAsync(_f.CompanyId, s.UserId, active: false);
            body = Transfer(s, 8m) with
            {
                CompanyBankAccountId = await AddBankAccountAsync(s, accountId: account),
            };
        }

        var response = await PostAsync(client, body);

        response.Status.Should().Be(HttpStatusCode.Created, response.Body);
        var effects = await EffectsAsync(response.Id, op.CashSessionId);
        effects
            .Postings.Should()
            .ContainSingle()
            .Which.Status.Should()
            .Be(AccountingPostingStatus.Failed);
        effects.Lines.Should().BeEmpty();
    }

    [Fact]
    public async Task Sin_modo_contable_decidido_no_queda_ningun_efecto()
    {
        await SeedAsync();
        var op = await _f.CreateOperatorAsync();
        var client = _f.CreateClient(op.UserId);
        List<AccountingOperationalPolicy> policies = [];
        await DbAsync(async db =>
        {
            policies = await db
                .AccountingOperationalPolicies.IgnoreQueryFilters()
                .Where(p => p.CompanyId == _f.CompanyId)
                .ToListAsync();
            db.AccountingOperationalPolicies.RemoveRange(policies);
        });
        try
        {
            var before = await TotalsAsync();

            var response = await PostAsync(client, Cash(op, 5m));

            response.Status.Should().Be(HttpStatusCode.UnprocessableEntity, response.Body);
            (await TotalsAsync()).Should().Be(before);
        }
        finally
        {
            await DbAsync(db =>
            {
                db.AccountingOperationalPolicies.AddRange(policies);
                return Task.CompletedTask;
            });
        }
    }

    // ── Consultas ────────────────────────────────────────────────────────

    [Fact]
    public async Task Saldo_listado_y_detalle_son_de_la_empresa_y_no_dependen_de_la_sucursal()
    {
        var s = await SeedAsync();
        var op = await _f.CreateOperatorAsync();
        var other = await AddBranchAsync(op.UserId);
        var customer = await AddCustomerAsync(s);
        var client = _f.CreateClient(op.UserId);
        var first = await PostAsync(client, Transfer(s, 100m) with { CustomerId = customer });
        UseBranch(client, other);
        var second = await PostAsync(client, Cash(op, 25.50m) with { CustomerId = customer });
        second.Status.Should().Be(HttpStatusCode.Created, second.Body);
        await AddAdvanceInOtherCompanyAsync(s, Guid.NewGuid(), amount: 900m, customer);

        foreach (var branch in new[] { _f.BranchId, other })
        {
            UseBranch(client, branch);
            var balance = await GetAsync(client, $"{Url}/balance?customerId={customer}");
            balance.Status.Should().Be(HttpStatusCode.OK, balance.Body);
            balance.Data.GetProperty("availableAmount").GetDecimal().Should().Be(125.50m);
            balance.Data.GetProperty("currencyCode").GetString().Should().Be("USD");

            var list = await GetAsync(client, $"{Url}?customerId={customer}");
            list.Data.GetProperty("total").GetInt32().Should().Be(2);
        }

        var filtered = await GetAsync(
            client,
            $"{Url}?customerId={customer}&branchId={other}&status=Confirmed"
        );
        filtered
            .Data.GetProperty("items")
            .EnumerateArray()
            .Should()
            .ContainSingle()
            .Which.GetProperty("id")
            .GetGuid()
            .Should()
            .Be(second.Id);
        var paged = await GetAsync(client, $"{Url}?customerId={customer}&page=2&pageSize=1");
        paged.Data.GetProperty("total").GetInt32().Should().Be(2);
        paged.Data.GetProperty("items").GetArrayLength().Should().Be(1);
        (await GetAsync(client, $"{Url}?customerId={customer}&status=Cancelled"))
            .Data.GetProperty("total")
            .GetInt32()
            .Should()
            .Be(0);

        var detail = await GetAsync(client, $"{Url}/{first.Id}");
        detail.Status.Should().Be(HttpStatusCode.OK, detail.Body);
        detail.Data.GetProperty("customerName").GetString().Should().NotBeNullOrWhiteSpace();
        var movement = detail
            .Data.GetProperty("movements")
            .EnumerateArray()
            .Should()
            .ContainSingle()
            .Subject;
        movement.GetProperty("movementType").GetString().Should().Be("Received");
        movement.GetProperty("createdByUserId").GetGuid().Should().Be(op.UserId);
        detail.Body.Should().NotContain("payloadHash", "la huella no se expone");

        var empty = await GetAsync(client, $"{Url}/balance?customerId={await AddCustomerAsync(s)}");
        empty.Data.GetProperty("availableAmount").GetDecimal().Should().Be(0m);
    }

    [Fact]
    public async Task Las_consultas_no_exponen_anticipos_de_otra_empresa()
    {
        var s = await SeedAsync();
        var op = await _f.CreateOperatorAsync();
        var customer = await AddCustomerAsync(s);
        var foreign = await AddAdvanceInOtherCompanyAsync(
            s,
            Guid.NewGuid(),
            amount: 900m,
            customer
        );
        var client = _f.CreateClient(op.UserId);

        (await GetAsync(client, $"{Url}/{foreign}")).Status.Should().Be(HttpStatusCode.NotFound);
        (await GetAsync(client, $"{Url}?customerId={customer}"))
            .Data.GetProperty("total")
            .GetInt32()
            .Should()
            .Be(0);
        (await GetAsync(client, $"{Url}/balance?customerId={customer}"))
            .Data.GetProperty("availableAmount")
            .GetDecimal()
            .Should()
            .Be(0m);
        var foreignTenant = await AddForeignTenantAsync(s);
        (await GetAsync(client, $"{Url}/balance?customerId={foreignTenant.Customer}"))
            .Status.Should()
            .Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?pageSize=101")]
    [InlineData("?status=9")]
    [InlineData("/balance")]
    public async Task Filtros_fuera_de_rango_responden_422(string query)
    {
        var op = await _f.CreateOperatorAsync();

        var response = await GetAsync(_f.CreateClient(op.UserId), Url + query);

        response.Status.Should().Be(HttpStatusCode.UnprocessableEntity, response.Body);
    }

    // ── Infraestructura del test ─────────────────────────────────────────

    private sealed record RegisterBody(
        Guid CustomerId,
        decimal Amount,
        DateOnly AdvanceDate,
        Guid PaymentMethodId,
        Guid? CompanyBankAccountId,
        Guid? CashRegisterId,
        string? Reference,
        string? Reason,
        Guid ClientRequestId
    );

    private RegisterBody Cash(FinancialCommandIdempotencyFixture.Operator op, decimal amount) =>
        new(
            _f.CustomerId,
            amount,
            _f.Today,
            _f.CashMethodId,
            null,
            op.CashRegisterId,
            null,
            null,
            Guid.NewGuid()
        );

    private RegisterBody Transfer(
        Seeded s,
        decimal amount,
        string? reference = null,
        string? reason = null
    ) =>
        new(
            _f.CustomerId,
            amount,
            _f.Today,
            s.TransferMethod,
            s.BankAccount,
            null,
            reference,
            reason,
            Guid.NewGuid()
        );

    private static async Task<Response> ReadAsync(HttpResponseMessage http)
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

    private static async Task<Response> PostAsync(HttpClient client, RegisterBody body) =>
        await ReadAsync(await client.PostAsJsonAsync(Url, body));

    private static async Task<Response> GetAsync(HttpClient client, string url) =>
        await ReadAsync(await client.GetAsync(url));

    private static void UseBranch(HttpClient client, Guid branchId)
    {
        client.DefaultRequestHeaders.Remove("X-Branch-Id");
        client.DefaultRequestHeaders.Add("X-Branch-Id", branchId.ToString());
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

    private sealed record Totals(
        int Advances,
        int Movements,
        int Audits,
        int CashMovements,
        int Postings,
        int Entries,
        int Outbox
    );

    private Task<Totals> TotalsAsync() =>
        QueryAsync(async db => new Totals(
            await db.CustomerAdvances.IgnoreQueryFilters().CountAsync(),
            await db.CustomerAdvanceMovements.IgnoreQueryFilters().CountAsync(),
            await db.CustomerAdvanceAudits.IgnoreQueryFilters().CountAsync(),
            await db
                .CashMovements.IgnoreQueryFilters()
                .CountAsync(m => m.MovementType != CashMovementType.Opening),
            await db.AccountingPostings.IgnoreQueryFilters().CountAsync(),
            await db.JournalEntries.IgnoreQueryFilters().CountAsync(),
            await db
                .OutboxMessages.IgnoreQueryFilters()
                .CountAsync(o => o.EventName.StartsWith("CustomerAdvance"))
        ));

    private sealed record Effects(
        List<CustomerAdvanceMovement> Movements,
        List<CashMovement> CashMovements,
        List<Guid> Audits,
        List<AccountingPosting> Postings,
        List<(Guid Account, decimal Debit, decimal Credit)> Lines,
        int CashPostings,
        int Outbox
    );

    private Task<Effects> EffectsAsync(Guid advanceId, Guid sessionId) =>
        QueryAsync(async db =>
        {
            var movements = await db
                .CustomerAdvanceMovements.IgnoreQueryFilters()
                .AsNoTracking()
                .Where(m => m.CustomerAdvanceId == advanceId)
                .ToListAsync();
            var ids = movements.Select(m => m.Id).ToList();
            var cash = await db
                .CashMovements.IgnoreQueryFilters()
                .AsNoTracking()
                .Where(m =>
                    m.CashSessionId == sessionId && m.MovementType != CashMovementType.Opening
                )
                .ToListAsync();
            var cashIds = cash.Select(m => m.Id).ToList();
            var entries = await db
                .JournalEntries.IgnoreQueryFilters()
                .AsNoTracking()
                .Include(j => j.Lines)
                .Where(j => ids.Contains(j.SourceEventId))
                .ToListAsync();
            var payloads = await db
                .OutboxMessages.IgnoreQueryFilters()
                .Where(o => o.EventName.StartsWith("CustomerAdvance"))
                .Select(o => o.Payload)
                .ToListAsync();
            return new Effects(
                movements,
                cash,
                await db
                    .CustomerAdvanceAudits.IgnoreQueryFilters()
                    .Where(a => a.EntityId == advanceId)
                    .Select(a => a.UserId)
                    .ToListAsync(),
                await db
                    .AccountingPostings.IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(p => ids.Contains(p.SourceEventId))
                    .ToListAsync(),
                entries
                    .SelectMany(e => e.Lines)
                    .Select(l => (l.AccountId, l.Debit, l.Credit))
                    .ToList(),
                await db
                    .AccountingPostings.IgnoreQueryFilters()
                    .CountAsync(p => p.SourceModule == "Caja" && cashIds.Contains(p.SourceEventId)),
                payloads.Count(p => p.ToString()!.Contains(advanceId.ToString()))
            );
        });

    private async Task AssertSingleEffectAsync(Guid advanceId, Guid sessionId, bool cash)
    {
        var effects = await EffectsAsync(advanceId, sessionId);
        effects.Movements.Should().ContainSingle();
        effects.Audits.Should().ContainSingle();
        effects.CashMovements.Should().HaveCount(cash ? 1 : 0);
        effects
            .Postings.Should()
            .ContainSingle()
            .Which.Status.Should()
            .Be(AccountingPostingStatus.Posted);
        effects.Lines.Should().HaveCount(2);
        effects.Outbox.Should().Be(1);
    }

    private async Task<Seeded> SeedAsync()
    {
        await _catalog.Gate.WaitAsync();
        try
        {
            if (_catalog.Value is { } existing)
                return existing;
            var userId = (await _f.CreateOperatorAsync()).UserId;
            var bankAccounting = await AddAccountAsync(_f.CompanyId, userId);
            Seeded? seeded = null;
            await DbAsync(async db =>
            {
                PaymentMethod Method(
                    string code,
                    PaymentMethodDetailType detail,
                    bool credit = false,
                    bool reference = false
                ) =>
                    PaymentMethod.Create(
                        _f.TenantId,
                        code,
                        code,
                        reference,
                        credit,
                        5,
                        userId,
                        detail
                    );
                var transfer = Method("TRANSF", PaymentMethodDetailType.Transfer);
                var referenced = Method(
                    "TRANSFREF",
                    PaymentMethodDetailType.Transfer,
                    reference: true
                );
                var card = Method("TARJ", PaymentMethodDetailType.Card);
                var cheque = Method("CHEQ", PaymentMethodDetailType.Check);
                var credit = Method("CRED", PaymentMethodDetailType.None, credit: true);
                var inactive = Method("TRANSFOFF", PaymentMethodDetailType.Transfer);
                inactive.Disable(userId);
                db.PaymentMethods.AddRange(transfer, referenced, card, cheque, credit, inactive);
                var bank = Bank.Create(
                    _f.TenantId,
                    "PICHINCHA",
                    "Banco Pichincha",
                    "Pichincha",
                    userId
                );
                db.Add(bank);
                await db.SaveChangesAsync();
                var bankAccount = CompanyBankAccount.Create(
                    _f.TenantId,
                    _f.CompanyId,
                    bank.Id,
                    BankAccountType.Checking,
                    "2200000001",
                    "Cuenta anticipos",
                    bankAccounting,
                    userId
                );
                db.Add(bankAccount);
                var advances = (
                    await db
                        .Accounts.IgnoreQueryFilters()
                        .Where(a => a.CompanyId == _f.CompanyId)
                        .ToListAsync()
                ).Single(a => a.Code.Value == "2.1.01.002");
                seeded = new Seeded(
                    transfer.Id,
                    referenced.Id,
                    card.Id,
                    cheque.Id,
                    credit.Id,
                    inactive.Id,
                    bank.Id,
                    bankAccount.Id,
                    bankAccounting,
                    advances.Id,
                    userId
                );
            });
            return _catalog.Value = seeded!;
        }
        finally
        {
            _catalog.Gate.Release();
        }
    }

    private async Task<Guid> AddAccountAsync(Guid companyId, Guid userId, bool active = true)
    {
        var account = Account.Create(
            _f.TenantId,
            companyId,
            AccountCode.Create($"1.9.{Random.Shared.Next(1, 999999)}"),
            "Banco de prueba",
            null,
            AccountType.Asset,
            AccountNature.Debit,
            allowsPosting: true,
            createdBy: userId
        );
        await DbAsync(db =>
        {
            db.Accounts.Add(account);
            if (!active)
                db.Entry(account).Property(a => a.IsActive).CurrentValue = false;
            return Task.CompletedTask;
        });
        return account.Id;
    }

    private async Task<Guid> AddBankAccountAsync(
        Seeded s,
        bool active = true,
        Guid? accountId = null
    )
    {
        var account = CompanyBankAccount.Create(
            _f.TenantId,
            _f.CompanyId,
            s.BankId,
            BankAccountType.Checking,
            $"33{Random.Shared.Next(10000000, 99999999)}",
            "Otra cuenta",
            accountId ?? s.BankAccountingAccount,
            s.UserId
        );
        await DbAsync(db =>
        {
            db.Add(account);
            if (!active)
                db.Entry(account).Property(a => a.IsActive).CurrentValue = false;
            return Task.CompletedTask;
        });
        return account.Id;
    }

    private async Task<Guid> AddCustomerAsync(
        Seeded s,
        bool active = true,
        RoleType role = RoleType.Customer
    )
    {
        var partner = BusinessPartner.Create(
            _f.TenantId,
            "06",
            $"P{Guid.NewGuid():N}"[..12],
            1,
            "Cliente anticipos",
            s.UserId
        );
        await DbAsync(async db =>
        {
            db.BusinessPartners.Add(partner);
            await db.SaveChangesAsync();
            db.BusinessPartnerRoles.Add(
                BusinessPartnerRole.Create(_f.TenantId, partner.Id, role, s.UserId)
            );
            if (!active)
                db.Entry(partner).Property(p => p.IsActive).CurrentValue = false;
        });
        return partner.Id;
    }

    private async Task<Guid> AddBranchAsync(Guid? authorizedUser)
    {
        var s = await SeedAsync();
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

    private sealed record OtherCompany(
        Guid Id,
        Guid Branch,
        Guid Register,
        Guid Bank,
        Guid Account
    );

    private async Task<OtherCompany> AddOtherCompanyAsync(Seeded s)
    {
        var company = Company.CreateManaged(
            _f.TenantId,
            $"099{Guid.NewGuid():N}"[..13],
            "Empresa Ajena S.A.",
            createdBy: s.UserId
        );
        await DbAsync(db =>
        {
            db.Companies.Add(company);
            return Task.CompletedTask;
        });
        var account = await AddAccountAsync(company.Id, s.UserId);
        var branch = NewBranch(company.Id, s.UserId, main: true);
        var register = CashRegister.Create(
            _f.TenantId,
            company.Id,
            branch.Id,
            "AJ1",
            "Caja ajena",
            s.UserId
        );
        var bank = CompanyBankAccount.Create(
            _f.TenantId,
            company.Id,
            s.BankId,
            BankAccountType.Checking,
            $"44{Random.Shared.Next(10000000, 99999999)}",
            "Cuenta ajena",
            account,
            s.UserId
        );
        await DbAsync(async db =>
        {
            db.Branches.Add(branch);
            await db.SaveChangesAsync();
            db.CashRegisters.Add(register);
            db.Add(bank);
        });
        return new OtherCompany(company.Id, branch.Id, register.Id, bank.Id, account);
    }

    /// <summary>Anticipo persistido directamente en otra empresa del mismo tenant.</summary>
    private async Task<Guid> AddAdvanceInOtherCompanyAsync(
        Seeded s,
        Guid key,
        decimal amount,
        Guid? customerId = null
    )
    {
        var other = await AddOtherCompanyAsync(s);
        var advance = CustomerAdvance.Register(
            _f.TenantId,
            other.Id,
            other.Branch,
            customerId ?? _f.CustomerId,
            "USD",
            amount,
            _f.Today,
            s.TransferMethod,
            other.Bank,
            null,
            s.UserId,
            new ERP.Domain.Common.ClientRequestKey(key, new string('B', 64))
        );
        // Siembra sin eventos: la empresa ajena no opera contabilidad en este escenario.
        advance.ClearDomainEvents();
        await DbAsync(db =>
        {
            db.CustomerAdvances.Add(advance);
            return Task.CompletedTask;
        });
        return advance.Id;
    }

    private async Task<(Guid Customer, Guid Bank)> AddForeignTenantAsync(Seeded s)
    {
        var tenant = Tenant.Create("Ajeno", $"aj-{Guid.NewGuid():N}", s.UserId);
        await DbAsync(db =>
        {
            db.Tenants.Add(tenant);
            return Task.CompletedTask;
        });
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
        var bank = Bank.Create(tenant.Id, "AJENO", "Banco ajeno", "Ajeno", s.UserId);
        await DbAsync(db =>
        {
            db.Companies.Add(company);
            db.BusinessPartners.Add(partner);
            db.Add(bank);
            return Task.CompletedTask;
        });
        var account = Account.Create(
            tenant.Id,
            company.Id,
            AccountCode.Create("1.1.01"),
            "Banco",
            null,
            AccountType.Asset,
            AccountNature.Debit,
            allowsPosting: true,
            createdBy: s.UserId
        );
        var bankAccount = CompanyBankAccount.Create(
            tenant.Id,
            company.Id,
            bank.Id,
            BankAccountType.Checking,
            "5500000001",
            "Cuenta ajena",
            account.Id,
            s.UserId
        );
        await DbAsync(async db =>
        {
            db.Accounts.Add(account);
            await db.SaveChangesAsync();
            db.Add(bankAccount);
        });
        return (partner.Id, bankAccount.Id);
    }
}
