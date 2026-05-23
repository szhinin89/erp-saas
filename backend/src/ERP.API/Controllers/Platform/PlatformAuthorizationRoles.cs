namespace ERP.API.Controllers.Platform;

/// <summary>JWT role claims for platform operators (Authorize Roles string).</summary>
public static class PlatformAuthorizationRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Support = "Support";
    public const string BillingAdmin = "BillingAdmin";
    public const string Auditor = "Auditor";

    public const string Operators = "SuperAdmin,Support,BillingAdmin,Auditor";
    public const string Mutators = "SuperAdmin";
    public const string BillingReaders = "SuperAdmin,BillingAdmin";
}
