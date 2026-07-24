namespace ERP.Application.Common.Services;

public sealed record TaxRateResult(decimal Rate, string Name);

public interface ISriTaxResolver
{
    Task<decimal?> GetVatRateAsync(string vatCode, CancellationToken ct = default);
    Task<decimal?> GetIceRateAsync(string iceCode, CancellationToken ct = default);

    Task<TaxRateResult?> GetVatRateWithNameAsync(string vatCode, CancellationToken ct = default);
    Task<TaxRateResult?> GetIceRateWithNameAsync(string iceCode, CancellationToken ct = default);

    Task<string?> GetPaymentMethodNameAsync(string code, CancellationToken ct = default);
}
