using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ERP.Application.Modules.Payables.UseCases;

namespace ERP.Application.Modules.Caja.FundingRequests;

// ── Contrato persistente V1 (NUNCA el command CLR) ──────────────────────────
//
// ZH-CASH-FUNDING-REQUEST-FOUNDATION-02E-B — intención completa y determinista de un pago a
// proveedor guardada en CashFundingRequest.PaymentPayload (jsonb). Solo datos del usuario necesarios
// para reconstruir el pago: nada de contexto (tenant/empresa/sucursal/actores viven en columnas
// propias de la solicitud) ni de estado derivable. Cambiar la forma = nueva versión (V2), nunca
// editar V1: las solicitudes ya guardadas deben poder reconstruirse siempre.

public sealed record CashFundingPaymentMethodLineV1(
    Guid PaymentMethodId,
    Guid? CompanyBankAccountId,
    Guid? CashRegisterId,
    decimal Amount,
    string? ReferenceNumber,
    string? CheckNumber,
    DateOnly? CheckDate,
    string? Notes,
    DateOnly? TransactionDate
);

public sealed record CashFundingPaymentApplicationLineV1(Guid AccountsPayableInstallmentId, decimal AmountApplied);

public sealed record CashFundingPaymentAllocationV1(int MethodLineIndex, int ApplicationLineIndex, decimal Amount);

public sealed record CashFundingPaymentSnapshotV1(
    Guid SupplierId,
    DateOnly PaymentDate,
    decimal TotalAmount,
    string? ReceiptNumber,
    IReadOnlyList<CashFundingPaymentMethodLineV1> MethodLines,
    IReadOnlyList<CashFundingPaymentApplicationLineV1> ApplicationLines,
    IReadOnlyList<CashFundingPaymentAllocationV1> Allocations,
    bool ConfirmUnappliedAmount
);

/// <summary>
/// ZH-CASH-FUNDING-REQUEST-FOUNDATION-02E-B — conversión intención ↔ snapshot V1, representación
/// canónica y huella. El hash se calcula SIEMPRE sobre la serialización canónica del objeto (orden
/// de propiedades fijo, camelCase, sin espacios, decimales sin ceros de relleno), nunca sobre el
/// texto leído de BD: jsonb normaliza claves/espacios, así que payload → snapshot → canónico
/// reproduce exactamente la huella original.
/// </summary>
public static class CashFundingPaymentSnapshot
{
    public const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions CanonicalOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new CanonicalDecimalConverter() },
    };

    public static CashFundingPaymentSnapshotV1 FromIntent(RegisterSupplierPaymentCommand intent) =>
        new(
            intent.SupplierId,
            intent.PaymentDate,
            intent.TotalAmount,
            string.IsNullOrWhiteSpace(intent.ReceiptNumber) ? null : intent.ReceiptNumber.Trim(),
            intent
                .MethodLines.Select(l => new CashFundingPaymentMethodLineV1(
                    l.PaymentMethodId,
                    l.CompanyBankAccountId,
                    l.CashRegisterId,
                    l.Amount,
                    l.ReferenceNumber,
                    l.CheckNumber,
                    l.CheckDate,
                    l.Notes,
                    l.TransactionDate
                ))
                .ToList(),
            intent
                .ApplicationLines.Select(l => new CashFundingPaymentApplicationLineV1(
                    l.AccountsPayableInstallmentId,
                    l.AmountApplied
                ))
                .ToList(),
            intent
                .Allocations.Select(a => new CashFundingPaymentAllocationV1(
                    a.MethodLineIndex,
                    a.ApplicationLineIndex,
                    a.Amount
                ))
                .ToList(),
            intent.ConfirmUnappliedAmount
        );

    /// <summary>Reconstruye la intención exacta para el núcleo de registro de pagos.</summary>
    public static RegisterSupplierPaymentCommand ToIntent(CashFundingPaymentSnapshotV1 snapshot) =>
        new(
            snapshot.SupplierId,
            snapshot.PaymentDate,
            snapshot.TotalAmount,
            snapshot.ReceiptNumber,
            snapshot
                .MethodLines.Select(l => new SupplierPaymentMethodLineRequest(
                    l.PaymentMethodId,
                    l.CompanyBankAccountId,
                    l.CashRegisterId,
                    l.Amount,
                    l.ReferenceNumber,
                    l.CheckNumber,
                    l.CheckDate,
                    l.Notes,
                    l.TransactionDate
                ))
                .ToList(),
            snapshot
                .ApplicationLines.Select(l => new SupplierPaymentApplicationLineRequest(
                    l.AccountsPayableInstallmentId,
                    l.AmountApplied
                ))
                .ToList(),
            snapshot
                .Allocations.Select(a => new SupplierPaymentAllocationLineRequest(
                    a.MethodLineIndex,
                    a.ApplicationLineIndex,
                    a.Amount
                ))
                .ToList(),
            snapshot.ConfirmUnappliedAmount
        );

    public static string Serialize(CashFundingPaymentSnapshotV1 snapshot) =>
        JsonSerializer.Serialize(snapshot, CanonicalOptions);

    public static CashFundingPaymentSnapshotV1 Deserialize(string payload, int payloadVersion)
    {
        if (payloadVersion != CurrentVersion)
            throw new InvalidOperationException(
                $"Versión de la intención de pago no soportada: {payloadVersion}."
            );
        return JsonSerializer.Deserialize<CashFundingPaymentSnapshotV1>(payload, CanonicalOptions)
            ?? throw new InvalidOperationException("La intención de pago guardada está vacía.");
    }

    /// <summary>SHA-256 (hex en mayúsculas, 64 caracteres) de la representación canónica.</summary>
    public static string ComputeHash(CashFundingPaymentSnapshotV1 snapshot) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Serialize(snapshot))));

    /// <summary>
    /// Caja objetivo única del snapshot: el Id si TODAS las líneas de efectivo apuntan a una sola
    /// caja; <c>null</c> si no hay efectivo o hay más de una caja (una solicitud atiende exactamente
    /// una caja ajena — diseño 02E-A).
    /// </summary>
    public static Guid? SingleCashRegisterId(CashFundingPaymentSnapshotV1 snapshot)
    {
        var registers = snapshot
            .MethodLines.Where(l => l.CashRegisterId is not null)
            .Select(l => l.CashRegisterId!.Value)
            .Distinct()
            .ToList();
        return registers.Count == 1 ? registers[0] : null;
    }

    /// <summary>Efectivo total pedido a una caja (Σ de sus líneas).</summary>
    public static decimal CashAmountFor(CashFundingPaymentSnapshotV1 snapshot, Guid cashRegisterId) =>
        snapshot.MethodLines.Where(l => l.CashRegisterId == cashRegisterId).Sum(l => l.Amount);

    /// <summary>Decimales canónicos: mismo valor ⇒ mismo texto (80, 80.0 y 80.00 → "80").</summary>
    private sealed class CanonicalDecimalConverter : JsonConverter<decimal>
    {
        public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.GetDecimal();

        public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) =>
            writer.WriteRawValue(value.ToString("0.############################", CultureInfo.InvariantCulture));
    }
}
