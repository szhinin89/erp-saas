namespace ERP.API.Controllers.Platform;

/// <summary>JWT role claims for platform operators (Authorize Roles string).</summary>
public static class PlatformAuthorizationRoles
{
    public const string PlatformOperator = "PlatformOperator";
    public const string Support = "Support";
    public const string BillingAdmin = "BillingAdmin";
    public const string Auditor = "Auditor";

    public const string Operators = "PlatformOperator,Support,BillingAdmin,Auditor";
    public const string Mutators = PlatformOperator;
    public const string BillingReaders = "PlatformOperator,BillingAdmin";
}
