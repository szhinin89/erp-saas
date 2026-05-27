namespace ERP.API.Contracts.Subscribers;

public sealed class UpdateSubscriberCompanyRequest
{
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public int DisplayOrder { get; set; }
    public int Priority { get; set; }
    public string PreferredLanguage { get; set; } = "es";
}
