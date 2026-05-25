namespace ERP.API.Contracts.Subscribers;

public sealed class UpdateSubscriberCompanyRequest
{
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string? Ruc { get; set; }
    public string? ShortName { get; set; }
    public string? TradeName { get; set; }
    public string? Dinardap { get; set; }
    public string? LogoUrl { get; set; }
    public int DisplayOrder { get; set; }
    public int Priority { get; set; }
}

public sealed class UpdateSubscriberOperationalSettingsRequest
{
    public string Currency { get; set; } = "USD";
    public string Language { get; set; } = "es";
    public string Timezone { get; set; } = "America/Guayaquil";
    public string? InvoicePrefix { get; set; }
    public int DefaultCreditDays { get; set; } = 30;
}
