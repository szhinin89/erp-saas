namespace ERP.Domain.MasterData.Models;

public sealed record BusinessPartnerDisplayInfo(
    Guid Id,
    string? TradeName,
    string? LegalName,
    string? IdentificationNumber,
    string? IdentificationType = null
)
{
    public string? DisplayName =>
        !string.IsNullOrWhiteSpace(TradeName) ? TradeName
        : !string.IsNullOrWhiteSpace(LegalName) ? LegalName
        : !string.IsNullOrWhiteSpace(IdentificationNumber) ? IdentificationNumber
        : null;
}
