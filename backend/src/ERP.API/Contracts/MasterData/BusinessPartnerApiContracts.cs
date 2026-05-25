namespace ERP.API.Contracts.MasterData;

public sealed class UpsertCompanyBpSettingsRequest
{
    public decimal? CreditLimit { get; set; }
    public short PaymentDays { get; set; }
    public bool IsBlocked { get; set; }
}

public sealed class CreateBusinessPartnerRequest
{
    public string IdentificationType { get; set; } = "";
    public string IdentificationNumber { get; set; } = "";
    public string LegalName { get; set; } = "";
    public string? TradeName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? CountryCode { get; set; }
    public bool AsCustomer { get; set; }
    public bool AsSupplier { get; set; }
}

public sealed class UpdateBusinessPartnerRequest
{
    public string IdentificationType { get; set; } = "";
    public string IdentificationNumber { get; set; } = "";
    public string LegalName { get; set; } = "";
    public string? TradeName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? CountryCode { get; set; }
}

public sealed class AddBusinessPartnerRoleRequest
{
    public bool AsCustomer { get; set; }
    public bool AsSupplier { get; set; }
}

public sealed class UpdateCustomerNotesRequest
{
    public string? Notes { get; set; }
}

public sealed class UpdateSupplierProfileRequest
{
    public string? DefaultTaxSupportCode { get; set; }
    public string? DefaultRetentionVatCode { get; set; }
    public string? DefaultRetentionIncomeCode { get; set; }
    public string? PaymentTerms { get; set; }
}
