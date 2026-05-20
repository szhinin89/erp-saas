namespace ERP.Application.Modules.Platform.Companies;

/// <summary>SaaS platform — fiscal company management (not ERP runtime).</summary>
public static class CompanyPermissions
{
    public const string View = "saas.companies.view";
    public const string Create = "saas.companies.create";
    public const string Update = "saas.companies.update";

    public const string PolicyView = "perm:" + View;
    public const string PolicyCreate = "perm:" + Create;
    public const string PolicyUpdate = "perm:" + Update;
}
