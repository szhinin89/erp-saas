using System.Text.Json.Nodes;
using ERP.Application.Modules.Caja.FundingRequests;
using ERP.Application.Modules.Payables.UseCases;
using FluentAssertions;

namespace ERP.Application.Tests.Caja;

/// <summary>
/// ZH-CASH-FUNDING-REQUEST-FOUNDATION-02E-B — snapshot V1 de la intención de pago: roundtrip exacto
/// intención → snapshot → JSON → snapshot → intención, hash estable (independiente de la escala de
/// los decimales y de la normalización de jsonb), versión obligatoria y caja objetivo única.
/// </summary>
public sealed class CashFundingPaymentSnapshotTests
{
    private static readonly Guid Supplier = Guid.NewGuid();
    private static readonly Guid Cash = Guid.NewGuid();
    private static readonly Guid Bank = Guid.NewGuid();
    private static readonly Guid CashMethod = Guid.NewGuid();
    private static readonly Guid BankMethod = Guid.NewGuid();
    private static readonly Guid Installment = Guid.NewGuid();

    private static RegisterSupplierPaymentCommand MixedIntent(decimal cash = 80m, decimal bank = 120m) =>
        new(
            Supplier,
            new DateOnly(2026, 9, 20),
            cash + bank,
            " REC-99 ",
            [
                new SupplierPaymentMethodLineRequest(BankMethod, Bank, null, bank, "OP-7788", TransactionDate: new DateOnly(2026, 9, 19)),
                new SupplierPaymentMethodLineRequest(CashMethod, null, Cash, cash, Notes: "Entrega en caja"),
            ],
            [new SupplierPaymentApplicationLineRequest(Installment, cash + bank)],
            [new SupplierPaymentAllocationLineRequest(0, 0, bank), new SupplierPaymentAllocationLineRequest(1, 0, cash)],
            ConfirmUnappliedAmount: false
        );

    /// <summary>
    /// ZH-FINANCIAL-COMMAND-IDEMPOTENCY-01 — valor fijo: el JSON canónico y su huella son contrato
    /// persistente (solicitudes guardadas + pagos directos idempotentes). Cualquier cambio del
    /// serializador canónico compartido que altere un solo byte rompe este test.
    /// </summary>
    [Fact]
    public void Huella_canonica_es_estable_valor_fijo()
    {
        var intent = new RegisterSupplierPaymentCommand(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            new DateOnly(2026, 9, 20),
            200.50m,
            " REC-99 ",
            [
                new SupplierPaymentMethodLineRequest(Guid.Parse("22222222-2222-2222-2222-222222222222"), null, Guid.Parse("33333333-3333-3333-3333-333333333333"), 200.500m, Notes: "Entrega"),
            ],
            [new SupplierPaymentApplicationLineRequest(Guid.Parse("44444444-4444-4444-4444-444444444444"), 200.5m)],
            [new SupplierPaymentAllocationLineRequest(0, 0, 200.50m)],
            ConfirmUnappliedAmount: false
        );

        var snapshot = CashFundingPaymentSnapshot.FromIntent(intent);

        CashFundingPaymentSnapshot.Serialize(snapshot).Should().Be(
            """{"supplierId":"11111111-1111-1111-1111-111111111111","paymentDate":"2026-09-20","totalAmount":200.5,"receiptNumber":"REC-99","methodLines":[{"paymentMethodId":"22222222-2222-2222-2222-222222222222","companyBankAccountId":null,"cashRegisterId":"33333333-3333-3333-3333-333333333333","amount":200.5,"referenceNumber":null,"checkNumber":null,"checkDate":null,"notes":"Entrega","transactionDate":null}],"applicationLines":[{"accountsPayableInstallmentId":"44444444-4444-4444-4444-444444444444","amountApplied":200.5}],"allocations":[{"methodLineIndex":0,"applicationLineIndex":0,"amount":200.5}],"confirmUnappliedAmount":false}"""
        );
        CashFundingPaymentSnapshot.ComputeHash(snapshot)
            .Should().Be("69FBAFF583E455A0DB952B6BB8396649D5CD657CDCF9972A5F34697CE2528D82");
    }

    [Fact]
    public void Roundtrip_reconstruye_exactamente_la_intencion()
    {
        var intent = MixedIntent();
        var snapshot = CashFundingPaymentSnapshot.FromIntent(intent);

        var json = CashFundingPaymentSnapshot.Serialize(snapshot);
        var restored = CashFundingPaymentSnapshot.ToIntent(
            CashFundingPaymentSnapshot.Deserialize(json, CashFundingPaymentSnapshot.CurrentVersion)
        );

        restored.Should().BeEquivalentTo(intent with { ReceiptNumber = "REC-99" }, o => o.WithStrictOrdering());
    }

    [Fact]
    public void Hash_es_estable_y_de_64_caracteres()
    {
        var a = CashFundingPaymentSnapshot.ComputeHash(CashFundingPaymentSnapshot.FromIntent(MixedIntent()));
        var b = CashFundingPaymentSnapshot.ComputeHash(CashFundingPaymentSnapshot.FromIntent(MixedIntent()));

        a.Should().Be(b).And.HaveLength(64);
    }

    [Fact]
    public void Hash_no_depende_de_la_escala_de_los_decimales()
    {
        var plain = CashFundingPaymentSnapshot.ComputeHash(CashFundingPaymentSnapshot.FromIntent(MixedIntent(80m, 120m)));
        var scaled = CashFundingPaymentSnapshot.ComputeHash(CashFundingPaymentSnapshot.FromIntent(MixedIntent(80.00m, 120.0m)));

        scaled.Should().Be(plain);
    }

    [Fact]
    public void Hash_cambia_si_cambia_la_intencion()
    {
        var a = CashFundingPaymentSnapshot.ComputeHash(CashFundingPaymentSnapshot.FromIntent(MixedIntent(80m)));
        var b = CashFundingPaymentSnapshot.ComputeHash(CashFundingPaymentSnapshot.FromIntent(MixedIntent(80.01m)));

        b.Should().NotBe(a);
    }

    [Fact]
    public void Hash_sobrevive_la_normalizacion_de_jsonb_orden_de_claves_y_espacios()
    {
        var snapshot = CashFundingPaymentSnapshot.FromIntent(MixedIntent());
        var original = CashFundingPaymentSnapshot.ComputeHash(snapshot);

        // Simula jsonb: mismas claves/valores, otro orden y otro espaciado.
        var node = JsonNode.Parse(CashFundingPaymentSnapshot.Serialize(snapshot))!.AsObject();
        var reordered = new JsonObject(node.Reverse().Select(p => KeyValuePair.Create(p.Key, p.Value?.DeepClone())));
        var normalized = reordered.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

        var reread = CashFundingPaymentSnapshot.Deserialize(normalized, 1);
        CashFundingPaymentSnapshot.ComputeHash(reread).Should().Be(original);
    }

    [Fact]
    public void Version_no_soportada_se_rechaza()
    {
        var json = CashFundingPaymentSnapshot.Serialize(CashFundingPaymentSnapshot.FromIntent(MixedIntent()));

        FluentActions.Invoking(() => CashFundingPaymentSnapshot.Deserialize(json, 2))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Caja_objetivo_unica_y_efectivo_pedido()
    {
        var snapshot = CashFundingPaymentSnapshot.FromIntent(MixedIntent(80m, 120m));

        CashFundingPaymentSnapshot.SingleCashRegisterId(snapshot).Should().Be(Cash);
        CashFundingPaymentSnapshot.CashAmountFor(snapshot, Cash).Should().Be(80m);

        var bankOnly = snapshot with { MethodLines = [snapshot.MethodLines[0]] };
        CashFundingPaymentSnapshot.SingleCashRegisterId(bankOnly).Should().BeNull("sin efectivo no hay caja objetivo");

        var twoRegisters = snapshot with
        {
            MethodLines = [snapshot.MethodLines[1], snapshot.MethodLines[1] with { CashRegisterId = Guid.NewGuid() }],
        };
        CashFundingPaymentSnapshot.SingleCashRegisterId(twoRegisters).Should().BeNull("una solicitud atiende exactamente una caja");
    }

    [Fact]
    public void El_contrato_no_contiene_contexto_ni_actores()
    {
        var json = CashFundingPaymentSnapshot.Serialize(CashFundingPaymentSnapshot.FromIntent(MixedIntent()));
        var keys = JsonNode.Parse(json)!.AsObject().Select(p => p.Key).ToList();

        keys.Should().Equal(
            "supplierId", "paymentDate", "totalAmount", "receiptNumber",
            "methodLines", "applicationLines", "allocations", "confirmUnappliedAmount");
    }
}
