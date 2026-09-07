namespace ERP.Application.Modules.Purchases.Services;

public sealed record RetentionCodeInfo(string TaxType, string Code, string Name, decimal Percentage);

public interface IRetentionCodeResolver
{
    Task<RetentionCodeInfo?> GetRetentionCodeAsync(
        string code,
        string taxType,
        CancellationToken ct = default
    );

    /// <summary>
    /// RETENTIONS-SUPPLIER-DEFAULTS-DYNAMIC-01: resuelve un código de retención por Id de catálogo
    /// (FK real desde SupplierRetentionDefault.SriRetentionCodeId), devolviendo null si el código
    /// no existe o ya no está activo — nunca lanza excepción por dato huérfano.
    /// </summary>
    Task<RetentionCodeInfo?> GetRetentionCodeByIdAsync(
        Guid sriRetentionCodeId,
        CancellationToken ct = default
    );
}
