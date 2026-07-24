namespace ERP.Application.Modules.Purchases.Services;

public sealed record RetentionCodeInfo(string Code, string Name, decimal Percentage);

public interface IRetentionCodeResolver
{
    Task<RetentionCodeInfo?> GetRetentionCodeAsync(string code, string taxType, CancellationToken ct = default);
}
